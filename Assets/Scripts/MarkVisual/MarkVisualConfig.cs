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
[CreateAssetMenu(fileName = "MarkVisualConfig", menuName = "RadVis/Mark Visual Config")]
public class MarkVisualConfig : ScriptableObject
{
    private const string ResourceName = "MarkVisualConfig";
    private const float MinimumMarkerSizeMeters = 0.001f;

    private static MarkVisualConfig loaded;

    [Header("(a) Marker")]
    [Tooltip("Replaces the whole marker object. Empty keeps the built-in sphere and its transparent shader.")]
    [SerializeField] private GameObject markerPrefab;

    [Tooltip("Marker size in meters. Radiation changes color only, never size.")]
    [SerializeField, Min(MinimumMarkerSizeMeters)] private float markerSizeMeters = 0.2f;

    [Header("Status Color by CPS")]
    [Tooltip("Bands from low to high CPS. Press + to add one. A reading past the last band keeps that band's color.")]
    [SerializeField] private List<StatusColorBand> statusBands = new List<StatusColorBand>();

    [Tooltip("Color when the reading is missing or invalid, or when no band is set.")]
    [SerializeField] private Color unknownColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Tooltip("ON = a reading at or below the marker manager's Hidden Max Cps hides the marker center. OFF = the center is drawn in Low Cps Color instead.")]
    [SerializeField] private bool hideLowCps = true;

    [Tooltip("Color at or below the marker manager's Hidden Max Cps. The center shows it only while Hide Low Cps is off; the off-screen cue always does.")]
    [SerializeField] private Color lowCpsColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    [Header("(b) Off-screen Cue")]
    [Tooltip("Look of one off-screen indicator. Empty keeps the built-in arrow and detector id.")]
    [SerializeField] private OffscreenIndicatorView offscreenPrefab;

    [Tooltip("How much of the cue's own size is held inside the screen edge. 1 keeps the whole cue inside, 0 lets it straddle the edge. Applies to the ellipse border only.")]
    [SerializeField, Range(0f, 1f)] private float offscreenEdgeInset = 1f;

    [Tooltip("Color the cue by the detector's reading. Off uses Offscreen Color instead.")]
    [SerializeField] private bool offscreenUseStatusColor = true;

    [Tooltip("Cue color used when Offscreen Use Status Color is off.")]
    [SerializeField] private Color offscreenColor = Color.white;

    [Header("(c) Proximity Information")]
    [Tooltip("Every look shown as the user approaches. All entries are spawned together. Empty draws nothing.")]
    [SerializeField] private List<SourcePresentation> proximityPrefabs = new List<SourcePresentation>();

    [Header("Code-drawn Extras")]
    [Tooltip("Faint inverse-square shells around the marker. Drawn in code, so no prefab changes their look.")]
    [SerializeField] private bool showFalloffShells = true;

    [Tooltip("Calibrated reference distance for I(r) = I0 * (reference/r)^2. At 8 cm, a 351 CPS reading produces roughly 0.47 m yellow and 1.06 m green boundaries.")]
    [SerializeField, Min(0.01f)] private float falloffReferenceDistanceMeters = 0.08f;

    [Tooltip("Maximum radius visualized around one marker.")]
    [SerializeField, Min(0.5f)] private float falloffMaxRadiusMeters = 5f;

    [Tooltip("Opacity of the outer falloff shells. Keep this very low so they do not obstruct the glasses view.")]
    [SerializeField, Range(0.002f, 0.12f)] private float falloffShellAlpha = 0.012f;

    [Tooltip("Maximum number of transition shells drawn around one marker.")]
    [SerializeField, Range(1, 3)] private int maxFalloffShells = 3;

    [Tooltip("Detector name text beside the marker. Drawn in code, so no prefab changes its look.")]
    [SerializeField] private bool showLabel = false;

    public GameObject MarkerPrefab => markerPrefab;
    public float MarkerSizeMeters => Mathf.Max(MinimumMarkerSizeMeters, markerSizeMeters);
    public Color UnknownColor => unknownColor;
    public bool HideLowCps => hideLowCps;
    public Color LowCpsColor => lowCpsColor;
    public OffscreenIndicatorView OffscreenPrefab => offscreenPrefab;
    public float OffscreenEdgeInset => offscreenEdgeInset;
    public bool OffscreenUseStatusColor => offscreenUseStatusColor;
    public Color OffscreenColor => offscreenColor;
    public IReadOnlyList<SourcePresentation> ProximityPrefabs => proximityPrefabs;

    public bool HasProximityPrefab()
    {
        if (proximityPrefabs == null)
            return false;

        for (int index = 0; index < proximityPrefabs.Count; index++)
        {
            if (proximityPrefabs[index] != null)
                return true;
        }

        return false;
    }
    public bool ShowFalloffShells => showFalloffShells;
    public float FalloffReferenceDistanceMeters => falloffReferenceDistanceMeters;
    public float FalloffMaxRadiusMeters => falloffMaxRadiusMeters;
    public float FalloffShellAlpha => falloffShellAlpha;
    public int MaxFalloffShells => maxFalloffShells;
    public bool ShowLabel => showLabel;

    // Statics survive entering play mode when Reload Domain is off, so drop the cache here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        loaded = null;
    }

    public static bool TryLoad(out MarkVisualConfig config)
    {
        if (loaded == null)
            loaded = Resources.Load<MarkVisualConfig>(ResourceName);

        config = loaded;
        return config != null;
    }

    public bool TryGetStatusColor(float countsPerSecond, out Color color)
    {
        color = unknownColor;

        float highestBound = GetHighestBandBound();

        if (float.IsNegativeInfinity(highestBound))
            return true;

        if (float.IsNaN(countsPerSecond) || float.IsInfinity(countsPerSecond) || countsPerSecond < 0f)
            return true;

        // Clamping to the last bound keeps a reading past every band inside that band.
        float reading = Mathf.Min(countsPerSecond, highestBound);
        float bestBound = float.PositiveInfinity;

        for (int index = 0; index < statusBands.Count; index++)
        {
            StatusColorBand band = statusBands[index];

            if (band == null || reading > band.MaxCps || band.MaxCps >= bestBound)
                continue;

            bestBound = band.MaxCps;
            color = band.Color;
        }

        return true;
    }

    private float GetHighestBandBound()
    {
        float highest = float.NegativeInfinity;

        if (statusBands == null)
            return highest;

        for (int index = 0; index < statusBands.Count; index++)
        {
            StatusColorBand band = statusBands[index];

            if (band != null)
                highest = Mathf.Max(highest, band.MaxCps);
        }

        return highest;
    }

    private void OnValidate()
    {
        markerSizeMeters = Mathf.Max(MinimumMarkerSizeMeters, markerSizeMeters);
    }
}
