using UnityEngine;

/// <summary>
/// Settings-screen switch and outline width of the plane debug look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "f_02_plane_sheets_config", menuName = "RadVis/Plane Sheets Config")]
public class PlaneSheetsConfig : ScriptableObject
{
    [Header("Debug")]
    [Tooltip("ON = every AR plane the glasses detect is drawn as a translucent sheet with an outline. A debugging aid, not part of the demo.")]
    [RuntimeTunable("Show planes")]
    [SerializeField] private bool showPlanes;

    [Tooltip("Outline width in meters. Read when a plane is first drawn.")]
    [SerializeField, Min(0.001f)] private float outlineWidthMeters = 0.005f;

    public bool ShowPlanes => showPlanes;
    public float OutlineWidthMeters => outlineWidthMeters;
}
