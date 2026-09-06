using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Ties a marker to an XREAL spatial anchor, or detaches it back to the room frame.
/// </summary>
public static class MarkerAnchorBinder
{
    public static bool TryCreateAnchor(
        DetectorSpatialAnchorManager anchorManager,
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        if (anchorManager != null && anchorManager.IsReady())
        {
            anchorManager.CreateAndSaveAnchorForDetector(detectorId, worldPosition, worldRotation);
            return true;
        }

        Debug.LogWarning("[MarkerAnchorBinder] Spatial anchor not created. Manager or ARAnchorManager missing; using coordinate fallback.");
        return false;
    }

    public static void BindToAnchor(
        SourceMarker marker,
        ARAnchor anchor,
        string persistentGuid,
        string anchorState,
        bool parentMarkerToAnchor)
    {
        if (marker == null || anchor == null)
            return;

        marker.anchor = anchor;
        marker.anchorGuid = persistentGuid;
        marker.anchorState = anchorState;
        marker.savedPosition = anchor.transform.position;
        marker.isFollowingPlacementOrigin = false;
        marker.isPlaced = true;

        if (marker.root == null)
            return;

        marker.root.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);

        if (!parentMarkerToAnchor)
            return;

        marker.root.transform.SetParent(anchor.transform, false);
        marker.root.transform.localPosition = Vector3.zero;
        marker.root.transform.localRotation = Quaternion.identity;
    }

    public static void DetachToRoom(SourceMarker marker, Transform roomParent, string anchorState)
    {
        if (marker == null)
            return;

        if (marker.root != null)
            marker.root.transform.SetParent(roomParent, true);

        marker.anchor = null;
        marker.anchorState = anchorState;
    }
}
