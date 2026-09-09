using UnityEngine;

/// <summary>
/// Applies one circle look's distance rule and shading on top of the marker root scale the manager sets.
/// </summary>
[DisallowMultipleComponent]
public class CircleMarkerVisual : MonoBehaviour
{
    private static readonly int BloomBoostId = Shader.PropertyToID("_BloomBoost");

    [Header("Look")]
    [Tooltip("Distance rule and shading for this look. Empty leaves the mesh at its authored scale.")]
    [SerializeField] private CircleMarkerConfig config;

    [Tooltip("Child carrying the mesh. Empty means the first child.")]
    [SerializeField] private Transform visual;

    [Tooltip("Camera the distance is measured from. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    public CircleMarkerConfig Config => config;

    // Runs before the manager builds SourceMarkerVisual, so the alpha it reads back
    // off MarkerAlphaOverride is already the config's.
    private void Awake()
    {
        if (config == null)
            return;

        MarkerAlphaOverride alphaOverride = GetComponent<MarkerAlphaOverride>();

        if (alphaOverride != null)
            alphaOverride.SetMarkerAlpha(config.MarkerAlpha);

        Renderer circleRenderer = GetComponentInChildren<Renderer>(true);

        if (circleRenderer != null)
            circleRenderer.material.SetFloat(BloomBoostId, config.BloomBoost);
    }

    private void LateUpdate()
    {
        if (config == null)
            return;

        if (visual == null)
        {
            if (transform.childCount == 0)
                return;

            visual = transform.GetChild(0);
        }

        if (head == null)
        {
            head = Camera.main;

            if (head == null)
                return;
        }

        // The root carries Marker Size Meters, so the child only multiplies the distance rule onto it.
        float distance = Vector3.Distance(head.transform.position, transform.position);
        visual.localScale = Vector3.one * config.GetDistanceScale(distance);
    }
}
