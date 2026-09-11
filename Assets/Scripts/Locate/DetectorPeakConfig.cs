using UnityEngine;

/// <summary>
/// Threshold, averaging window and single-or-all mode of the detector rule, edited in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "DetectorPeakConfig", menuName = "RadVis/Detector Peak Config")]
public class DetectorPeakConfig : ScriptableObject
{
    [Header("Rule")]
    [Tooltip("Window mean the strongest detector has to reach before the source disc is drawn. 3 fits the 2026-09-07 lab source: background 0.4 cps, 20 cm gives 4.8 and 30 cm gives 1.9.")]
    [RuntimeTunable(0f, 20f, "Threshold (cps)")]
    [SerializeField, Min(0f)] private float thresholdCps = 3f;

    [Tooltip("Seconds of server-second readings averaged per detector before the strongest one is picked.")]
    [SerializeField, Min(1)] private int windowSeconds = 3;

    [Tooltip("Age above which a detector's last reading stops counting as reporting. Same 5 s rule the marker manager uses for its snapshot.")]
    [SerializeField, Min(0.5f)] private float freshnessSeconds = 5f;

    [Tooltip("How far another detector's window mean has to beat the standing winner before the disc moves, as a fraction of the standing mean. 0 hands the disc to any lead, the behavior up to 2026-09-10. 0.4 holds it still for a source midway between two detectors 20 cm apart, where both read about 20 cps and a 3 s mean scatters 2.6.")]
    [SerializeField, Min(0f)] private float switchMarginRatio;

    [Tooltip("ON = a source disc is drawn at every detector whose window mean reaches the threshold. OFF = only at the strongest one, the behavior up to 2026-09-11.")]
    [RuntimeTunable("Show all above threshold")]
    [SerializeField] private bool showAllAboveThreshold;

    public bool ShowAllAboveThreshold => showAllAboveThreshold;
    public float ThresholdCps => Mathf.Max(0f, thresholdCps);
    public int WindowSeconds => Mathf.Max(1, windowSeconds);
    public float FreshnessSeconds => Mathf.Max(0.5f, freshnessSeconds);
    public float SwitchMarginRatio => Mathf.Max(0f, switchMarginRatio);
}
