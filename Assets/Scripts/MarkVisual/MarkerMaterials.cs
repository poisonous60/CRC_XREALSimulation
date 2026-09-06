using UnityEngine;

/// <summary>
/// Transparent material setup shared by the center sphere and the falloff shells.
/// </summary>
public static class MarkerMaterials
{
    private static Shader cachedDetectorTransparentShader;

    // Statics survive entering play mode when Reload Domain is off, so drop the cache here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        cachedDetectorTransparentShader = null;
    }

    public static Shader GetDetectorTransparentShader()
    {
        if (cachedDetectorTransparentShader == null)
            cachedDetectorTransparentShader = Resources.Load<Shader>("RadVisDetectorTransparent");

        if (cachedDetectorTransparentShader == null)
            cachedDetectorTransparentShader = Shader.Find("RadVis/DetectorTransparent");

        return cachedDetectorTransparentShader;
    }

    public static Material SetRendererTransparentColor(
        Renderer renderer,
        Color color,
        int renderQueueOffset,
        bool renderInside,
        MarkerVisualSettings settings)
    {
        if (renderer == null || renderer.material == null)
            return null;

        Material material = renderer.material;

        // A supplied marker prefab carries its own shader, so the forced swap would undo it.
        if (settings != null && settings.forceDedicatedTransparentShader && !settings.hasMarkerPrefab)
        {
            Shader transparentShader = GetDetectorTransparentShader();
            if (transparentShader != null && material.shader != transparentShader)
                material.shader = transparentShader;
        }

        ConfigureTransparentMaterial(material);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Cull"))
        {
            material.SetFloat(
                "_Cull",
                renderInside
                    ? (float)UnityEngine.Rendering.CullMode.Off
                    : (float)UnityEngine.Rendering.CullMode.Back);
        }

        material.renderQueue =
            (int)UnityEngine.Rendering.RenderQueue.Transparent + renderQueueOffset;

        return material;
    }

    public static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
            return;

        // URP/Lit transparent setup.
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHAMODULATE_ON");
        material.SetShaderPassEnabled("ShadowCaster", false);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // Built-in Standard shader Fade mode matches SrcAlpha/OneMinusSrcAlpha.
        if (material.HasProperty("_Mode"))
            material.SetFloat("_Mode", 2f);
    }
}
