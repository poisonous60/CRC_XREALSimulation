using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    private MarkerVisualSettings visualSettings;

    private MarkerVisualSettings VisualSettings
    {
        get
        {
            if (visualSettings == null)
                visualSettings = new MarkerVisualSettings();

            visualSettings.markerAlpha = markerAlpha;
            visualSettings.fixedMarkerSize = fixedMarkerSize;
            visualSettings.forceDedicatedTransparentShader = forceDedicatedTransparentShader;
            visualSettings.hasMarkerPrefab = HasMarkerPrefab();
            visualSettings.showGrayPreviewSphere = showGrayPreviewSphere;
            visualSettings.previewSphereColor = previewSphereColor;
            visualSettings.previewSphereAlpha = previewSphereAlpha;
            visualSettings.controllerHoverHighlightBlend = controllerHoverHighlightBlend;
            visualSettings.controllerHoverAlphaBoost = controllerHoverAlphaBoost;
            visualSettings.controllerMoveHighlightBlend = controllerMoveHighlightBlend;
            visualSettings.controllerMoveAlphaBoost = controllerMoveAlphaBoost;
            visualSettings.hiddenMaxCps = hiddenMaxCps;
            visualSettings.greenMaxCps = greenMaxCps;
            visualSettings.dangerThresholdCps = dangerThresholdCps;
            visualSettings.showFalloffShells = showFalloffShells;
            visualSettings.falloffReferenceDistanceMeters = falloffReferenceDistanceMeters;
            visualSettings.falloffMaxRadiusMeters = falloffMaxRadiusMeters;
            visualSettings.falloffShellAlpha = falloffShellAlpha;
            visualSettings.maxFalloffShells = maxFalloffShells;
            visualSettings.showLabel = showLabel;
            visualSettings.labelFontSize = labelFontSize;
            visualSettings.labelOutlineWidth = labelOutlineWidth;
            visualSettings.labelOutlineColor = labelOutlineColor;
            visualSettings.labelWorldScale = labelWorldScale;
            visualSettings.labelCameraOffsetMeters = labelCameraOffsetMeters;
            visualSettings.showDistanceInLabel = showDistanceInLabel;
            visualSettings.showAnchorStateInLabel = showAnchorStateInLabel;
            visualSettings.headCamera = fallbackCamera;
            return visualSettings;
        }
    }

    private SourceMarker CreateOrMoveMarker(
        string detectorId,
        Vector3 worldPosition,
        float estimatedDistance,
        float qrPixelSize,
        Transform parent,
        bool forceMoveExisting = false)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (string.IsNullOrEmpty(detectorId))
            return null;

        if (markers.TryGetValue(detectorId, out SourceMarker existing))
        {
            if (existing == null || existing.root == null)
            {
                if (existing != null)
                    DestroyMarkerVisualResources(existing);
                markers.Remove(detectorId);
            }
            else
            {
                if (!updateExistingMarkerOnRescan && !forceMoveExisting)
                    return existing;

                Vector3 finalPosition = worldPosition;
                if (!forceMoveExisting &&
                    smoothPositionOnRescan &&
                    existing.anchor == null &&
                    !existing.isFollowingPlacementOrigin)
                {
                    finalPosition = Vector3.Lerp(existing.savedPosition, worldPosition, rescanPositionBlend);
                }

                existing.root.transform.SetParent(parent != null && parentMarkerToAnchor ? parent : transform, true);
                existing.root.transform.position = finalPosition;
                existing.root.transform.localScale = Vector3.one * fixedMarkerSize;
                existing.savedPosition = finalPosition;

                if (estimatedDistance > 0f)
                    existing.lastEstimatedDistance = estimatedDistance;
                if (qrPixelSize > 0f)
                    existing.lastQrPixelSize = qrPixelSize;

                ForceMarkerVisible(existing);
                UpdateMarkerVisual(existing, existing.lastRadiationValue);
                return existing;
            }
        }

        Transform markerParent = parent != null && parentMarkerToAnchor ? parent : transform;

        MarkVisualSetting.TryLoad(out MarkVisualSetting visualSetting);

        GameObject prefab = markerPrefab;

        if (prefab == null && visualSetting != null)
            prefab = visualSetting.MarkerPrefab;

        GameObject root = prefab != null
            ? Instantiate(prefab, worldPosition, Quaternion.identity, markerParent)
            : CreateDefaultSphere(worldPosition, markerParent);

        root.name = $"DetectorMarker_{detectorId}";

        if (visualSetting != null && visualSetting.ProximityPrefab != null)
            root.AddComponent<SourcePresentationHost>();

        root.transform.position = worldPosition;
        root.transform.localScale = Vector3.one * fixedMarkerSize;

        Renderer renderer = root.GetComponentInChildren<Renderer>();

        SourceMarker info = root.AddComponent<SourceMarker>();
        info.detectorId = detectorId;
        info.renderer = renderer;

        // Captured before the label and the shells exist, so hiding never fights their owners.
        info.CaptureBodyRenderers(root);
        info.savedPosition = worldPosition;
        info.lastRadiationValue = GetLatestRadiationValue(detectorId, -1f);
        info.lastEstimatedDistance = estimatedDistance;
        info.lastQrPixelSize = qrPixelSize;
        info.lastPlacementImagePoint = new Vector2(0.5f, 0.5f);
        info.lastImageWidth = 1;
        info.lastImageHeight = 1;
        info.lastPlacementMethod = "preview projection";
        info.isFollowingPlacementOrigin = false;
        info.isPlaced = false;
        info.centerVisualRequested = true;
        info.anchorState = useSpatialAnchors ? "no anchor yet" : "preview projection";

        info.visual = root.AddComponent<SourceMarkerVisual>();
        info.visual.Initialize(info, VisualSettings);
        info.visual.EnsureLabel();

        markers.Add(detectorId, info);
        ForceMarkerVisible(info);
        UpdateMarkerVisual(info, info.lastRadiationValue);
        return info;
    }

    private bool HasMarkerPrefab()
    {
        if (markerPrefab != null)
            return true;

        return MarkVisualSetting.TryLoad(out MarkVisualSetting visualSetting)
            && visualSetting.MarkerPrefab != null;
    }

    private GameObject CreateDefaultSphere(Vector3 position, Transform parent)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent != null ? parent : transform, true);
        sphere.transform.position = position;

        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = MarkerMaterials.GetDetectorTransparentShader();
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader != null)
                renderer.material = new Material(shader);

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        Collider collider = sphere.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
            Destroy(collider);
        }

        return sphere;
    }

    private void UpdateMarkerVisual(SourceMarker marker, float radiationValue)
    {
        if (marker == null || marker.root == null)
            return;

        marker.lastRadiationValue = radiationValue;

        if (marker.visual != null)
        {
            marker.visual.SetSettings(VisualSettings);
            marker.visual.Refresh(radiationValue, IsPreviewMarker(marker));
        }

        // Radiation value affects visibility/color only. Center size stays fixed.
        marker.root.transform.localScale = Vector3.one * fixedMarkerSize;

        bool waitingForPlaneHit =
            usePlaneIntersectionPlacement &&
            marker.isFollowingPlacementOrigin &&
            !marker.hasValidPlaneHit;

        if (waitingForPlaneHit && hidePreviewWithoutPlaneHit)
            SetMarkerRequestedVisibility(marker, false);
        else
            ForceMarkerVisible(marker);
    }

    private void UpdateLabel(SourceMarker marker, float radiationValue, bool moved)
    {
        if (marker == null || marker.visual == null)
            return;

        marker.visual.SetSettings(VisualSettings);
        marker.visual.UpdateLabel(radiationValue, moved);
    }

    private void UpdateLabelTransform(SourceMarker marker)
    {
        if (marker == null || marker.visual == null)
            return;

        marker.visual.SetSettings(VisualSettings);
        marker.visual.UpdateLabelTransform();
    }

    private void DestroyMarkerVisualResources(SourceMarker marker)
    {
        if (marker == null || marker.visual == null)
            return;

        marker.visual.DestroyResources();
    }

    private Color GetRiskColor(float radiationValue)
    {
        return MarkerRisk.GetColor(radiationValue, VisualSettings);
    }

    private RadiationRiskBand GetRiskBand(float radiationValue)
    {
        return MarkerRisk.GetBand(radiationValue, VisualSettings);
    }
}
