Shader "Liminal/Underwater Crack Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Crack Sprite", 2D) = "white" {}
        [HDR] _TintColor ("Glow Tint", Color) = (1, 0.72, 0.48, 1)
        _EmissionIntensity ("Glow Strength", Range(0, 12)) = 2.55
        [HDR] _HaloColor ("Crack-Shaped Halo Color", Color) = (1, 0.035, 0.01, 1)
        _HaloIntensity ("Crack-Shaped Halo Strength", Range(0, 8)) = 0.8
        _HaloRadius ("Crack-Shaped Halo Width", Range(1, 12)) = 3.5
        _AlphaBoost ("Edge Strength", Range(0.25, 4)) = 1.35
        _Cutoff ("Invisible Edge Cutoff", Range(0, 0.25)) = 0.015
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float4 _HaloColor;
                float _EmissionIntensity;
                float _HaloIntensity;
                float _HaloRadius;
                float _AlphaBoost;
                float _Cutoff;
            CBUFFER_END

            float CrackMask(float4 sampled)
            {
                float brightness = max(sampled.r, max(sampled.g, sampled.b));
                return saturate((brightness - 0.06) * 1.4) * sampled.a;
            }

            float SampleCrackMask(float2 uv)
            {
                return CrackMask(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float coreMask = CrackMask(sampled);

                uint textureWidth;
                uint textureHeight;
                _MainTex.GetDimensions(textureWidth, textureHeight);
                float2 texel = _HaloRadius / max(float2(textureWidth, textureHeight), 1.0);
                float2 diagonal = texel * 0.70710678;

                float innerHalo = 0.0;
                innerHalo += SampleCrackMask(input.uv + float2(texel.x, 0.0));
                innerHalo += SampleCrackMask(input.uv - float2(texel.x, 0.0));
                innerHalo += SampleCrackMask(input.uv + float2(0.0, texel.y));
                innerHalo += SampleCrackMask(input.uv - float2(0.0, texel.y));
                innerHalo += SampleCrackMask(input.uv + float2(diagonal.x, diagonal.y));
                innerHalo += SampleCrackMask(input.uv + float2(diagonal.x, -diagonal.y));
                innerHalo += SampleCrackMask(input.uv + float2(-diagonal.x, diagonal.y));
                innerHalo += SampleCrackMask(input.uv - float2(diagonal.x, diagonal.y));
                innerHalo *= 0.125;

                float2 wideTexel = texel * 2.1;
                float2 wideDiagonal = diagonal * 2.1;
                float outerHalo = 0.0;
                outerHalo += SampleCrackMask(input.uv + float2(wideTexel.x, 0.0));
                outerHalo += SampleCrackMask(input.uv - float2(wideTexel.x, 0.0));
                outerHalo += SampleCrackMask(input.uv + float2(0.0, wideTexel.y));
                outerHalo += SampleCrackMask(input.uv - float2(0.0, wideTexel.y));
                outerHalo += SampleCrackMask(input.uv + float2(wideDiagonal.x, wideDiagonal.y));
                outerHalo += SampleCrackMask(input.uv + float2(wideDiagonal.x, -wideDiagonal.y));
                outerHalo += SampleCrackMask(input.uv + float2(-wideDiagonal.x, wideDiagonal.y));
                outerHalo += SampleCrackMask(input.uv - float2(wideDiagonal.x, wideDiagonal.y));
                outerHalo *= 0.125;

                float shapedHalo = saturate(innerHalo * 0.85 + outerHalo * 0.55 - coreMask * 0.25);
                float coreAlpha = saturate(sampled.a * input.color.a * _AlphaBoost);
                float haloAlpha = shapedHalo * input.color.a * 0.8;
                float alpha = max(coreAlpha, haloAlpha);
                clip(alpha - _Cutoff);

                float3 glowingCore = sampled.rgb * input.color.rgb * _TintColor.rgb * _EmissionIntensity;
                float3 shapedRedGlow = _HaloColor.rgb * _HaloIntensity * shapedHalo * input.color.rgb;
                float3 glowingColor = glowingCore + shapedRedGlow;
                return float4(glowingColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
