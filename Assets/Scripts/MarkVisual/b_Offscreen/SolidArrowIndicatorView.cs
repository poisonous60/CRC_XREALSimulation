using TMPro;
using UnityEngine;

/// <summary>
/// (b) off-screen cue: a filled arrow pointing at the detector with its id held upright on the inboard side.
/// </summary>
[DisallowMultipleComponent]
public class SolidArrowIndicatorView : OffscreenIndicatorView
{
    [Header("Layout")]
    [Tooltip("Width and height of the cue's rect. It has to cover the arrow and the label, since the edge inset is measured from it.")]
    [SerializeField] private Vector2 rectSize = new Vector2(160f, 160f);

    [Tooltip("Distance from the arrow to the label, measured opposite the way the arrow points.")]
    [SerializeField, Min(0f)] private float labelDistance = 56f;

    [Header("Label")]
    [Tooltip("Font size of the detector id.")]
    [SerializeField, Min(10f)] private float fontSize = 32f;

    [Tooltip("Width and height of the label's own rect.")]
    [SerializeField] private Vector2 labelSize = new Vector2(120f, 48f);

    private SolidArrowGraphic arrow;
    private TextMeshProUGUI label;
    private bool useStatusColor = true;
    private Color configuredColor = Color.white;

    public override bool UsesEllipseClamp => true;

    private void Awake()
    {
        arrow = GetComponentInChildren<SolidArrowGraphic>(true);
        label = GetComponentInChildren<TextMeshProUGUI>(true);

        ((RectTransform)transform).sizeDelta = rectSize;

        if (MarkVisualConfig.TryLoad(out MarkVisualConfig config))
        {
            useStatusColor = config.OffscreenUseStatusColor;
            configuredColor = config.OffscreenColor;
        }

        if (arrow != null)
            arrow.raycastTarget = false;

        if (label == null)
        {
            Debug.LogWarning($"[SolidArrowIndicatorView] {name} found no TextMeshProUGUI child.");
            return;
        }

        label.fontSize = fontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = labelSize;
    }

    public override void Show(string detectorId, OffscreenEdge edge, Color statusColor)
    {
        Color cueColor = useStatusColor ? statusColor : configuredColor;

        if (arrow != null)
            arrow.color = cueColor;

        if (label == null)
            return;

        label.text = detectorId;
        label.color = cueColor;
    }

    public override void SetDirection(float degrees)
    {
        if (arrow != null)
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, degrees);

        if (label == null)
            return;

        float radians = degrees * Mathf.Deg2Rad;
        label.rectTransform.anchoredPosition =
            new Vector2(-Mathf.Cos(radians), -Mathf.Sin(radians)) * labelDistance;
        label.rectTransform.localEulerAngles = Vector3.zero;
    }
}
