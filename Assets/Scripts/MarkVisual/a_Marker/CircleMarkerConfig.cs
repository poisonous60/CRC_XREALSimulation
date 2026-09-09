using UnityEngine;

/// <summary>
/// Distance rule and opacity of one circle marker look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "a_00_circle_config", menuName = "RadVis/Circle Marker Config")]
public class CircleMarkerConfig : ScriptableObject
{
    private const float MinimumDistanceMeters = 0.05f;

    [Header("Size")]
    [Tooltip("Distance at which the circle is exactly Marker Size Meters across. The base size itself comes from Config > (a) Marker > Marker Size Meters.")]
    [SerializeField, Min(MinimumDistanceMeters)] private float referenceDistanceMeters = 3f;

    [Tooltip("1 leaves the circle world-fixed so only perspective grows it. 0 holds the same screen size at every distance. 2 grows it twice as fast as perspective.")]
    [SerializeField, Range(0f, 3f)] private float approachGrowth = 1f;

    [Header("Look")]
    [Tooltip("Opacity this look draws at. Overwrites Marker Alpha Override on the prefab, which is what the manager reads.")]
    [RuntimeTunable(0f, 1f, "Marker alpha")]
    [SerializeField, Range(0f, 1f)] private float markerAlpha = 0.5f;

    [Tooltip("Multiplier on the circle color before it is written. URP Bloom only catches pixels brighter than its threshold, so 1 glows nothing and higher values widen the halo.")]
    [RuntimeTunable(1f, 8f, "Bloom boost")]
    [SerializeField, Range(1f, 8f)] private float bloomBoost = 1f;

    public float ReferenceDistanceMeters => Mathf.Max(MinimumDistanceMeters, referenceDistanceMeters);
    public float ApproachGrowth => approachGrowth;
    public float MarkerAlpha => Mathf.Clamp01(markerAlpha);
    public float BloomBoost => Mathf.Max(1f, bloomBoost);

    public float GetDistanceScale(float headDistanceMeters)
    {
        float distance = Mathf.Max(MinimumDistanceMeters, headDistanceMeters);
        return Mathf.Pow(ReferenceDistanceMeters / distance, ApproachGrowth - 1f);
    }
}
