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
    }

    public override void Show(string detectorId, OffscreenEdge edge, Color statusColor)
    {
        if (tick == null)
            return;

        showing = true;
        baseColor = useStatusColor ? statusColor : configuredColor;
        ApplyFadedColor();
        currentEdge = edge;

        if (config == null)
            return;

        tickRect.sizeDelta = new Vector2(config.Length, config.Thickness);
        tickRect.localEulerAngles = new Vector3(0f, 0f, GetEdgeRotation(edge));
    }

    public override bool Hide()
    {
        showing = false;

        return config == null || config.FadeSeconds <= 0f || fadeProgress <= 0f;
    }

    // The placer anchors this root after calling Show, so the offset has to wait until
    // the frame's placement is already in.
    private void LateUpdate()
    {
        if (tickRect == null || config == null)
            return;

        tickRect.anchoredPosition = GetOutwardDirection(currentEdge) * config.EdgeOffset;

        float target = showing ? 1f : 0f;

        if (Mathf.Approximately(fadeProgress, target))
            return;

        fadeProgress = config.FadeSeconds > 0f
            ? Mathf.MoveTowards(fadeProgress, target, Time.deltaTime / config.FadeSeconds)
            : target;

        ApplyFadedColor();
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
