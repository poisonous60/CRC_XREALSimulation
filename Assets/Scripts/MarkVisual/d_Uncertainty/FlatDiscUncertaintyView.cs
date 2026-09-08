using UnityEngine;

/// <summary>
/// Uncertainty footprint drawn as a flat disc lying on the surface.
/// </summary>
[DisallowMultipleComponent]
public class FlatDiscUncertaintyView : UncertaintyPresentation
{
    [Header("Mesh")]
    [Tooltip("Object carrying the disc mesh. Empty uses the first renderer under this object.")]
    [SerializeField] private Transform visual;

    [Tooltip("Diameter of the unit mesh, in mesh units. A Unity Cylinder is 1.")]
    [SerializeField, Min(0.0001f)] private float meshDiameter = 1f;

    private Renderer discRenderer;
    private Material discMaterial;

    public override void Show(Pose surfacePose, float radiusMeters, Color color)
    {
        if (!TryResolveRenderer())
            return;

        transform.SetPositionAndRotation(surfacePose.position, surfacePose.rotation);

        // The visual child owns the thickness, so the root only carries the radius.
        float diameter = 2f * radiusMeters / meshDiameter;
        transform.localScale = new Vector3(diameter, 1f, diameter);

        if (discMaterial.color != color)
            discMaterial.color = color;
    }

    private bool TryResolveRenderer()
    {
        if (discMaterial != null)
            return true;

        if (visual != null)
            discRenderer = visual.GetComponent<Renderer>();

        if (discRenderer == null)
            discRenderer = GetComponentInChildren<Renderer>(true);

        if (discRenderer == null)
        {
            Debug.LogWarning($"[FlatDiscUncertaintyView] {name} has no renderer.");
            return false;
        }

        discMaterial = discRenderer.material;
        return true;
    }

    private void OnDestroy()
    {
        if (discMaterial != null)
            Destroy(discMaterial);
    }
}
