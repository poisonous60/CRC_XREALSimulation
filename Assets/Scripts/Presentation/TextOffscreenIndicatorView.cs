using TMPro;
using UnityEngine;

/// <summary>
/// Default off-screen indicator: an ASCII arrow and the detector id on one line.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextOffscreenIndicatorView : OffscreenIndicatorView
{
    [Header("Text")]
    [Tooltip("Font size of the indicator line.")]
    [SerializeField, Min(10f)] private float fontSize = 28f;

    [Tooltip("Width and height of the indicator rect.")]
    [SerializeField] private Vector2 rectSize = new Vector2(420f, 64f);

    [Header("Arrows")]
    [Tooltip("Glyph shown when the detector is off the left edge.")]
    [SerializeField] private string leftArrow = "<";

    [Tooltip("Glyph shown when the detector is off the right edge.")]
    [SerializeField] private string rightArrow = ">";

    [Tooltip("Glyph shown when the detector is above the view.")]
    [SerializeField] private string upArrow = "^";

    [Tooltip("Glyph shown when the detector is below the view.")]
    [SerializeField] private string downArrow = "v";

    private TextMeshProUGUI text;

    private void Awake()
    {
        text = GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;
        text.rectTransform.sizeDelta = rectSize;
    }

    public override void Show(string label, OffscreenEdge edge, Color color)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;

        switch (edge)
        {
            case OffscreenEdge.Left:
                rect.pivot = new Vector2(0f, 0.5f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.text = $"{leftArrow}  {label}";
                break;

            case OffscreenEdge.Right:
                rect.pivot = new Vector2(1f, 0.5f);
                text.alignment = TextAlignmentOptions.MidlineRight;
                text.text = $"{label}  {rightArrow}";
                break;

            case OffscreenEdge.Up:
                rect.pivot = new Vector2(0.5f, 1f);
                text.alignment = TextAlignmentOptions.Top;
                text.text = $"{upArrow}  {label}";
                break;

            default:
                rect.pivot = new Vector2(0.5f, 0f);
                text.alignment = TextAlignmentOptions.Bottom;
                text.text = $"{downArrow}  {label}";
                break;
        }

        text.color = color;
    }
}
