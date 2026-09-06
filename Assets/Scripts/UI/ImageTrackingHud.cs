using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Head-locked HUD for the image-tracking trial: marker distance, bearing, camera-relative offset, normal check.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImageTrackingHud : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    [Header("References")]
    [Tooltip("Optional. Resolved from this GameObject when empty.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    [Tooltip("Optional. Camera.main is used when empty.")]
    [SerializeField] private Camera targetCamera;

    [Header("Marker")]
    [Tooltip("Reference image name to report. Falls back to the first tracked image when no name matches.")]
    [SerializeField] private string markerName = "ROOM_ORIGIN";

    [Header("XREAL Viewport Layout")]
    [Tooltip("Distance of the head-locked canvas from the center eye in meters.")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 1.5f;

    [Tooltip("Normalized bottom-left anchor. 0.06/0.06 keeps text inside the XREAL display edge.")]
    [SerializeField] private Vector2 hudViewportAnchor = new Vector2(0.06f, 0.06f);

    [Tooltip("Text block size in canvas pixels on the 1920 x 1080 reference canvas.")]
    [SerializeField] private Vector2 hudPixelSize = new Vector2(1000f, 320f);

    [Tooltip("TextMeshPro font size on the reference canvas.")]
    [SerializeField, Min(10f)] private float hudFontSize = 30f;

    private readonly StringBuilder textBuilder = new StringBuilder(256);

    private RectTransform canvasRect;
    private Canvas canvas;
    private TMP_Text hudText;
    private float lastTrackingTime = -1f;

    private void Awake()
    {
        if (trackedImageManager == null)
            trackedImageManager = GetComponent<ARTrackedImageManager>();
        if (trackedImageManager == null)
            Debug.LogWarning($"[ImageTrackingHud] No ARTrackedImageManager on {gameObject.name}; the HUD stays idle.");
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null)
            return;

        EnsureCanvas();
        UpdateCanvasPoseFromRuntimeProjection();
        UpdateHudText();
    }

    private void OnDestroy()
    {
        if (canvasRect != null)
            Destroy(canvasRect.gameObject);
    }

    private bool TryGetMarkerImage(out ARTrackedImage image)
    {
        image = null;
        if (trackedImageManager == null || !trackedImageManager.isActiveAndEnabled)
            return false;

        foreach (ARTrackedImage candidate in trackedImageManager.trackables)
        {
            if (image == null)
                image = candidate;

            if (string.Equals(candidate.referenceImage.name, markerName, StringComparison.OrdinalIgnoreCase))
            {
                image = candidate;
                break;
            }
        }

        return image != null;
    }

    private void UpdateHudText()
    {
        textBuilder.Clear();

        if (!TryGetMarkerImage(out ARTrackedImage image))
        {
            textBuilder.Append(markerName).Append(": not detected\n");
            textBuilder.Append("Look at the wall marker from within 1.5x its width");
            hudText.text = textBuilder.ToString();
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        Vector3 imagePosition = image.transform.position;
        Vector3 local = cameraTransform.InverseTransformPoint(imagePosition);
        float distance = local.magnitude;
        float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float pitch = Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
        float facing = Vector3.Dot(image.transform.up, (cameraTransform.position - imagePosition).normalized);

        if (image.trackingState == TrackingState.Tracking)
            lastTrackingTime = Time.time;

        textBuilder.Append(image.referenceImage.name).Append("  ").Append(image.trackingState);
        if (lastTrackingTime >= 0f && image.trackingState != TrackingState.Tracking)
            textBuilder.Append($"  (last tracked {Time.time - lastTrackingTime:F1} s ago)");
        textBuilder.Append('\n');
        textBuilder.Append($"Distance {distance:F2} m\n");
        textBuilder.Append($"Yaw {yaw:+0.0;-0.0} deg {(yaw < 0f ? "<" : ">")}   Pitch {pitch:+0.0;-0.0} deg {(pitch < 0f ? "v" : "^")}\n");
        textBuilder.Append($"Offset right {local.x:+0.00;-0.00}  up {local.y:+0.00;-0.00}  forward {local.z:0.00} m\n");
        textBuilder.Append($"Normal facing viewer {facing:+0.00;-0.00}   Size {image.size.x * 100f:F1} x {image.size.y * 100f:F1} cm");
        hudText.text = textBuilder.ToString();
    }

    private void EnsureCanvas()
    {
        if (canvasRect != null)
            return;

        GameObject canvasObject = new GameObject(
            "ImageTrackingHUD_Runtime",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.layer = 5;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        canvas.worldCamera = targetCamera;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        hudText = CreateHudText(canvasRect);
    }

    private TMP_Text CreateHudText(RectTransform parent)
    {
        GameObject textObject = new GameObject(
            "ImageTrackingStatusText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = 5;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = hudViewportAnchor;
        rect.anchorMax = hudViewportAnchor;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = hudPixelSize;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.fontSize = hudFontSize;
        text.color = Color.white;
        text.richText = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;
        text.margin = new Vector4(8f, 8f, 8f, 8f);
        return text;
    }

    private void UpdateCanvasPoseFromRuntimeProjection()
    {
        float distance = Mathf.Max(0.2f, canvasDistanceMeters);
        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
        Vector3 center = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

        float visibleWorldHeight = Vector3.Distance(bottomLeft, topLeft);
        float uniformScale = visibleWorldHeight / ReferenceHeight;

        canvasRect.position = center;
        canvasRect.rotation = targetCamera.transform.rotation;
        canvasRect.localScale = Vector3.one * uniformScale;

        if (canvas.worldCamera != targetCamera)
            canvas.worldCamera = targetCamera;
    }
}
