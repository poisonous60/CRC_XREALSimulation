using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private MarkerInfo CreateOrMoveMarker(
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

        if (markers.TryGetValue(detectorId, out MarkerInfo existing))
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

        MarkVisualSetting.TryLoad(out MarkVisualSetting presentationConfig);

        GameObject prefab = markerPrefab;

        if (prefab == null && presentationConfig != null)
            prefab = presentationConfig.MarkerPrefab;

        GameObject root = prefab != null
            ? Instantiate(prefab, worldPosition, Quaternion.identity, markerParent)
            : CreateDefaultSphere(worldPosition, markerParent);

        root.name = $"DetectorMarker_{detectorId}";

        if (presentationConfig != null && presentationConfig.ProximityPrefab != null)
            root.AddComponent<SourcePresentationHost>();
        root.transform.position = worldPosition;
        root.transform.localScale = Vector3.one * fixedMarkerSize;

        Renderer renderer = root.GetComponentInChildren<Renderer>();
        TMP_Text label = null;

        if (showLabel)
            label = CreateLabel(root.transform, detectorId);

        MarkerInfo info = new MarkerInfo
        {
            detectorId = detectorId,
            root = root,
            renderer = renderer,
            label = label,
            savedPosition = worldPosition,
            lastRadiationValue = GetLatestRadiationValue(detectorId, -1f),
            lastEstimatedDistance = estimatedDistance,
            lastQrPixelSize = qrPixelSize,
            lastPlacementImagePoint = new Vector2(0.5f, 0.5f),
            lastImageWidth = 1,
            lastImageHeight = 1,
            lastPlacementMethod = "preview projection",
            isFollowingPlacementOrigin = false,
            isPlaced = false,
            centerVisualRequested = true,
            anchorState = useSpatialAnchors ? "no anchor yet" : "preview projection"
        };

        markers.Add(detectorId, info);
        ForceMarkerVisible(info);
        UpdateMarkerVisual(info, info.lastRadiationValue);
        return info;
    }

    private bool HasMarkerPrefab()
    {
        if (markerPrefab != null)
            return true;

        return MarkVisualSetting.TryLoad(out MarkVisualSetting presentationConfig)
            && presentationConfig.MarkerPrefab != null;
    }

    private GameObject CreateDefaultSphere(Vector3 position, Transform parent)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(parent != null ? parent : transform, true);
        sphere.transform.position = position;

        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = GetDetectorTransparentShader();
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

    private TMP_Text CreateLabel(Transform parent, string detectorId)
    {
        GameObject labelObject = new GameObject($"Label_{detectorId}");
        labelObject.transform.SetParent(parent, false);
        labelObject.transform.localPosition = Vector3.zero;
        labelObject.transform.localRotation = Quaternion.identity;

        TMP_Text label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = labelFontSize;
        label.text = detectorId;
        label.color = Color.white;
        label.outlineWidth = labelOutlineWidth;
        label.outlineColor = labelOutlineColor;

        SetLabelWorldScale(label.transform);

        return label;
    }

    private void UpdateLabelTransform(MarkerInfo marker)
    {
        if (marker == null || marker.root == null || marker.label == null || fallbackCamera == null)
            return;

        Transform cameraTransform = fallbackCamera.transform;
        Transform labelTransform = marker.label.transform;

        labelTransform.position =
            marker.root.transform.position +
            cameraTransform.right * labelCameraOffsetMeters.x +
            cameraTransform.up * labelCameraOffsetMeters.y +
            cameraTransform.forward * labelCameraOffsetMeters.z;

        labelTransform.rotation = cameraTransform.rotation;
        SetLabelWorldScale(labelTransform);
    }

    private void SetLabelWorldScale(Transform labelTransform)
    {
        if (labelTransform == null)
            return;

        Transform parent = labelTransform.parent;
        if (parent == null)
        {
            labelTransform.localScale = Vector3.one * labelWorldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        labelTransform.localScale = new Vector3(
            SafeScaleDivision(labelWorldScale, parentScale.x),
            SafeScaleDivision(labelWorldScale, parentScale.y),
            SafeScaleDivision(labelWorldScale, parentScale.z)
        );
    }

    private float SafeScaleDivision(float desiredWorldScale, float parentScale)
    {
        return Mathf.Abs(parentScale) > 0.0001f
            ? desiredWorldScale / Mathf.Abs(parentScale)
            : desiredWorldScale;
    }

    private void UpdateMarkerVisual(MarkerInfo marker, float radiationValue)
    {
        if (marker == null || marker.root == null)
            return;

        marker.lastRadiationValue = radiationValue;

        RadiationRiskBand riskBand = GetRiskBand(radiationValue);
        bool useGrayPreview = showGrayPreviewSphere && IsPreviewMarker(marker);

        Color color = useGrayPreview ? previewSphereColor : GetRiskColor(radiationValue);
        color.a = useGrayPreview ? Mathf.Clamp01(previewSphereAlpha) : markerAlpha;

        // Highlight only the center material. Scaling the marker root would also
        // scale every inverse-square falloff shell and falsify its real-world radius.
        if (marker.isControllerMoving)
        {
            float originalAlpha = color.a;
            color = Color.Lerp(
                color,
                Color.white,
                Mathf.Clamp01(controllerMoveHighlightBlend));
            color.a = Mathf.Clamp01(originalAlpha + controllerMoveAlphaBoost);
        }
        else if (marker.isControllerHovered)
        {
            float originalAlpha = color.a;
            color = Color.Lerp(
                color,
                Color.white,
                Mathf.Clamp01(controllerHoverHighlightBlend));
            color.a = Mathf.Clamp01(originalAlpha + controllerHoverAlphaBoost);
        }

        marker.centerMaterial =
            SetRendererTransparentColor(marker.renderer, color, 10, false);

        // Keep the logical marker root alive so HUD distance, gaze selection,
        // Cancel, and anchor state continue to work even when 0-2 CPS hides the
        // center sphere. An unknown preview stays visible for placement; an
        // already placed detector with no valid reading stays visually quiet.
        // A gray preview is an aiming aid, so it ignores the CPS bands entirely
        // and stays visible even at a hidden 0-2 CPS reading.
        marker.centerVisualRequested =
            useGrayPreview ||
            marker.isControllerMoving ||
            (riskBand != RadiationRiskBand.Hidden &&
             (riskBand != RadiationRiskBand.Unknown || !marker.isPlaced));

        UpdateFalloffShellVisuals(marker, radiationValue, riskBand);

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

        UpdateLabel(marker, radiationValue, false);
    }

    private Color GetRiskColor(float radiationValue)
    {
        if (MarkVisualSetting.TryLoad(out MarkVisualSetting presentationConfig) &&
            presentationConfig.TryGetStatusColor(radiationValue, out Color bandColor))
        {
            return ToMarkerColor(bandColor);
        }

        switch (GetRiskBand(radiationValue))
        {
            case RadiationRiskBand.Green:
            case RadiationRiskBand.Hidden:
                return ToMarkerColor(new Color(0.0f, 1.0f, 0.0f));
            case RadiationRiskBand.Yellow:
                return ToMarkerColor(new Color(1.0f, 1.0f, 0.0f));
            case RadiationRiskBand.Red:
                return ToMarkerColor(new Color(1.0f, 0.0f, 0.0f));
            default:
                return ToMarkerColor(new Color(0.65f, 0.65f, 0.65f));
        }
    }

    private Color ToMarkerColor(Color rgb)
    {
        return new Color(rgb.r, rgb.g, rgb.b, markerAlpha);
    }

    private RadiationRiskBand GetRiskBand(float radiationValue)
    {
        if (float.IsNaN(radiationValue) ||
            float.IsInfinity(radiationValue) ||
            radiationValue < 0f)
        {
            return RadiationRiskBand.Unknown;
        }

        float hiddenThreshold = Mathf.Max(0f, hiddenMaxCps);
        float greenThreshold = Mathf.Max(hiddenThreshold, greenMaxCps);
        float redThreshold = Mathf.Max(greenThreshold, dangerThresholdCps);

        if (radiationValue <= hiddenThreshold)
            return RadiationRiskBand.Hidden;
        if (radiationValue <= greenThreshold)
            return RadiationRiskBand.Green;
        if (radiationValue <= redThreshold)
            return RadiationRiskBand.Yellow;
        return RadiationRiskBand.Red;
    }

    private Material SetRendererTransparentColor(
        Renderer renderer,
        Color color,
        int renderQueueOffset,
        bool renderInside)
    {
        if (renderer == null || renderer.material == null)
            return null;

        Material material = renderer.material;

        // A supplied marker prefab carries its own shader, so the forced swap would undo it.
        if (forceDedicatedTransparentShader && !HasMarkerPrefab())
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

    private void DestroyMarkerVisualResources(MarkerInfo marker)
    {
        if (marker == null)
            return;

        if (marker.centerMaterial != null)
        {
            Destroy(marker.centerMaterial);
            marker.centerMaterial = null;
        }

        if (marker.falloffShells == null)
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

    private void ConfigureTransparentMaterial(Material material)
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

    private Shader GetDetectorTransparentShader()
    {
        if (cachedDetectorTransparentShader == null)
            cachedDetectorTransparentShader = Resources.Load<Shader>("RadVisDetectorTransparent");

        if (cachedDetectorTransparentShader == null)
            cachedDetectorTransparentShader = Shader.Find("RadVis/DetectorTransparent");

        return cachedDetectorTransparentShader;
    }

    private void UpdateLabel(MarkerInfo marker, float radiationValue, bool moved)
    {
        if (marker == null || marker.label == null)
            return;

        string text = marker.detectorId;

        if (radiationValue >= 0f)
            text += $"\n{radiationValue:F3}";

        if (showDistanceInLabel && marker.lastEstimatedDistance > 0f)
            text += $"\n{marker.lastEstimatedDistance:F2}m";

        if (showAnchorStateInLabel && !string.IsNullOrEmpty(marker.anchorState))
            text += $"\n{marker.anchorState}";

        if (moved)
            text += "\nposition updated";

        marker.label.text = text;
    }
}
