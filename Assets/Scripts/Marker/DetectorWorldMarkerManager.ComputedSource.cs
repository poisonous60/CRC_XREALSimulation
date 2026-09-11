using System;
using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    public const string ComputedPlacementMethod = "computed";
    private const string ComputedSourceKeySeparator = "@";

    public bool TryShowComputedSource(Vector3 worldPosition, out string resultMessage)
    {
        return TryShowComputedSource(worldPosition, -1f, out resultMessage);
    }

    public bool TryShowComputedSource(Vector3 worldPosition, float radiationValue, out string resultMessage)
    {
        return TryShowComputedSourceAt("", worldPosition, radiationValue, out resultMessage);
    }

    // An empty detectorId is the single Source Marker; any other id gets its own computed Source marker.
    public bool TryShowComputedSourceAt(string detectorId, Vector3 worldPosition, float radiationValue, out string resultMessage)
    {
        SourceMarker marker = CreateOrMoveMarker(ComputedSourceKey(detectorId), worldPosition, 0f, 0f, null, true);

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
        HideComputedSourceAt("");
    }

    public void HideComputedSourceAt(string detectorId)
    {
        if (markers.TryGetValue(ComputedSourceKey(detectorId), out SourceMarker marker))
            SetMarkerRequestedVisibility(marker, false);
    }

    public bool IsSourceMarkerKey(string detectorId)
    {
        string key = NormalizeDetectorId(detectorId);

        return DetectorIdsEqual(key, sourceMarkerKey) ||
               key.StartsWith(NormalizeDetectorId(sourceMarkerKey) + ComputedSourceKeySeparator, StringComparison.OrdinalIgnoreCase);
    }

    private string ComputedSourceKey(string detectorId)
    {
        detectorId = NormalizeDetectorId(detectorId);

        return string.IsNullOrEmpty(detectorId)
            ? NormalizeDetectorId(sourceMarkerKey)
            : NormalizeDetectorId(sourceMarkerKey) + ComputedSourceKeySeparator + detectorId;
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
