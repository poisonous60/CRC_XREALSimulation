using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StatusColorBand
{
    [Tooltip("Upper CPS bound of this band. The lowest band the reading fits into wins.")]
    [SerializeField, Min(0f)] private float maxCps = 10f;

    [Tooltip("Marker color inside this band. Alpha comes from the marker's own alpha setting.")]
    [SerializeField] private Color color = Color.green;

    public float MaxCps => maxCps;
    public Color Color => color;
}

/// <summary>
/// Project-wide slots for the source marker's look, loaded from Resources by every scene.
/// </summary>
[CreateAssetMenu(fileName = "MarkVisualSetting", menuName = "RadVis/Mark Visual Setting")]
public class MarkVisualSetting : ScriptableObject
{
    private const string ResourceName = "MarkVisualSetting";
    private const float MinimumMarkerSizeMeters = 0.001f;

    private static MarkVisualSetting loaded;

    [Header("(a) Marker")]
    [Tooltip("Replaces the whole marker object. Empty keeps the built-in sphere and its transparent shader.")]
    [SerializeField] private GameObject markerPrefab;

    [Tooltip("Marker size in meters. Radiation changes color only, never size.")]
    [SerializeField, Min(MinimumMarkerSizeMeters)] private float markerSizeMeters = 0.2f;

    [Header("Status Color by CPS")]
    [Tooltip("Bands from low to high CPS. Press + to add one.")]
    [SerializeField] private List<StatusColorBand> statusBands = new List<StatusColorBand>();

    [Tooltip("Color above the highest band.")]
    [SerializeField] private Color aboveHighestBandColor = Color.red;

    [Tooltip("Color when the reading is missing or invalid.")]
    [SerializeField] private Color unknownColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("(b) Off-screen Cue")]
    [Tooltip("Look of one off-screen indicator. Empty keeps the built-in arrow and detector id.")]
    [SerializeField] private OffscreenIndicatorView offscreenPrefab;

    [Header("(c) Proximity Information")]
    [Tooltip("Information shown as the user approaches. Empty draws nothing.")]
    [SerializeField] private SourcePresentation proximityPrefab;

    [Header("Code-drawn Extras")]
    [Tooltip("Faint inverse-square shells around the marker. Drawn in code, so no prefab changes their look.")]
    [SerializeField] private bool showFalloffShells = true;

    [Tooltip("Detector name text beside the marker. Drawn in code, so no prefab changes its look.")]
    [SerializeField] private bool showLabel = false;

    public GameObject MarkerPrefab => markerPrefab;
    public float MarkerSizeMeters => Mathf.Max(MinimumMarkerSizeMeters, markerSizeMeters);
    public Color UnknownColor => unknownColor;
    public OffscreenIndicatorView OffscreenPrefab => offscreenPrefab;
    public SourcePresentation ProximityPrefab => proximityPrefab;
    public bool ShowFalloffShells => showFalloffShells;
    public bool ShowLabel => showLabel;

    // Statics survive entering play mode when Reload Domain is off, so drop the cache here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        loaded = null;
    }

    public static bool TryLoad(out MarkVisualSetting config)
    {
        if (loaded == null)
            loaded = Resources.Load<MarkVisualSetting>(ResourceName);

        config = loaded;
        return config != null;
    }

    public bool TryGetStatusColor(float countsPerSecond, out Color color)
    {
        color = unknownColor;

        if (!HasValidBand())
            return false;

        if (float.IsNaN(countsPerSecond) || float.IsInfinity(countsPerSecond) || countsPerSecond < 0f)
            return true;

        float bestBound = float.PositiveInfinity;
        bool matched = false;

        for (int index = 0; index < statusBands.Count; index++)
        {
            StatusColorBand band = statusBands[index];

            if (band == null || countsPerSecond > band.MaxCps || band.MaxCps >= bestBound)
                continue;

            bestBound = band.MaxCps;
            color = band.Color;
            matched = true;
        }

        if (!matched)
            color = aboveHighestBandColor;

        return true;
    }

    private bool HasValidBand()
    {
        if (statusBands == null)
            return false;

        for (int index = 0; index < statusBands.Count; index++)
        {
            if (statusBands[index] != null)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        markerSizeMeters = Mathf.Max(MinimumMarkerSizeMeters, markerSizeMeters);
    }
}
