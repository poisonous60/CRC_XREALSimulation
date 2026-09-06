using UnityEngine;

/// <summary>
/// Feeds the source cloud shader a 0 to 1 level from the marker's latest reading.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public class SourceCloudLevel : MonoBehaviour
{
    [Header("Reading Range")]
    [Tooltip("Reading mapped to the low end of the ramp.")]
    [SerializeField, Min(0f)] private float minimumCps = 2f;

    [Tooltip("Reading mapped to the high end of the ramp. Keep this the same across every scene so two runs stay comparable.")]
    [SerializeField, Min(0.001f)] private float maximumCps = 350f;

    [Tooltip("Map the reading logarithmically. Dose falls off with the square of distance, so a linear ramp spends most of its range on the last step.")]
    [SerializeField] private bool useLogarithmicScale = true;

    [Header("Missing Reading")]
    [Tooltip("Level used before any reading arrives.")]
    [SerializeField, Range(0f, 1f)] private float unknownLevel = 0f;

    [Header("Smoothing")]
    [Tooltip("Seconds the level takes to catch up with a new reading. 0 snaps.")]
    [SerializeField, Min(0f)] private float smoothingSeconds = 0.25f;

    private static readonly int LevelProperty = Shader.PropertyToID("_Level");

    private SourceMarker owner;
    private Renderer cloudRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float currentLevel;

    private void Awake()
    {
        cloudRenderer = GetComponent<Renderer>();
        owner = GetComponentInParent<SourceMarker>();
        propertyBlock = new MaterialPropertyBlock();
        currentLevel = unknownLevel;

        if (owner == null)
            Debug.LogWarning($"[SourceCloudLevel] {name} found no SourceMarker above it.");
    }

    private void LateUpdate()
    {
        float target = owner != null ? LevelFor(owner.lastRadiationValue) : unknownLevel;

        currentLevel = smoothingSeconds <= 0f
            ? target
            : Mathf.Lerp(currentLevel, target, 1f - Mathf.Exp(-Time.deltaTime / smoothingSeconds));

        cloudRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(LevelProperty, currentLevel);
        cloudRenderer.SetPropertyBlock(propertyBlock);
    }

    private float LevelFor(float countsPerSecond)
    {
        if (countsPerSecond < 0f ||
            float.IsNaN(countsPerSecond) ||
            float.IsInfinity(countsPerSecond))
        {
            return unknownLevel;
        }

        float low = Mathf.Max(0f, minimumCps);
        float high = Mathf.Max(low + 0.001f, maximumCps);

        if (!useLogarithmicScale)
            return Mathf.InverseLerp(low, high, countsPerSecond);

        float logLow = Mathf.Log10(low + 1f);
        float logHigh = Mathf.Log10(high + 1f);
        float logValue = Mathf.Log10(Mathf.Max(0f, countsPerSecond) + 1f);

        return Mathf.InverseLerp(logLow, logHigh, logValue);
    }
}
