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
    private const float MoveFadeMinMeters = 0.05f;

    private Color displayedColor = Color.clear;
    private Color fadeFromColor = Color.clear;
    private Color targetColor = Color.clear;
    private float colorFadeProgress = 1f;
    private float appearFadeProgress = 1f;
    private Renderer ghostRenderer;
    private Material ghostMaterial;
    private Color ghostColor = Color.clear;
    private MaterialPropertyBlock ghostPropertyBlock;
    private float ghostProgress;
    private int ghostFrame;
    private bool wasCenterDrawn;
    private bool hasDisplayedColor;

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

        SetTargetColor(color);

        // Keep the logical marker root alive so HUD distance, gaze selection,
        // Cancel, and anchor state continue to work even when Hide Low Cps hides
        // the center sphere at 0-2 CPS. An unknown preview stays visible for
        // placement; an already placed detector with no valid reading stays
        // visually quiet. A gray preview is an aiming aid, so it ignores the CPS
        // bands entirely and stays visible even at a hidden 0-2 CPS reading.
        bool hideLowCps =
            !MarkVisualConfig.TryLoad(out MarkVisualConfig config) || config.HideLowCps;

        marker.centerVisualRequested =
            useGrayPreview ||
            marker.isControllerMoving ||
            ((riskBand != RadiationRiskBand.Hidden || !hideLowCps) &&
             (riskBand != RadiationRiskBand.Unknown || !marker.isPlaced));

        if (shells != null)
            shells.Refresh(radiationValue, riskBand);

        UpdateLabel(radiationValue, false);
        return riskBand;
    }

    // Refresh runs on readings and drags, not every frame, so the crossfade needs its own tick.
    private void LateUpdate()
    {
        bool appearFadeMoved = TickAppearFade();
        UpdateGhost();

        if (colorFadeProgress >= 1f)
        {
            if (appearFadeMoved && hasDisplayedColor)
                ApplyColor(displayedColor);

            return;
        }

        float fadeSeconds = GetColorFadeSeconds();

        colorFadeProgress = fadeSeconds > 0f
            ? Mathf.MoveTowards(colorFadeProgress, 1f, Time.deltaTime / fadeSeconds)
            : 1f;

        ApplyColor(Color.Lerp(fadeFromColor, targetColor, EvaluateColorFade(colorFadeProgress)));
    }

    private void SetTargetColor(Color color)
    {
        float fadeSeconds = GetColorFadeSeconds();

        if (!hasDisplayedColor || fadeSeconds <= 0f)
        {
            fadeFromColor = color;
            targetColor = color;
            colorFadeProgress = 1f;
            ApplyColor(color);
            return;
        }

        if (targetColor == color)
            return;

        fadeFromColor = displayedColor;
        targetColor = color;
        colorFadeProgress = 0f;
    }

    private void ApplyColor(Color color)
    {
        if (marker == null)
            return;

        displayedColor = color;
        hasDisplayedColor = true;

        // The stored color stays at full opacity so a band change mid fade-in keeps
        // crossfading between the two real colors.
        Color drawnColor = color;
        drawnColor.a *= EvaluateAppearFade(appearFadeProgress);

        marker.centerMaterial =
            MarkerMaterials.SetRendererTransparentColor(marker.renderer, drawnColor, 10, false, settings);
    }

    public bool Hide()
    {
        return marker == null ||
               marker.renderer == null ||
               !marker.renderer.enabled ||
               GetAppearFadeSeconds() <= 0f ||
               appearFadeProgress <= 0f;
    }

    // Everything outside the look reads the root, so the root moves at once and a copy of the
    // visual child is left behind to fade out on the spot the marker is leaving.
    public void MoveTo(Vector3 worldPosition)
    {
        bool fadeMove = CanFadeMove(worldPosition);

        // A second move in the same frame must not trade the ghost for a copy taken from a
        // position that was never on screen.
        if (fadeMove && (ghostRenderer == null || ghostFrame != Time.frameCount))
        {
            DestroyGhost();
            SpawnGhost();
        }

        transform.position = worldPosition;

        if (fadeMove)
            appearFadeProgress = 0f;
    }

    private void SpawnGhost()
    {
        GameObject copy = Instantiate(marker.renderer.gameObject, transform);
        copy.name = $"MoveGhost_{marker.detectorId}";
        copy.transform.SetParent(transform.parent, true);

        ghostRenderer = copy.GetComponent<Renderer>();

        if (ghostRenderer == null)
        {
            Destroy(copy);
            return;
        }

        // The ghost is a still picture of what was drawn, so it keeps only what draws it.
        // Colliders would answer the controller ray on a spot the marker already left, and a
        // look script would keep running on a copy that no longer belongs to any marker.
        // FaceCamera survives because a billboard that stopped turning reads as a skewed card.
        Collider[] colliders = copy.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);

        MonoBehaviour[] behaviours = copy.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (!(behaviours[i] is FaceCamera))
                Destroy(behaviours[i]);
        }

        // Instantiate does not carry the property block, so a look that drives its shader
        // through one would draw the ghost with default values instead of what was on screen.
        if (ghostPropertyBlock == null)
            ghostPropertyBlock = new MaterialPropertyBlock();

        marker.renderer.GetPropertyBlock(ghostPropertyBlock);
        ghostRenderer.SetPropertyBlock(ghostPropertyBlock);

        ghostMaterial = ghostRenderer.material;
        ghostColor = displayedColor;
        ghostColor.a *= EvaluateAppearFade(appearFadeProgress);
        ghostProgress = 1f;
        ghostFrame = Time.frameCount;
    }

    private void UpdateGhost()
    {
        if (ghostRenderer == null)
            return;

        float fadeSeconds = GetAppearFadeSeconds();

        ghostProgress = fadeSeconds > 0f
            ? Mathf.MoveTowards(ghostProgress, 0f, Time.deltaTime / fadeSeconds)
            : 0f;

        if (ghostProgress <= 0f)
        {
            DestroyGhost();
            return;
        }

        Color color = ghostColor;
        color.a *= EvaluateAppearFade(ghostProgress);
        MarkerMaterials.SetRendererTransparentColor(ghostRenderer, color, 10, false, settings);
    }

    // The ghost is parented outside the marker root so the root can move away from it, which
    // also means it outlives the marker unless it is taken down here.
    private void OnDestroy()
    {
        DestroyGhost();
    }

    private void OnDisable()
    {
        DestroyGhost();
    }

    private void DestroyGhost()
    {
        if (ghostMaterial != null)
            Destroy(ghostMaterial);

        if (ghostRenderer != null)
            Destroy(ghostRenderer.gameObject);

        ghostMaterial = null;
        ghostRenderer = null;
    }

    private bool CanFadeMove(Vector3 worldPosition)
    {
        return marker != null &&
               marker.renderer != null &&
               marker.renderer.transform != transform &&
               marker.renderer.enabled &&
               appearFadeProgress > 0f &&
               marker.isPlaced &&
               !marker.isControllerMoving &&
               GetAppearFadeSeconds() > 0f &&
               (worldPosition - transform.position).sqrMagnitude >=
                   MoveFadeMinMeters * MoveFadeMinMeters;
    }

    // The visibility policy switches the renderer on the frame the marker appears, so the
    // fade-in starts from that edge instead of from a call the manager would have to make.
    private bool TickAppearFade()
    {
        bool centerDrawn = marker != null && marker.renderer != null && marker.renderer.enabled;

        if (centerDrawn != wasCenterDrawn)
        {
            wasCenterDrawn = centerDrawn;
            appearFadeProgress = centerDrawn && GetAppearFadeSeconds() > 0f ? 0f : 1f;
            return true;
        }

        if (!centerDrawn)
            return false;

        float target = marker.centerDrawRequested ? 1f : 0f;

        if (Mathf.Approximately(appearFadeProgress, target))
        {
            if (target <= 0f)
            {
                marker.renderer.enabled = false;
                wasCenterDrawn = false;
            }

            return false;
        }

        float fadeSeconds = GetAppearFadeSeconds();

        appearFadeProgress = fadeSeconds > 0f
            ? Mathf.MoveTowards(appearFadeProgress, target, Time.deltaTime / fadeSeconds)
            : target;

        return true;
    }

    private static float GetAppearFadeSeconds()
    {
        return MarkVisualConfig.TryLoad(out MarkVisualConfig config) ? config.AppearFadeSeconds : 0f;
    }

    private static float EvaluateAppearFade(float progress)
    {
        return MarkVisualConfig.TryLoad(out MarkVisualConfig config)
            ? config.EvaluateAppearFade(progress)
            : Mathf.Clamp01(progress);
    }

    private static float GetColorFadeSeconds()
    {
        return MarkVisualConfig.TryLoad(out MarkVisualConfig config) ? config.StatusColorFadeSeconds : 0f;
    }

    private static float EvaluateColorFade(float progress)
    {
        return MarkVisualConfig.TryLoad(out MarkVisualConfig config)
            ? config.EvaluateStatusColorFade(progress)
            : Mathf.Clamp01(progress);
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
        DestroyGhost();

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
