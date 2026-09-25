using UnityEngine;

/// <summary>
/// Settings-screen switch of the frame anchor debug look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "f_01_anchor_axes_config", menuName = "RadVis/Anchor Axes Config")]
public class AnchorAxesConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("ON = XYZ axes are drawn where the room frame anchor stands. A debugging aid, not part of the demo.")]
    [RuntimeTunable("Show frame anchor")]
    [SerializeField] private bool showAxes;

    public bool ShowAxes => showAxes;
}
