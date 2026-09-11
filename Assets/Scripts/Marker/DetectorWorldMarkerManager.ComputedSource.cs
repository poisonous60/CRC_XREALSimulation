using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    public const string ComputedPlacementMethod = "computed";

    public bool TryShowComputedSource(Vector3 worldPosition, out string resultMessage)
    {
        return TryShowComputedSource(worldPosition, -1f, out resultMessage);
    }

    public bool TryShowComputedSource(Vector3 worldPosition, float radiationValue, out string resultMessage)
    {
        SourceMarker marker = CreateOrMoveMarker(sourceMarkerKey, worldPosition, 0f, 0f, null, true);

        if (marker == null || marker.root == null)
        {
            resultMessage = "Could not create the computed source marker.";
            return false;
        }

        marker.isPlaced = true;
        marker.isFollowingPlacementOrigin = false;
        marker.lastPlacementMethod = ComputedPlacementMethod;
        marker.anchorState = ComputedPlacementMethod;

        if (radiationValue >= 0f)
            UpdateMarkerVisual(marker, radiationValue);

        ApplyMarkerVisibility(marker);

        resultMessage = "";
        return true;
    }

    public void HideComputedSource()
    {
        if (markers.TryGetValue(NormalizeDetectorId(sourceMarkerKey), out SourceMarker marker))
            SetMarkerRequestedVisibility(marker, false);
    }

    public bool TryGetPlacedMarkerPosition(string detectorId, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!markers.TryGetValue(NormalizeDetectorId(detectorId), out SourceMarker marker) ||
            marker == null ||
            marker.root == null ||
            !marker.isPlaced)
        {
            return false;
        }

        worldPosition = marker.root.transform.position;
        return true;
    }
}
