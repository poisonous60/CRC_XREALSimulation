using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    public bool TryShowComputedSource(Vector3 worldPosition, out string resultMessage)
    {
        SourceMarker marker = CreateOrMoveMarker(sourceMarkerKey, worldPosition, 0f, 0f, null, true);

        if (marker == null || marker.root == null)
        {
            resultMessage = "Could not create the computed source marker.";
            return false;
        }

        marker.isPlaced = true;
        marker.isFollowingPlacementOrigin = false;
        marker.lastPlacementMethod = "computed";
        marker.anchorState = "computed";
        ApplyMarkerVisibility(marker);

        resultMessage = "";
        return true;
    }

    public void HideComputedSource()
    {
        if (markers.TryGetValue(NormalizeDetectorId(sourceMarkerKey), out SourceMarker marker))
            SetMarkerRequestedVisibility(marker, false);
    }
}
