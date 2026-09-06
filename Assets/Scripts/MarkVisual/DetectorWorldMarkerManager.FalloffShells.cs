using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private void UpdateFalloffShellVisuals(
        SourceMarker marker,
        float centerCps,
        RadiationRiskBand centerBand)
    {
        HideFalloffShells(marker);

        if (!showFalloffShells ||
            marker == null ||
            !marker.isPlaced ||
            (centerBand != RadiationRiskBand.Red && centerBand != RadiationRiskBand.Yellow))
        {
            return;
        }

        EnsureFalloffShellPool(marker);
        if (marker.falloffShells == null || marker.falloffShells.Count == 0)
            return;

        float referenceDistance = Mathf.Max(0.01f, falloffReferenceDistanceMeters);
        float centerRadius = Mathf.Max(0.01f, fixedMarkerSize * 0.5f);
        float maximumRadius = Mathf.Max(centerRadius, falloffMaxRadiusMeters);
        int availableShells = Mathf.Min(
            Mathf.Clamp(maxFalloffShells, 1, 3),
            marker.falloffShells.Count);
        int shellIndex = 0;
        float lastConfiguredRadius = centerRadius;

        float hiddenThreshold = Mathf.Max(0.001f, hiddenMaxCps);
        float greenThreshold = Mathf.Max(hiddenThreshold, greenMaxCps);
        float redThreshold = Mathf.Max(greenThreshold, dangerThresholdCps);
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
            TryConfigureFalloffBoundary(
                marker,
                ref shellIndex,
                ref lastConfiguredRadius,
                availableShells,
                redBoundaryRadius,
                maximumRadius,
                RadiationRiskBand.Red);
        }

        TryConfigureFalloffBoundary(
            marker,
            ref shellIndex,
            ref lastConfiguredRadius,
            availableShells,
            yellowBoundaryRadius,
            maximumRadius,
            RadiationRiskBand.Yellow);

        TryConfigureFalloffBoundary(
            marker,
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
            RadiationRiskBand endpointBand = GetRiskBand(centerCps * ratio * ratio);
            TryConfigureFalloffBoundary(
                marker,
                ref shellIndex,
                ref lastConfiguredRadius,
                availableShells,
                maximumRadius,
                maximumRadius,
                endpointBand);
        }
    }

    private bool TryConfigureFalloffBoundary(
        SourceMarker marker,
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

        ConfigureFalloffShell(marker, shellIndex, radiusMeters, band);
        shellIndex++;
        lastConfiguredRadius = radiusMeters;
        return true;
    }

    private void EnsureFalloffShellPool(SourceMarker marker)
    {
        if (marker == null || marker.root == null)
            return;

        if (marker.falloffShells == null)
            marker.falloffShells = new List<FalloffShellInfo>();

        int desiredCount = Mathf.Clamp(maxFalloffShells, 1, 3);
        while (marker.falloffShells.Count < desiredCount)
        {
            int shellIndex = marker.falloffShells.Count;
            GameObject shellObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shellObject.name = $"FalloffShell_{shellIndex + 1}";
            shellObject.layer = marker.root.layer;
            shellObject.transform.SetParent(marker.root.transform, false);
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
                Shader shader = GetDetectorTransparentShader();
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

    private void ConfigureFalloffShell(
        SourceMarker marker,
        int shellIndex,
        float radiusMeters,
        RadiationRiskBand band)
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

        float centerDiameter = Mathf.Max(0.001f, fixedMarkerSize);
        shell.root.transform.localPosition = Vector3.zero;
        shell.root.transform.localRotation = Quaternion.identity;
        shell.root.transform.localScale =
            Vector3.one * ((radiusMeters * 2f) / centerDiameter);

        Color color = GetRiskColorForBand(band);
        color.a = falloffShellAlpha;

        // Inner shells render after outer shells; the center sphere renders last.
        int queueOffset = Mathf.Clamp(maxFalloffShells, 1, 3) - shellIndex;
        shell.material =
            SetRendererTransparentColor(shell.renderer, color, queueOffset, true);
        shell.visualRequested = true;
    }

    private Color GetRiskColorForBand(RadiationRiskBand band)
    {
        switch (band)
        {
            case RadiationRiskBand.Red:
                return GetRiskColor(Mathf.Max(0f, dangerThresholdCps) + 1f);
            case RadiationRiskBand.Yellow:
                return GetRiskColor(Mathf.Max(0f, greenMaxCps) + 1f);
            default:
                return GetRiskColor(Mathf.Max(0f, greenMaxCps));
        }
    }

    private float GetLatestRadiationValue(string detectorId, float fallbackValue)
    {
        return IsRadiationSnapshotFresh() && latestAggregateRadiationValue >= 0f
            ? latestAggregateRadiationValue
            : fallbackValue;
    }

    private void HideFalloffShells(SourceMarker marker)
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
}
