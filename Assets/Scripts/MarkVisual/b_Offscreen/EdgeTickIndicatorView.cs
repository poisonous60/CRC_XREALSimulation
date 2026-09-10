using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (b) off-screen cue: a plain bar on the edge the source left by, turned so its long axis follows that edge.
/// </summary>
[DisallowMultipleComponent]
public class EdgeTickIndicatorView : OffscreenIndicatorView
{
    [Header("Look")]
    [Tooltip("Shape and edge offset for this look. Empty keeps the bar at its authored rect.")]
    [SerializeField] private EdgeTickConfig config;

    private Image tick;
    private RectTransform tickRect;
    private OffscreenEdge currentEdge = OffscreenEdge.Left;
    private bool useStatusColor = true;
    private Color configuredColor = Color.white;
    private Color baseColor = Color.white;
    private float fadeProgress;
    private bool showing;
    private OffscreenEdge pendingEdge;
    private bool edgeChangePending;
    private Vector2 heldAnchor;

    public EdgeTickConfig Config => config;

    // The tick reports a state, not a bearing, so it never slides along its edge.
    public override bool UsesFixedEdgeCenter => true;

    private void Awake()
    {
        tick = GetComponentInChildren<Image>(true);

        if (MarkVisualConfig.TryLoad(out MarkVisualConfig visualConfig))
        {
            useStatusColor = visualConfig.OffscreenUseStatusColor;
            configuredColor = visualConfig.OffscreenColor;
        }

        if (tick == null)
        {
            Debug.LogWarning($"[EdgeTickIndicatorView] {name} found no Image child.");
            return;
        }

        tick.raycastTarget = false;
        tickRect = tick.rectTransform;

        if (config != null)
            ((RectTransform)transform).sizeDelta = new Vector2(config.Length, config.Length);
    }

    // A pooled tick that was deactivated fades in again from nothing.
    private void OnEnable()
    {
        fadeProgress = 0f;
        showing = false;
        edgeChangePending = false;
    }

    public override void Show(string detectorId, OffscreenEdge edge, Color statusColor)
    {
        if (tick == null)
            return;

        baseColor = useStatusColor ? statusColor : configuredColor;

        if (config != null)
        {
            tickRect.sizeDelta = new Vector2(config.Length, config.Thickness);

            // The placer only overwrites the root anchor after this call, so the anchor read
            // here is still the edge the tick has to fade out on.
            if (!edgeChangePending && showing && edge != currentEdge &&
                config.FadeOnEdgeChange && config.FadeSeconds > 0f)
            {
                heldAnchor = ((RectTransform)transform).anchorMin;
                edgeChangePending = true;
            }
        }

        if (edgeChangePending)
        {
            pendingEdge = edge;
            showing = false;
        }
        else
        {
            ApplyEdge(edge);
            showing = true;
        }

        ApplyFadedColor();
    }

    public override bool Hide()
    {
        showing = false;
        edgeChangePending = false;

        return config == null || config.FadeSeconds <= 0f || fadeProgress <= 0f;
    }

    // The placer anchors this root after calling Show, so the offset has to wait until
    // the frame's placement is already in.
    private void LateUpdate()
    {
        if (tickRect == null || config == null)
            return;

        if (edgeChangePending)
        {
            RectTransform root = (RectTransform)transform;
            root.anchorMin = heldAnchor;
            root.anchorMax = heldAnchor;
            root.anchoredPosition = Vector2.zero;
        }

        tickRect.anchoredPosition = GetOutwardDirection(currentEdge) * config.EdgeOffset;

        float target = showing ? 1f : 0f;

        if (!Mathf.Approximately(fadeProgress, target))
        {
            fadeProgress = config.FadeSeconds > 0f
                ? Mathf.MoveTowards(fadeProgress, target, Time.deltaTime / config.FadeSeconds)
                : target;

            ApplyFadedColor();
        }

        if (edgeChangePending && fadeProgress <= 0f)
        {
            ApplyEdge(pendingEdge);
            edgeChangePending = false;
            showing = true;
        }
    }

    private void ApplyEdge(OffscreenEdge edge)
    {
        currentEdge = edge;

        if (config != null)
            tickRect.localEulerAngles = new Vector3(0f, 0f, GetEdgeRotation(edge));
    }

    private void ApplyFadedColor()
    {
        Color faded = baseColor;

        if (config != null && config.FadeSeconds > 0f)
            faded.a *= config.EvaluateFade(fadeProgress);

        tick.color = faded;
    }

    // Authored with the long axis on +X and the outward face on +Y, so one rotation
    // per edge keeps an asymmetric bar pointing off screen on all four.
    private static float GetEdgeRotation(OffscreenEdge edge)
    {
        switch (edge)
        {
            case OffscreenEdge.Up:
                return 0f;
            case OffscreenEdge.Right:
                return -90f;
            case OffscreenEdge.Down:
                return 180f;
            default:
                return 90f;
        }
    }

    private static Vector2 GetOutwardDirection(OffscreenEdge edge)
    {
        switch (edge)
        {
            case OffscreenEdge.Up:
                return Vector2.up;
            case OffscreenEdge.Right:
                return Vector2.right;
            case OffscreenEdge.Down:
                return Vector2.down;
            default:
                return Vector2.left;
        }
    }
}
