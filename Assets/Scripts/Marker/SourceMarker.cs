using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// One placed or previewed source marker. Lives on the marker object itself.
/// </summary>
[DisallowMultipleComponent]
public class SourceMarker : MonoBehaviour
{
    public string detectorId;
    public SourceMarkerVisual visual;
    public Renderer renderer;
    public Renderer[] bodyRenderers;
    public bool[] bodyRendererDefaults;
    public TMP_Text label;
    public Vector3 savedPosition;
    public float lastRadiationValue;
    public float lastEstimatedDistance;
    public float lastQrPixelSize;
    public Vector2 lastPlacementImagePoint;
    public int lastImageWidth;
    public int lastImageHeight;
    public string lastPlacementMethod;
    public bool isFollowingPlacementOrigin;
    public bool isPlaced;
    public bool hasValidPlaneHit;
    public bool visibilityRequested;
    public bool isVisible;
    public bool centerVisualRequested;
    public bool centerDrawRequested;
    public GameObject placementVisual;
    public bool isControllerHovered;
    public bool isControllerMoving;
    public List<FalloffShellInfo> falloffShells;
    public Material centerMaterial;
    public ARAnchor anchor;
    public string anchorGuid;
    public string anchorState;

    public GameObject root => gameObject;

    // Deactivating the object used to preserve each renderer's own enabled flag. The object
    // now stays active, so that flag is recorded here and honored when the marker is shown.
    public void CaptureBodyRenderers(GameObject markerRoot)
    {
        if (markerRoot == null)
            return;

        bodyRenderers = markerRoot.GetComponentsInChildren<Renderer>(true);
        bodyRendererDefaults = new bool[bodyRenderers.Length];

        for (int i = 0; i < bodyRenderers.Length; i++)
            bodyRendererDefaults[i] = bodyRenderers[i] != null && bodyRenderers[i].enabled;
    }

    // Anything parented to the marker after creation has to say so, or hiding the marker
    // leaves it drawn.
    public void RegisterBodyRenderers(GameObject addedObject)
    {
        if (addedObject == null)
            return;

        Renderer[] addedRenderers = addedObject.GetComponentsInChildren<Renderer>(true);
        if (addedRenderers == null || addedRenderers.Length == 0)
            return;

        List<Renderer> merged = new List<Renderer>();
        List<bool> mergedDefaults = new List<bool>();

        if (bodyRenderers != null)
        {
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                merged.Add(bodyRenderers[i]);
                mergedDefaults.Add(
                    bodyRendererDefaults != null && i < bodyRendererDefaults.Length
                        ? bodyRendererDefaults[i]
                        : true);
            }
        }

        for (int i = 0; i < addedRenderers.Length; i++)
        {
            Renderer added = addedRenderers[i];
            if (added == null || merged.Contains(added))
                continue;

            merged.Add(added);
            mergedDefaults.Add(added.enabled);
            added.enabled = isVisible && added.enabled;
        }

        bodyRenderers = merged.ToArray();
        bodyRendererDefaults = mergedDefaults.ToArray();
    }
}
