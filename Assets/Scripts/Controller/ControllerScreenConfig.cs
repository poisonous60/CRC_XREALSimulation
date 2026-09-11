using UnityEngine;

/// <summary>
/// Numbers the Beam Pro lock and settings screens need. Text and colors live on the prefab.
/// </summary>
[CreateAssetMenu(fileName = "ControllerScreenConfig", menuName = "RadVis/Controller Screen Config")]
public class ControllerScreenConfig : ScriptableObject
{
    [Header("Lock Screen")]
    [Tooltip("Touches needed inside Unlock Window Seconds to leave the lock screen.")]
    [SerializeField, Min(2)] private int unlockTapCount = 5;

    [Tooltip("Window the touches must fall inside. The newest and the oldest of the last Unlock Tap Count touches are compared.")]
    [SerializeField, Min(0.1f)] private float unlockWindowSeconds = 1f;

    [Tooltip("Touches landing this close together count as one. Grabbing the phone puts several fingers down at once, while deliberate tapping is far slower.")]
    [SerializeField, Min(0f)] private float tapGroupingSeconds = 0.05f;

    [Header("Settings Screen Margins (% of panel height)")]
    [Tooltip("Empty space between the panel's top edge and the title.")]
    [SerializeField, Range(0f, 100f)] private float topMargin = 14f;

    [Tooltip("Height of the title band.")]
    [SerializeField, Range(0f, 100f)] private float titleHeight = 8f;

    [Tooltip("Empty space between the title and the first slider row.")]
    [SerializeField, Range(0f, 100f)] private float titleGap = 2f;

    [Tooltip("Empty space between the slider rows area and the panel's bottom edge. The Back button sits inside it.")]
    [SerializeField, Range(0f, 100f)] private float bottomMargin = 30f;

    [Tooltip("Height of the Back button's center above the panel's bottom edge.")]
    [SerializeField, Range(0f, 100f)] private float backButtonFromBottom = 8.8f;

    [Header("Settings Screen Rows (px)")]
    [Tooltip("Empty pixels above the first row, inside the rows area.")]
    [SerializeField, Min(0)] private int rowPaddingTop = 0;

    [Tooltip("Empty pixels below the last row, inside the rows area.")]
    [SerializeField, Min(0)] private int rowPaddingBottom = 0;

    [Tooltip("Pixels between two rows of the same config, header row included.")]
    [SerializeField, Min(0f)] private float rowSpacing = 24f;

    [Tooltip("Pixels between the last row of one config and the header row of the next.")]
    [SerializeField, Min(0f)] private float categorySpacing = 24f;

    public int UnlockTapCount => Mathf.Max(2, unlockTapCount);
    public float UnlockWindowSeconds => Mathf.Max(0.1f, unlockWindowSeconds);
    public float TapGroupingSeconds => Mathf.Max(0f, tapGroupingSeconds);

    public float TitleTopFraction => 1f - Fraction(topMargin);
    public float TitleBottomFraction => Mathf.Clamp01(TitleTopFraction - Fraction(titleHeight));
    public float RowsTopFraction => Mathf.Clamp01(TitleBottomFraction - Fraction(titleGap));
    public float RowsBottomFraction => Fraction(bottomMargin);
    public float BackButtonFraction => Fraction(backButtonFromBottom);
    public int RowPaddingTop => Mathf.Max(0, rowPaddingTop);
    public int RowPaddingBottom => Mathf.Max(0, rowPaddingBottom);
    public float RowSpacing => Mathf.Max(0f, rowSpacing);
    public float CategorySpacing => Mathf.Max(0f, categorySpacing);

    private static float Fraction(float percent) => Mathf.Clamp01(percent / 100f);
}
