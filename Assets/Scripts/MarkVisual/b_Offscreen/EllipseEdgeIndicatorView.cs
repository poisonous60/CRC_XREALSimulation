using UnityEngine;

/// <summary>
/// (b) off-screen cue placed on an ellipse border: a chevron and a short halo arc, no text.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EllipseEdgeArrowGraphic))]
public class EllipseEdgeIndicatorView : OffscreenIndicatorView
{
    [Header("Layout")]
    [Tooltip("Width and height of the cue's rect.")]
    [SerializeField] private Vector2 rectSize = new Vector2(96f, 96f);

    private EllipseEdgeArrowGraphic arrow;

    public override bool UsesEllipseClamp => true;

    private void Awake()
    {
        arrow = GetComponent<EllipseEdgeArrowGraphic>();
        arrow.raycastTarget = false;
        arrow.rectTransform.sizeDelta = rectSize;
    }

    public override void Show(string label, OffscreenEdge edge, Color color)
    {
        if (arrow == null)
            return;

        arrow.color = color;
    }

    public override void SetDirection(float degrees)
    {
        if (arrow == null)
            return;

        arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, degrees);
    }
}
