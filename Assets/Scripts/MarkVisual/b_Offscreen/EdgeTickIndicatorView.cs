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

    public EdgeTickConfig Config => config;

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

    public override void Show(string detectorId, OffscreenEdge edge, Color statusColor)
    {
        if (tick == null)
            return;

        tick.color = useStatusColor ? statusColor : configuredColor;
        currentEdge = edge;

        if (config == null)
            return;

        tickRect.sizeDelta = new Vector2(config.Length, config.Thickness);
        tickRect.localEulerAngles = new Vector3(0f, 0f, GetEdgeRotation(edge));
    }

    // The placer anchors this root after calling Show, so reading the anchor for the
    // offset has to wait until the frame's placement is already in.
    private void LateUpdate()
    {
        if (tickRect == null || config == null)
            return;

        tickRect.anchoredPosition = GetOutwardDirection(currentEdge) * GetOutwardShift(currentEdge);
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

    private float GetOutwardShift(OffscreenEdge edge)
    {
        RectTransform root = (RectTransform)transform;
        RectTransform layer = root.parent as RectTransform;

        if (layer == null)
            return 0f;

        float marginPixels;

        switch (edge)
        {
            case OffscreenEdge.Up:
                marginPixels = (1f - root.anchorMax.y) * layer.rect.height;
                break;
            case OffscreenEdge.Down:
                marginPixels = root.anchorMin.y * layer.rect.height;
                break;
            case OffscreenEdge.Right:
                marginPixels = (1f - root.anchorMax.x) * layer.rect.width;
                break;
            default:
                marginPixels = root.anchorMin.x * layer.rect.width;
                break;
        }

        return marginPixels - config.EdgeOffset - config.Thickness * 0.5f;
    }
}
