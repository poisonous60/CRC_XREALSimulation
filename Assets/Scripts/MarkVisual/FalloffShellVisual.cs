using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faint inverse-square shells drawn around one marker.
/// </summary>
[DisallowMultipleComponent]
public class FalloffShellVisual : MonoBehaviour
{
    private SourceMarker marker;
    private MarkerVisualSettings settings;

    public void Initialize(SourceMarker owner, MarkerVisualSettings visualSettings)
    {
        marker = owner;
        settings = visualSettings;
    }

    public void Refresh(float centerCps, RadiationRiskBand centerBand)
    {
        Hide();

        if (settings == null ||
            !settings.showFalloffShells ||
            marker == null ||
            !marker.isPlaced ||
            (centerBand != RadiationRiskBand.Red && centerBand != RadiationRiskBand.Yellow))
        {
            return;
        }

        EnsurePool();
        if (marker.falloffShells == null || marker.falloffShells.Count == 0)
            return;

        float referenceDistance = Mathf.Max(0.01f, settings.falloffReferenceDistanceMeters);
        float centerRadius = Mathf.Max(0.01f, settings.fixedMarkerSize * 0.5f);
        float maximumRadius = Mathf.Max(centerRadius, settings.falloffMaxRadiusMeters);
        int availableShells = Mathf.Min(
            Mathf.Clamp(settings.maxFalloffShells, 1, 3),
            marker.falloffShells.Count);
        int shellIndex = 0;
        float lastConfiguredRadius = centerRadius;

        float hiddenThreshold = Mathf.Max(0.001f, settings.hiddenMaxCps);
        float greenThreshold = Mathf.Max(hiddenThreshold, settings.greenMaxCps);
        float redThreshold = Mathf.Max(greenThreshold, settings.dangerThresholdCps);
        float redBoundaryRadius =
            referenceDistance * Mathf.Sqrt(centerCps / redThreshold);
        float yellowBoundaryRadius =
            referenceDistance * Mathf.Sqrt(centerCps / greenThreshold);
        float greenBoundaryRadius =
            referenceDistance * Mathf.Sqrt(centerCps / hiddenThreshold);

        // Each shell marks the outer edge of the like-colored approach zone.
        // A barely-red reading keeps its red boundary inside the fixed center;
        // a much stronger reading expands that red zone correctly.
        if (centerBand == RadiationRiskBand.Red)
        {
            TryConfigureBoundary(
                ref shellIndex,
                ref lastConfiguredRadius,
                availableShells,
                redBoundaryRadius,
                maximumRadius,
                RadiationRiskBand.Red);
        }

        TryConfigureBoundary(
            ref shellIndex,
            ref lastConfiguredRadius,
            availableShells,
            yellowBoundaryRadius,
            maximumRadius,
            RadiationRiskBand.Yellow);

        TryConfigureBoundary(
            ref shellIndex,
            ref lastConfiguredRadius,
            availableShells,
            greenBoundaryRadius,
            maximumRadius,
            RadiationRiskBand.Green);

        // If the true green boundary is beyond the configured view range, show
        // the actually estimated band at the endpoint instead of a false safe
        // boundary. This is most relevant for exceptionally high readings.
        if (greenBoundaryRadius > maximumRadius && shellIndex < availableShells)
        {
            float ratio = referenceDistance / Mathf.Max(maximumRadius, referenceDistance);
            RadiationRiskBand endpointBand = MarkerRisk.GetBand(centerCps * ratio * ratio, settings);
            TryConfigureBoundary(
                ref shellIndex,
                ref lastConfiguredRadius,
                availableShells,
                maximumRadius,
                maximumRadius,
                endpointBand);
        }
    }

    public void Hide()
    {
        if (marker == null || marker.falloffShells == null)
            return;

        for (int i = 0; i < marker.falloffShells.Count; i++)
        {
            FalloffShellInfo shell = marker.falloffShells[i];
            if (shell != null)
                shell.visualRequested = false;
        }
    }

    public void DestroyMaterials()
    {
        if (marker == null || marker.falloffShells == null)
            return;

        for (int i = 0; i < marker.falloffShells.Count; i++)
        {
            FalloffShellInfo shell = marker.falloffShells[i];
            if (shell == null || shell.material == null)
                continue;

            Destroy(shell.material);
            shell.material = null;
        }
    }

    private bool TryConfigureBoundary(
        ref int shellIndex,
        ref float lastConfiguredRadius,
        int availableShells,
        float radiusMeters,
        float maximumRadius,
        RadiationRiskBand band)
    {
        if (shellIndex >= availableShells ||
            radiusMeters > maximumRadius ||
            radiusMeters <= lastConfiguredRadius * 1.05f ||
            band == RadiationRiskBand.Hidden ||
            band == RadiationRiskBand.Unknown)
        {
            return false;
        }

        ConfigureShell(shellIndex, radiusMeters, band);
        shellIndex++;
        lastConfiguredRadius = radiusMeters;
        return true;
    }

    private void EnsurePool()
    {
        if (marker == null)
            return;

        if (marker.falloffShells == null)
            marker.falloffShells = new List<FalloffShellInfo>();

        int desiredCount = Mathf.Clamp(settings.maxFalloffShells, 1, 3);
        while (marker.falloffShells.Count < desiredCount)
        {
            int shellIndex = marker.falloffShells.Count;
            GameObject shellObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shellObject.name = $"FalloffShell_{shellIndex + 1}";
            shellObject.layer = gameObject.layer;
            shellObject.transform.SetParent(transform, false);
            shellObject.transform.localPosition = Vector3.zero;
            shellObject.transform.localRotation = Quaternion.identity;
            shellObject.transform.localScale = Vector3.one;

            Collider collider = shellObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Destroy(collider);
            }

            Renderer shellRenderer = shellObject.GetComponent<Renderer>();
            Material shellMaterial = null;
            if (shellRenderer != null)
            {
                Shader shader = MarkerMaterials.GetDetectorTransparentShader();
                if (shader == null)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                if (shader != null)
                {
                    shellMaterial = new Material(shader);
                    shellRenderer.material = shellMaterial;
                }

                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shellRenderer.enabled = false;
            }

            marker.falloffShells.Add(new FalloffShellInfo
            {
                root = shellObject,
                renderer = shellRenderer,
                visualRequested = false,
                material = shellMaterial
            });
        }
    }

    private void ConfigureShell(int shellIndex, float radiusMeters, RadiationRiskBand band)
    {
        if (marker == null ||
            marker.falloffShells == null ||
            shellIndex < 0 ||
            shellIndex >= marker.falloffShells.Count)
        {
            return;
        }

        FalloffShellInfo shell = marker.falloffShells[shellIndex];
        if (shell == null || shell.root == null)
            return;

        float centerDiameter = Mathf.Max(0.001f, settings.fixedMarkerSize);
        shell.root.transform.localPosition = Vector3.zero;
        shell.root.transform.localRotation = Quaternion.identity;
        shell.root.transform.localScale =
            Vector3.one * ((radiusMeters * 2f) / centerDiameter);

        Color color = MarkerRisk.GetColorForBand(band, settings);
        color.a = settings.falloffShellAlpha;

        // Inner shells render after outer shells; the center sphere renders last.
        int queueOffset = Mathf.Clamp(settings.maxFalloffShells, 1, 3) - shellIndex;
        shell.material =
            MarkerMaterials.SetRendererTransparentColor(shell.renderer, color, queueOffset, true, settings);
        shell.visualRequested = true;
    }
}
