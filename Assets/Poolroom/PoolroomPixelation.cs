using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
[SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
[VolumeComponentMenu("Post-processing/Custom/Poolroom Pixelation")]
public sealed class PoolroomPixelation : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    private static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
    private static readonly int StrengthId = Shader.PropertyToID("_PixelationStrength");
    private const string ShaderResourceName = "Poolroom Pixelation";

    [Tooltip("Controls the size of the screen pixels. Zero turns the effect off.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0f, 0f, 1f);

    private Material material;

    public bool IsActive()
    {
        return material != null && strength.value > 0f;
    }

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup()
    {
        Shader shader = Resources.Load<Shader>(ShaderResourceName);
        if (shader == null)
        {
            Debug.LogError($"Poolroom pixelation could not load the '{ShaderResourceName}' shader.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Render(
        CommandBuffer commandBuffer,
        HDCamera camera,
        RTHandle source,
        RTHandle destination)
    {
        if (material == null)
        {
            return;
        }

        material.SetTexture(InputTextureId, source);
        material.SetFloat(StrengthId, strength.value);
        HDUtils.DrawFullScreen(commandBuffer, material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}
