using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Cancels an active placement when a tap lands on the panel background.</summary>
[DisallowMultipleComponent]
public sealed class ControllerPanelCancelArea : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("Optional. Auto-found under the controller prefab root when empty.")]
    [SerializeField] private BeamProControllerBridge bridge;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || !IsBackgroundTap(eventData.rawPointerPress))
            return;

        if (bridge == null && transform.root != null)
            bridge = transform.root.GetComponentInChildren<BeamProControllerBridge>(true);

        if (bridge == null)
            return;

        bridge.CancelPendingPlacement();
    }

    // A tap on a child widget bubbles up to this panel, so the hit graphic decides:
    // anything belonging to a button, input field or XREAL pad is not a cancel.
    private static bool IsBackgroundTap(GameObject pressedObject)
    {
        return pressedObject != null &&
               pressedObject.GetComponentInParent<Selectable>() == null &&
               pressedObject.GetComponentInParent<XREALButton>() == null;
    }
}
