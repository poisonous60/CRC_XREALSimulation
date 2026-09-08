using UnityEngine;

/// <summary>
/// Size rule for one circle marker look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "a_00_circle_config", menuName = "RadVis/Circle Marker Config")]
public class CircleMarkerConfig : ScriptableObject
{
    private const float MinimumDiameterMeters = 0.001f;
    private const float MinimumDistanceMeters = 0.05f;

    [Header("Size")]
    [Tooltip("Circle diameter in meters at Reference Distance Meters. Replaces Marker Size Meters for this look.")]
    [SerializeField, Min(MinimumDiameterMeters)] private float diameterMeters = 0.6f;

    [Tooltip("Distance at which the circle is exactly Diameter Meters across.")]
    [SerializeField, Min(MinimumDistanceMeters)] private float referenceDistanceMeters = 3f;

    [Tooltip("1 leaves the circle world-fixed so only perspective grows it. 0 holds the same screen size at every distance. 2 grows it twice as fast as perspective.")]
    [SerializeField, Range(0f, 3f)] private float approachGrowth = 1f;

    public float DiameterMeters => Mathf.Max(MinimumDiameterMeters, diameterMeters);
    public float ReferenceDistanceMeters => Mathf.Max(MinimumDistanceMeters, referenceDistanceMeters);
    public float ApproachGrowth => approachGrowth;

    public float GetWorldDiameter(float headDistanceMeters)
    {
        float distance = Mathf.Max(MinimumDistanceMeters, headDistanceMeters);
        return DiameterMeters * Mathf.Pow(ReferenceDistanceMeters / distance, ApproachGrowth - 1f);
    }
}
