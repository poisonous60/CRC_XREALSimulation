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
    public bool isControllerHovered;
    public bool isControllerMoving;
    public List<FalloffShellInfo> falloffShells;
    public Material centerMaterial;
    public ARAnchor anchor;
    public string anchorGuid;
    public string anchorState;

    public GameObject root => gameObject;

    // Anything parented to the marker after creation has to say so, or hiding the marker
    // leaves it drawn: the marker object itself is never deactivated.
    public void RegisterBodyRenderers(GameObject addedObject)
    {
        if (addedObject == null)
            return;

        Renderer[] addedRenderers = addedObject.GetComponentsInChildren<Renderer>(true);
        if (addedRenderers == null || addedRenderers.Length == 0)
            return;

        List<Renderer> merged = new List<Renderer>();
        if (bodyRenderers != null)
            merged.AddRange(bodyRenderers);

        for (int i = 0; i < addedRenderers.Length; i++)
        {
            if (addedRenderers[i] != null && !merged.Contains(addedRenderers[i]))
                merged.Add(addedRenderers[i]);
        }

        bodyRenderers = merged.ToArray();
    }
}
