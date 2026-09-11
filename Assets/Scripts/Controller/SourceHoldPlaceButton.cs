using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Holds the Source preview on the gaze center while pressed, fixes it on release.</summary>
public class SourceHoldPlaceButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [Tooltip("Optional. Auto-found on the controller prefab root when empty.")]
    [SerializeField] private BeamProControllerBridge controllerBridge;

    [Header("Target")]
    [Tooltip("Detector ID this button places. Empty places the Source Marker through the scan-or-place flow.")]
    [SerializeField] private string detectorId = "";

    private bool isHeld;

    public void SetDetectorId(string id)
    {
        detectorId = id;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!ResolveBridge())
            return;

        isHeld = true;

        if (controllerBridge.HasPendingPlacement)
            return;

        if (string.IsNullOrEmpty(detectorId))
            controllerBridge.StartQrScan();
        else
            controllerBridge.StartDetectorPlacement(detectorId);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        ReleaseHold();
    }

    private void OnDisable()
    {
        ReleaseHold();
    }

    private void ReleaseHold()
    {
        if (!isHeld)
            return;

        isHeld = false;

        if (!ResolveBridge() || !controllerBridge.HasPendingPlacement)
            return;

        controllerBridge.PlaceDetector();
    }

    private bool ResolveBridge()
    {
        if (controllerBridge != null)
            return true;

        Transform searchRoot = transform.root;
        if (searchRoot != null)
            controllerBridge = searchRoot.GetComponentInChildren<BeamProControllerBridge>(true);

        if (controllerBridge == null)
            Debug.LogWarning("[SourceHoldPlaceButton] BeamProControllerBridge not found. Hold placement is disabled.");

        return controllerBridge != null;
    }
}
