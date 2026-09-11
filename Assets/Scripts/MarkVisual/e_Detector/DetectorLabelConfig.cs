using UnityEngine;

/// <summary>
/// Size, offset and color of one detector label look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "e_01_cps_label_config", menuName = "RadVis/Detector Label Config")]
public class DetectorLabelConfig : ScriptableObject
{
    private const float MinimumDistanceMeters = 0.05f;

    [Header("Size")]
    [Tooltip("Height of one text line on the glasses display, in pixels.")]
    [RuntimeTunable(24f, 200f, "Label size (px)")]
    [SerializeField, Min(1f)] private float textSizePixels = 40f;

    [Tooltip("Head-to-detector distance at which the pixel size above is met.")]
    [SerializeField, Min(MinimumDistanceMeters)] private float referenceDistanceMeters = 2f;

    [Tooltip("1 leaves the label world-fixed so only perspective grows it as you walk up. 0 holds the same pixel size at every distance. 2 grows it twice as fast as perspective.")]
    [SerializeField, Range(0f, 3f)] private float approachGrowth = 1f;

    [Header("Placement")]
    [Tooltip("World height above the detector's placed point. The label sits inside the source disc on purpose when this detector wins.")]
    [SerializeField] private float verticalOffsetMeters = 0.1f;

    [Header("Look")]
    [Tooltip("Color of the label. The reading's status color is not copied so the color stays the source disc's job.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Outline thickness that keeps the text readable against a bright room.")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.25f;

    [Tooltip("Outline color.")]
    [SerializeField] private Color outlineColor = Color.black;

    public float TextSizePixels => Mathf.Max(1f, textSizePixels);
    public float ReferenceDistanceMeters => Mathf.Max(MinimumDistanceMeters, referenceDistanceMeters);
    public float ApproachGrowth => approachGrowth;
    public float VerticalOffsetMeters => verticalOffsetMeters;
    public Color TextColor => textColor;
    public float OutlineWidth => Mathf.Clamp01(outlineWidth);
    public Color OutlineColor => outlineColor;

    // Growth 0 returns the head distance itself, which cancels the perspective shrink.
    public float GetSizingDistanceMeters(float headDistanceMeters)
    {
        float distance = Mathf.Max(MinimumDistanceMeters, headDistanceMeters);
        return ReferenceDistanceMeters * Mathf.Pow(ReferenceDistanceMeters / distance, ApproachGrowth - 1f);
    }
}
