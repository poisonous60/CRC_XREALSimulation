using UnityEngine;

/// <summary>
/// Threshold and averaging window of the strongest-detector rule, edited in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "DetectorPeakConfig", menuName = "RadVis/Detector Peak Config")]
public class DetectorPeakConfig : ScriptableObject
{
    [Header("Rule")]
    [Tooltip("Window mean the strongest detector has to reach before the source disc is drawn. 3 fits the 2026-09-07 lab source: background 0.4 cps, 20 cm gives 4.8 and 30 cm gives 1.9.")]
    [SerializeField, Min(0f)] private float thresholdCps = 3f;

    [Tooltip("Seconds of server-second readings averaged per detector before the strongest one is picked.")]
    [SerializeField, Min(1)] private int windowSeconds = 3;

    [Tooltip("Age above which a detector's last reading stops counting as reporting. Same 5 s rule the marker manager uses for its snapshot.")]
    [SerializeField, Min(0.5f)] private float freshnessSeconds = 5f;

    public float ThresholdCps => Mathf.Max(0f, thresholdCps);
    public int WindowSeconds => Mathf.Max(1, windowSeconds);
    public float FreshnessSeconds => Mathf.Max(0.5f, freshnessSeconds);
}
