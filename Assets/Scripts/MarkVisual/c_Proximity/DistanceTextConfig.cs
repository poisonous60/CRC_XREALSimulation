using UnityEngine;

/// <summary>
/// Size, color and visible range of one distance text look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "c_01_distance_text_config", menuName = "RadVis/Distance Text Config")]
public class DistanceTextConfig : ScriptableObject
{
    [Header("Size")]
    [Tooltip("Height of one text line on the glasses display, in pixels.")]
    [RuntimeTunable(8f, 72f, "Text size (px)")]
    [SerializeField, Min(1f)] private float textSizePixels = 24f;

    [Tooltip("On: the text holds the pixel size above at every distance. Off: it shrinks with distance like a real object.")]
    [SerializeField] private bool constantScreenSize = true;

    [Tooltip("Head-to-source distance at which the pixel size above is met. Only used while Constant Screen Size is off.")]
    [SerializeField, Min(0.05f)] private float referenceDistanceMeters = 2f;

    [Header("Look")]
    [Tooltip("Color of the distance line. The source color is not copied so the reading stays the marker's job.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Outline thickness that keeps the text readable against a bright room.")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;

    [Tooltip("Outline color.")]
    [SerializeField] private Color outlineColor = Color.black;

    [Header("Range")]
    [Tooltip("Distance above which the line is hidden. 0 keeps it visible at every distance.")]
    [RuntimeTunable(0f, 10f, "Text range (m)")]
    [SerializeField, Min(0f)] private float maximumVisibleDistanceMeters = 0f;

    public float TextSizePixels => Mathf.Max(1f, textSizePixels);
    public bool ConstantScreenSize => constantScreenSize;
    public float ReferenceDistanceMeters => Mathf.Max(0.05f, referenceDistanceMeters);
    public Color TextColor => textColor;
    public float OutlineWidth => Mathf.Clamp01(outlineWidth);
    public Color OutlineColor => outlineColor;
    public float MaximumVisibleDistanceMeters => Mathf.Max(0f, maximumVisibleDistanceMeters);
}
