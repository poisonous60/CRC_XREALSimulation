using UnityEngine;

/// <summary>
/// Sizes a circle marker from its own config, leaving the marker root scale to the manager.
/// </summary>
[DisallowMultipleComponent]
public class CircleMarkerVisual : MonoBehaviour
{
    private const float MinimumRootScale = 0.0001f;

    [Header("Look")]
    [Tooltip("Size rule for this look. Empty leaves the mesh at its authored scale.")]
    [SerializeField] private CircleMarkerConfig config;

    [Tooltip("Child carrying the mesh. Empty means the first child.")]
    [SerializeField] private Transform visual;

    [Tooltip("Camera the distance is measured from. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    public CircleMarkerConfig Config => config;

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

        // The manager owns the root scale, so the size rule goes on the mesh child.
        // Scaling the root would also resize the falloff shells and the ray pick radius.
        float rootScale = Mathf.Abs(transform.localScale.x);

        if (rootScale < MinimumRootScale)
            return;

        float distance = Vector3.Distance(head.transform.position, transform.position);
        visual.localScale = Vector3.one * (config.GetWorldDiameter(distance) / rootScale);
    }
}
