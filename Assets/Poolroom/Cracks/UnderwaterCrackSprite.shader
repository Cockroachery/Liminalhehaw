Shader "Liminal/Underwater Crack Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Crack Sprite", 2D) = "white" {}
        [HDR] _TintColor ("Glow Tint", Color) = (1, 0.72, 0.48, 1)
        _EmissionIntensity ("Glow Strength", Range(0, 12)) = 4
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
                float _EmissionIntensity;
                float _AlphaBoost;
                float _Cutoff;
            CBUFFER_END

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
                float alpha = saturate(sampled.a * input.color.a * _AlphaBoost);
                clip(alpha - _Cutoff);

                float3 glowingColor = sampled.rgb * input.color.rgb * _TintColor.rgb * _EmissionIntensity;
                return float4(glowingColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
