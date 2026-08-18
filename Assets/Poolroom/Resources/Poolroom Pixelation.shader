Shader "Hidden/Poolroom/Pixelation"
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
    float _PixelationStrength;

    float4 Pixelate(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        float blockSize = 1.0 + _PixelationStrength * _PixelationStrength * 95.0;
        float2 positionSS = input.texcoord * _ScreenSize.xy;
        float2 blockCenter = (floor(positionSS / blockSize) + 0.5) * blockSize;
        uint2 samplePosition = (uint2)clamp(blockCenter, 0.0, _ScreenSize.xy - 1.0);
        return LOAD_TEXTURE2D_X_LOD(_InputTexture, samplePosition, 0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }

        Pass
        {
            Name "Poolroom Pixelation"
            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Pixelate
            ENDHLSL
        }
    }

    Fallback Off
}
