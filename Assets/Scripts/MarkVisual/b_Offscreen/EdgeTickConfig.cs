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
    [Tooltip("Pixels the tick is pushed outward from the margin the HUD anchors it at. 0 leaves it on that margin; XREAL shows less than the canvas is wide, so a positive value can push it off the side.")]
    [SerializeField] private float edgeOffset;

    public float Length => Mathf.Max(1f, length);
    public float Thickness => Mathf.Max(1f, thickness);
    public float EdgeOffset => edgeOffset;
}
