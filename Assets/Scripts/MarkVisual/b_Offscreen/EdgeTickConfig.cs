using UnityEngine;

/// <summary>
/// Shape and placement of one edge tick cue, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "b_00_edge_tick_config", menuName = "RadVis/Edge Tick Config")]
public class EdgeTickConfig : ScriptableObject
{
    [Header("Shape")]
    [Tooltip("Length along the screen edge. The tick turns with the edge, so this stays the long axis on all four.")]
    [SerializeField, Min(1f)] private float length = 96f;

    [Tooltip("Thickness across the screen edge.")]
    [SerializeField, Min(1f)] private float thickness = 14f;

    [Header("Placement")]
    [Tooltip("Pixels the tick is pushed outward from the margin the HUD anchors it at. 0 leaves it on that margin; a positive value can push it off the side.")]
    [SerializeField] private float edgeOffset;

    [Header("Fade")]
    [Tooltip("Seconds the tick takes to reach full opacity after it appears. 0 switches it on instantly.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0f;

    [Tooltip("Shape of the fade. X is elapsed fade progress 0 to 1, Y is the share of full opacity shown. Straight line fades evenly.")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public float Length => Mathf.Max(1f, length);
    public float Thickness => Mathf.Max(1f, thickness);
    public float EdgeOffset => edgeOffset;
    public float FadeSeconds => Mathf.Max(0f, fadeSeconds);

    public float EvaluateFade(float progress)
    {
        float clamped = Mathf.Clamp01(progress);

        if (fadeCurve == null || fadeCurve.length == 0)
            return clamped;

        return Mathf.Clamp01(fadeCurve.Evaluate(clamped));
    }
}
