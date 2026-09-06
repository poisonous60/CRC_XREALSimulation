using UnityEngine;

/// <summary>
/// Every number the marker's look needs, handed to each marker when it is created.
/// </summary>
public class MarkerVisualSettings
{
    public float markerAlpha = 0.18f;
    public float fixedMarkerSize = 0.2f;
    public bool forceDedicatedTransparentShader = true;
    public bool hasMarkerPrefab;

    public bool showGrayPreviewSphere = true;
    public Color previewSphereColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    public float previewSphereAlpha = 0.35f;

    public float controllerHoverHighlightBlend = 0.20f;
    public float controllerHoverAlphaBoost = 0.08f;
    public float controllerMoveHighlightBlend = 0.32f;
    public float controllerMoveAlphaBoost = 0.14f;

    public float hiddenMaxCps = 2f;
    public float greenMaxCps = 10f;
    public float dangerThresholdCps = 350f;

    public bool showFalloffShells = true;
    public float falloffReferenceDistanceMeters = 0.08f;
    public float falloffMaxRadiusMeters = 5f;
    public float falloffShellAlpha = 0.012f;
    public int maxFalloffShells = 3;

    public bool showLabel;
    public float labelFontSize = 4.5f;
    public float labelOutlineWidth = 0.2f;
    public Color labelOutlineColor = Color.black;
    public float labelWorldScale = 0.04f;
    public Vector3 labelCameraOffsetMeters = new Vector3(0.16f, -0.13f, -0.01f);
    public bool showDistanceInLabel = true;
    public bool showAnchorStateInLabel = true;

    public Camera headCamera;
}
