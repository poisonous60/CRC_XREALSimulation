using TMPro;
using UnityEngine;

/// <summary>
/// How one marker looks: center color, transparent material, label, falloff shells.
/// </summary>
[DisallowMultipleComponent]
public class SourceMarkerVisual : MonoBehaviour
{
    private SourceMarker marker;
    private MarkerVisualSettings settings;
    private FalloffShellVisual shells;
    private MarkerAlphaOverride alphaOverride;

    public void Initialize(SourceMarker owner, MarkerVisualSettings visualSettings)
    {
        marker = owner;
        settings = visualSettings;
        alphaOverride = GetComponentInChildren<MarkerAlphaOverride>(true);

        if (shells == null)
            shells = gameObject.AddComponent<FalloffShellVisual>();

        shells.Initialize(owner, visualSettings);
    }

    public void SetSettings(MarkerVisualSettings visualSettings)
    {
        settings = visualSettings;

        if (shells != null)
            shells.Initialize(marker, visualSettings);
    }

    public void EnsureLabel()
    {
        if (marker == null || settings == null || !settings.showLabel || marker.label != null)
            return;

        GameObject labelObject = new GameObject($"Label_{marker.detectorId}");
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = Vector3.zero;
        labelObject.transform.localRotation = Quaternion.identity;

        TMP_Text label = labelObject.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = settings.labelFontSize;
        label.text = marker.detectorId;
        label.color = Color.white;
        label.outlineWidth = settings.labelOutlineWidth;
        label.outlineColor = settings.labelOutlineColor;

        marker.label = label;
        SetLabelWorldScale(label.transform);
    }

    public RadiationRiskBand Refresh(float radiationValue, bool isPreview)
    {
        if (marker == null || settings == null)
            return RadiationRiskBand.Unknown;

        RadiationRiskBand riskBand = MarkerRisk.GetBand(radiationValue, settings);
        bool useGrayPreview = settings.showGrayPreviewSphere && isPreview;

        Color color = useGrayPreview
            ? settings.previewSphereColor
            : MarkerRisk.GetColor(radiationValue, settings);
        color.a = useGrayPreview
            ? Mathf.Clamp01(settings.previewSphereAlpha)
            : (alphaOverride != null ? alphaOverride.MarkerAlpha : settings.markerAlpha);

        // Highlight only the center material. Scaling the marker root would also
        // scale every inverse-square falloff shell and falsify its real-world radius.
        if (marker.isControllerMoving)
        {
            float originalAlpha = color.a;
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(settings.controllerMoveHighlightBlend));
            color.a = Mathf.Clamp01(originalAlpha + settings.controllerMoveAlphaBoost);
        }
        else if (marker.isControllerHovered)
        {
            float originalAlpha = color.a;
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(settings.controllerHoverHighlightBlend));
            color.a = Mathf.Clamp01(originalAlpha + settings.controllerHoverAlphaBoost);
        }

        marker.centerMaterial =
            MarkerMaterials.SetRendererTransparentColor(marker.renderer, color, 10, false, settings);

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

        if (shells != null)
            shells.Refresh(radiationValue, riskBand);

        UpdateLabel(radiationValue, false);
        return riskBand;
    }

    public void UpdateLabel(float radiationValue, bool moved)
    {
        if (marker == null || marker.label == null || settings == null)
            return;

        string text = marker.detectorId;

        if (radiationValue >= 0f)
            text += $"\n{radiationValue:F3}";

        if (settings.showDistanceInLabel && marker.lastEstimatedDistance > 0f)
            text += $"\n{marker.lastEstimatedDistance:F2}m";

        if (settings.showAnchorStateInLabel && !string.IsNullOrEmpty(marker.anchorState))
            text += $"\n{marker.anchorState}";

        if (moved)
            text += "\nposition updated";

        marker.label.text = text;
    }

    public void UpdateLabelTransform()
    {
        if (marker == null || marker.label == null || settings == null || settings.headCamera == null)
            return;

        Transform cameraTransform = settings.headCamera.transform;
        Transform labelTransform = marker.label.transform;

        labelTransform.position =
            transform.position +
            cameraTransform.right * settings.labelCameraOffsetMeters.x +
            cameraTransform.up * settings.labelCameraOffsetMeters.y +
            cameraTransform.forward * settings.labelCameraOffsetMeters.z;

        labelTransform.rotation = cameraTransform.rotation;
        SetLabelWorldScale(labelTransform);
    }

    public void DestroyResources()
    {
        if (marker != null && marker.centerMaterial != null)
        {
            Destroy(marker.centerMaterial);
            marker.centerMaterial = null;
        }

        if (shells != null)
            shells.DestroyMaterials();
    }

    private void SetLabelWorldScale(Transform labelTransform)
    {
        if (labelTransform == null || settings == null)
            return;

        Transform parent = labelTransform.parent;
        if (parent == null)
        {
            labelTransform.localScale = Vector3.one * settings.labelWorldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        labelTransform.localScale = new Vector3(
            SafeScaleDivision(settings.labelWorldScale, parentScale.x),
            SafeScaleDivision(settings.labelWorldScale, parentScale.y),
            SafeScaleDivision(settings.labelWorldScale, parentScale.z)
        );
    }

    private float SafeScaleDivision(float desiredWorldScale, float parentScale)
    {
        return Mathf.Abs(parentScale) > 0.0001f
            ? desiredWorldScale / Mathf.Abs(parentScale)
            : desiredWorldScale;
    }
}
