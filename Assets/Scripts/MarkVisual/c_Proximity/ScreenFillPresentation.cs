using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (c) proximity information as a threshold: inside the trigger radius the whole view takes the marker's color.
/// </summary>
[DisallowMultipleComponent]
public class ScreenFillPresentation : SourcePresentation
{
    [Header("Look")]
    [Tooltip("Trigger radius and opacity for this look. Empty draws nothing.")]
    [SerializeField] private ScreenFillConfig config;

    [Header("Placement")]
    [Tooltip("Distance from the head at which the fill canvas is parked. Keep it under the trigger radius so the fill sits in front of the marker it reports.")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 0.5f;

    [Tooltip("Draw order against the other world canvases. ARDetectorHud sits at 100, so anything under that leaves the HUD readable.")]
    [SerializeField] private int sortingOrder = 50;

    private Transform source;
    private Camera head;
    private SourceMarker owner;
    private Canvas fillCanvas;
    private RectTransform canvasRect;
    private Image fill;
    private float strength;

    public ScreenFillConfig Config => config;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;
        owner = GetComponentInParent<SourceMarker>();

        if (config == null)
        {
            Debug.LogWarning($"[ScreenFillPresentation] {name} has no config; nothing is drawn.");
            return;
        }

        EnsureCanvas();
    }

    private void EnsureCanvas()
    {
        if (fillCanvas != null || head == null)
            return;

        GameObject canvasObject = new GameObject("ScreenFillCanvas", typeof(Canvas));
        canvasObject.layer = 5;
        fillCanvas = canvasObject.GetComponent<Canvas>();
        fillCanvas.renderMode = RenderMode.WorldSpace;
        fillCanvas.worldCamera = head;

        // Without an explicit order the fill sorts by depth against the marker's own
        // transparent circle, which then draws over the very fill it triggered.
        fillCanvas.overrideSorting = true;
        fillCanvas.sortingOrder = sortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.localScale = Vector3.one;

        // RequireComponent is editor-only, so a script-built Graphic gets no CanvasRenderer.
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
        fillObject.layer = 5;
        fillObject.transform.SetParent(canvasObject.transform, false);

        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillObject.AddComponent<Image>();
        fill.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (config == null || source == null || head == null || fill == null || canvasRect == null)
            return;

        float distance = Vector3.Distance(head.transform.position, source.position);
        bool markerVisible = owner == null || owner.isVisible;
        float target = markerVisible && distance <= config.TriggerRadiusMeters ? 1f : 0f;

        strength = config.FadeSeconds > 0f
            ? Mathf.MoveTowards(strength, target, Time.deltaTime / config.FadeSeconds)
            : target;

        bool showing = strength > 0.001f;

        if (fill.enabled != showing)
            fill.enabled = showing;

        if (!showing)
            return;

        FollowHead();

        Color fillColor = GetFillColor();
        fillColor.a = config.FillAlpha * config.EvaluateFade(strength);
        fill.color = fillColor;
    }

    private Color GetFillColor()
    {
        if (config.FollowStatusBand &&
            owner != null &&
            MarkVisualConfig.TryLoad(out MarkVisualConfig visualConfig) &&
            visualConfig.TryGetStatusColor(owner.lastRadiationValue, out Color statusColor))
        {
            return statusColor;
        }

        return config.SolidColor;
    }

    private void FollowHead()
    {
        float distance = Mathf.Max(0.2f, canvasDistanceMeters);
        Vector3 center = head.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        float visibleHeight = HeadViewport.VisibleHeight(head, distance);

        canvasRect.position = center;
        canvasRect.rotation = head.transform.rotation;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = new Vector2(visibleHeight * HeadViewport.Aspect(head), visibleHeight);
    }

    private void OnDestroy()
    {
        if (fillCanvas != null)
            Destroy(fillCanvas.gameObject);
    }
}
