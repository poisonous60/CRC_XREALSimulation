using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>Plane debug look: every AR plane the glasses detect as a translucent sheet with an outline, drawn only while the Settings checkbox is on.</summary>
[DisallowMultipleComponent]
public class PlaneSheetsView : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Holds the Settings checkbox that shows the planes. Empty never shows them.")]
    [SerializeField] private PlaneSheetsConfig config;

    [Tooltip("Sheet of floor, table and ceiling planes.")]
    [SerializeField] private Material horizontalSheetMaterial;

    [Tooltip("Outline of floor, table and ceiling planes.")]
    [SerializeField] private Material horizontalOutlineMaterial;

    [Tooltip("Sheet of wall planes.")]
    [SerializeField] private Material verticalSheetMaterial;

    [Tooltip("Outline of wall planes.")]
    [SerializeField] private Material verticalOutlineMaterial;

    private ARPlaneManager planeManager;
    private bool shown;

    public PlaneSheetsConfig Config => config;

    private void OnEnable()
    {
        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        if (planeManager == null)
        {
            Debug.LogWarning("[PlaneSheetsView] No ARPlaneManager in the scene; nothing to draw.");
            return;
        }

        planeManager.trackablesChanged.AddListener(HandleTrackablesChanged);
    }

    private void OnDisable()
    {
        if (planeManager != null)
            planeManager.trackablesChanged.RemoveListener(HandleTrackablesChanged);
    }

    private void LateUpdate()
    {
        bool visible = planeManager != null && config != null && config.ShowPlanes;

        if (visible == shown)
            return;

        shown = visible;

        foreach (ARPlane plane in planeManager.trackables)
            SetSheetEnabled(plane, visible);
    }

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        if (!shown)
            return;

        foreach (ARPlane plane in args.added)
            SetSheetEnabled(plane, true);
    }

    // ARPlaneMeshVisualizer builds the sheet and outline from the boundary and hides them when disabled.
    // Planes come from ARPlaneManager without a prefab, so the parts are added the first time they are shown.
    private void SetSheetEnabled(ARPlane plane, bool visible)
    {
        if (plane == null)
            return;

        ARPlaneMeshVisualizer visualizer = plane.GetComponent<ARPlaneMeshVisualizer>();

        if (visualizer == null)
        {
            if (!visible || config == null)
                return;

            bool horizontal = plane.alignment.IsHorizontal();
            GameObject planeObject = plane.gameObject;

            if (planeObject.GetComponent<MeshFilter>() == null)
                planeObject.AddComponent<MeshFilter>();

            MeshRenderer sheetRenderer = planeObject.GetComponent<MeshRenderer>();
            if (sheetRenderer == null)
                sheetRenderer = planeObject.AddComponent<MeshRenderer>();
            sheetRenderer.sharedMaterial = horizontal ? horizontalSheetMaterial : verticalSheetMaterial;
            sheetRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sheetRenderer.receiveShadows = false;

            LineRenderer outlineRenderer = planeObject.GetComponent<LineRenderer>();
            if (outlineRenderer == null)
                outlineRenderer = planeObject.AddComponent<LineRenderer>();
            outlineRenderer.sharedMaterial = horizontal ? horizontalOutlineMaterial : verticalOutlineMaterial;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.widthMultiplier = config.OutlineWidthMeters;
            // The sheet shader fades by the angle to the normal; without generated normals the line has none.
            outlineRenderer.generateLightingData = true;
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;

            visualizer = planeObject.AddComponent<ARPlaneMeshVisualizer>();
        }

        visualizer.enabled = visible;
    }
}
