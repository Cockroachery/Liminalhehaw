using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[Serializable]
[SupportedOnRenderPipeline(typeof(HDRenderPipelineAsset))]
[VolumeComponentMenu("Post-processing/Custom/Poolroom Title Blur")]
public sealed class PoolroomTitleBlur : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    private static readonly int InputTextureId = Shader.PropertyToID("_InputTexture");
    private static readonly int BlurStrengthId = Shader.PropertyToID("_BlurStrength");

    [Tooltip("Controls how strongly the starting view is blurred behind the title screen.")]
    public ClampedFloatParameter strength = new ClampedFloatParameter(0f, 0f, 1f);

    private Material material;

    public bool IsActive()
    {
        return material != null && strength.value > 0f;
    }

    public override CustomPostProcessInjectionPoint injectionPoint =>
        CustomPostProcessInjectionPoint.BeforePostProcess;

    public override void Setup()
    {
        Shader shader = Resources.Load<Shader>("Poolroom Title Blur");
        if (shader == null)
        {
            Debug.LogError("Poolroom title screen could not load its blur shader.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Render(CommandBuffer commandBuffer, HDCamera camera, RTHandle source, RTHandle destination)
    {
        if (material == null)
        {
            return;
        }

        material.SetTexture(InputTextureId, source);
        material.SetFloat(BlurStrengthId, strength.value);
        HDUtils.DrawFullScreen(commandBuffer, material, destination, shaderPassId: 0);
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(material);
        material = null;
    }
}
