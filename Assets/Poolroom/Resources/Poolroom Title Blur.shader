Shader "Hidden/Poolroom/Title Blur"
{
    HLSLINCLUDE

    #pragma target 4.5

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vert(Attributes input)
    {
        Varyings output;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);
        return output;
    }

    TEXTURE2D_X(_InputTexture);
    float _BlurStrength;

    float4 BlurTitleView(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float2 center = input.texcoord * _ScreenSize.xy;
        float radius = 3.0 + _BlurStrength * _BlurStrength * 18.0;
        float4 blurred = 0.0;
        float totalWeight = 0.0;

        [unroll]
        for (int y = -2; y <= 2; y++)
        {
            [unroll]
            for (int x = -2; x <= 2; x++)
            {
                float distanceSquared = (float)(x * x + y * y);
                float weight = exp(-distanceSquared * 0.45);
                float2 samplePoint = center + float2(x, y) * radius;
                uint2 samplePosition = (uint2)clamp(samplePoint, 0.0, _ScreenSize.xy - 1.0);
                blurred += LOAD_TEXTURE2D_X_LOD(_InputTexture, samplePosition, 0) * weight;
                totalWeight += weight;
            }
        }

        return blurred / totalWeight;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Poolroom Title Blur"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurTitleView
            ENDHLSL
        }
    }

    Fallback Off
}
