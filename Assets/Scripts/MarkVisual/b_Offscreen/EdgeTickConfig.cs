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
    [Tooltip("Distance from the screen edge to the tick's outward end. 0 puts the tick against the edge; negative pushes it off screen.")]
    [SerializeField] private float edgeOffset = 12f;

    public float Length => Mathf.Max(1f, length);
    public float Thickness => Mathf.Max(1f, thickness);
    public float EdgeOffset => edgeOffset;
}
