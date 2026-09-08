using UnityEngine;

/// <summary>
/// Paper geometry and look of the estimated-source scene.
/// </summary>
[CreateAssetMenu(fileName = "SourceLocateConfig", menuName = "RadVis/Source Locate Config")]
public class SourceLocateConfig : ScriptableObject
{
    [Header("Calibration")]
    [Tooltip("A copy of msv100-calibration/layout.json: the four corners and every calibration anchor.")]
    [SerializeField] private TextAsset layoutJson;

    [Header("Paper Geometry")]
    [Tooltip("Room X and Z, in meters, of the square's (0, 0) corner. Default assumes the two A4 sheets butted long edge to long edge, ROOM_ORIGIN on the left.")]
    [SerializeField] private Vector2 squareOriginFromMarkerMeters = new Vector2(0.10994f, -0.13304f);

    [Header("Height Above the Paper")]
    [Tooltip("Height of the four detector labels.")]
    [SerializeField, Min(0f)] private float detectorHeightMeters = 0.02f;

    [Tooltip("Height of the uncertainty disc. Keep it just off the paper so it does not z-fight.")]
    [SerializeField, Min(0f)] private float discHeightMeters = 0.002f;

    [Tooltip("Height of the source marker. The container's active material sits near its mid-height.")]
    [SerializeField, Min(0f)] private float sourceHeightMeters = 0.045f;

    [Header("Uncertainty Disc")]
    [Tooltip("Smallest disc radius drawn, so a confident estimate still shows a mark.")]
    [SerializeField, Min(0.001f)] private float minimumDiscRadiusMeters = 0.005f;

    [Header("Detector Labels")]
    [Tooltip("World height of one label line.")]
    [SerializeField, Min(0.001f)] private float labelHeightMeters = 0.012f;

    [Tooltip("Label color.")]
    [SerializeField] private Color labelColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    [Header("Motion")]
    [Tooltip("Seconds the marker takes to slide to a new estimate. 0 moves it the instant the estimate arrives, like locate.py.")]
    [SerializeField, Min(0f)] private float positionLerpSeconds = 0f;

    public TextAsset LayoutJson => layoutJson;
    public Vector2 SquareOriginFromMarkerMeters => squareOriginFromMarkerMeters;
    public float DetectorHeightMeters => detectorHeightMeters;
    public float DiscHeightMeters => discHeightMeters;
    public float SourceHeightMeters => sourceHeightMeters;
    public float MinimumDiscRadiusMeters => minimumDiscRadiusMeters;
    public float LabelHeightMeters => labelHeightMeters;
    public Color LabelColor => labelColor;
    public float PositionLerpSeconds => positionLerpSeconds;
}
