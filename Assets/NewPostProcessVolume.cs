using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System;

[Serializable, VolumeComponentMenu("Post-processing/Custom/NewPostProcessVolume")]
public sealed class NewPostProcessVolume : CustomPostProcessVolumeComponent, IPostProcessComponent
{
    [Tooltip("Controls the intensity of the effect.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    Material m_Material; // Material for the main effect shader
    Material m_BlitSliceMaterial; // Material for the simple slice blit shader

    // Path to your main effect shader graph
    // *** IMPORTANT: Ensure this shader graph has an exposed Texture2D property named "_MainTex" ***
    const string kShaderName = "Shader Graphs/CustomPostProcess";

    // Path to the simple blit shader (this MUST handle Texture2DArray input as _MainTex)
    const string kBlitShaderName = "Hidden/BlitTexture2DArray";

    // Property IDs for efficiency
    static readonly int _IntensityID = Shader.PropertyToID("_Intensity");
    // *** CHANGE: Use _MainTex for the main effect shader's input ***
    static readonly int _MainTexID = Shader.PropertyToID("_MainTex");
    // Property ID for the blit shader's input (expects Texture2DArray) - Keep separate name for clarity
    static readonly int _MainTexBlitID = Shader.PropertyToID("_MainTex");
    // Property ID for the blit shader's slice index input
    static readonly int _SliceIndexID = Shader.PropertyToID("_SliceIndex");

    // Check both materials are loaded, as blit might be needed even if main effect is simple
    // Note: IsActive check might need adjustment if blit material is optional/fails gracefully
    public bool IsActive() => m_Material != null && intensity.value > 0f; // Blit material check happens in Render

    public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

    public override void Setup()
    {
        // Load Main Effect Material
        Shader mainShader = Shader.Find(kShaderName);
        if (mainShader != null)
        {
            m_Material = CoreUtils.CreateEngineMaterial(mainShader);
            // Optional: Check if the main shader has the _MainTex property
            if (!m_Material.HasProperty(_MainTexID))
                 Debug.LogWarning($"Shader '{kShaderName}' might be missing the '_MainTex' property needed by NewPostProcessVolume.");
        }
        else
            Debug.LogError($"Unable to find shader '{kShaderName}'. Post Process Volume NewPostProcessVolume will not work.");

        // Load Blit Material
        Shader blitShader = Shader.Find(kBlitShaderName);
        if (blitShader != null)
        {
            m_BlitSliceMaterial = CoreUtils.CreateEngineMaterial(blitShader);
             // Check if the blit shader has the _MainTex property (it should)
            if (!m_BlitSliceMaterial.HasProperty(_MainTexBlitID))
                 Debug.LogWarning($"Shader '{kBlitShaderName}' might be missing the '_MainTex' property needed for slice blitting.");
        }
        else
            // Log error only once during setup if not found
            Debug.LogError($"Unable to find shader '{kBlitShaderName}'. Slice blitting for Texture2DArray sources will fail.");
    }

    public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
    {
        // Passthrough if effect is inactive or the main material failed to load
        if (m_Material == null || intensity.value <= 0f)
        {
            HDUtils.BlitCameraTexture(cmd, source, destination);
            return;
        }

        RTHandle sourceTextureForEffect = source; // This will hold the Texture2D for the main effect
        bool temporaryAllocated = false;

        // --- Check if source is a Texture2DArray and blit slice 0 if necessary ---
        if (source.rt != null && source.rt.dimension == TextureDimension.Tex2DArray)
        {
            // Ensure the blit material is available for array processing
            if (m_BlitSliceMaterial == null)
            {
                // Log error once per frame to prevent spam if blit material failed setup
                Debug.LogError($"Blit Slice Material ('{kBlitShaderName}') is missing or failed to load for {nameof(NewPostProcessVolume)}. Cannot process Texture2DArray source. Passthrough.");
                HDUtils.BlitCameraTexture(cmd, source, destination);
                return;
            }

            // Get a temporary RT descriptor matching the source but as Texture2D
            RenderTextureDescriptor descriptor = source.rt.descriptor;
            descriptor.dimension = TextureDimension.Tex2D;
            descriptor.depthBufferBits = 0;
            descriptor.vrUsage = VRTextureUsage.None;
            descriptor.useMipMap = false;

            RTHandle tempRT = RTHandles.Alloc(descriptor, name: "TempBlitSliceTex2D");

            // --- Configure and execute the slice blit ---
            // Set properties for the blit shader (Hidden/BlitTexture2DArray)
            m_BlitSliceMaterial.SetTexture(_MainTexBlitID, source); // Assign source ARRAY here
            m_BlitSliceMaterial.SetInt(_SliceIndexID, 0);

            // Blit slice 0 from source (Texture2DArray) to tempRT (Texture2D)
            HDUtils.DrawFullScreen(cmd, m_BlitSliceMaterial, tempRT, shaderPassId: 0);

            // The main effect will now use the temporary Texture2D
            sourceTextureForEffect = tempRT;
            temporaryAllocated = true;
        }
        // --- End Slice Blit ---

        // --- Apply Main Effect ---
        // Set properties for the main effect shader (Shader Graphs/CustomPostProcess)
        // This shader *must* expect _MainTex to be a Texture2D
        m_Material.SetFloat(_IntensityID, intensity.value);
        // *** CHANGE: Use _MainTexID to assign the texture to the main material ***
        m_Material.SetTexture(_MainTexID, sourceTextureForEffect); // Assign the guaranteed Texture2D

        // Execute the main post-process effect using the (potentially temporary) Texture2D source
        HDUtils.DrawFullScreen(cmd, m_Material, destination, shaderPassId: 0);
        // --- End Main Effect ---

        // --- Cleanup ---
        // Release the temporary RT if it was allocated
        if (temporaryAllocated)
        {
            RTHandles.Release(sourceTextureForEffect);
        }
    }

    public override void Cleanup()
    {
        CoreUtils.Destroy(m_Material);
        CoreUtils.Destroy(m_BlitSliceMaterial);
    }
}
