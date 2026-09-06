using System.Collections.Generic;

public partial class DetectorWorldMarkerManager
{
    private readonly MarkerVisibilityPolicy visibilityPolicy = new MarkerVisibilityPolicy();

    private MarkerVisibilityPolicy VisibilityPolicy
    {
        get
        {
            visibilityPolicy.hideUntilServerConnected = hideMarkersUntilServerConnected;
            visibilityPolicy.hideWithoutFreshRadiationData = hideMarkersWithoutFreshRadiationData;
            visibilityPolicy.showPreviewBeforeServerConnected = showPreviewBeforeServerConnected;
            visibilityPolicy.showGrayPreviewSphere = showGrayPreviewSphere;
            visibilityPolicy.requireRoomCalibration = requireRoomCalibrationBeforeDetectorPlacement;
            visibilityPolicy.roomCoordinateSystemEnabled = enableRoomCoordinateSystem;
            visibilityPolicy.showLabel = showLabel;
            visibilityPolicy.maximumSnapshotAgeSeconds = maximumRadiationSnapshotAgeSeconds;
            return visibilityPolicy;
        }
    }

    private bool IsPreviewMarker(SourceMarker marker)
    {
        return visibilityPolicy.IsPreview(marker);
    }

    private void ForceMarkerVisible(SourceMarker marker)
    {
        SetMarkerRequestedVisibility(marker, true);
    }

    private void SetMarkerRequestedVisibility(SourceMarker marker, bool visible)
    {
        if (marker == null)
            return;

        marker.visibilityRequested = visible;
        ApplyMarkerVisibility(marker);
    }

    private void ApplyMarkerVisibility(SourceMarker marker)
    {
        VisibilityPolicy.Apply(marker, roomCoordinateSystem, radiationSnapshot);
    }

    private bool IsRadiationSnapshotFresh()
    {
        return radiationSnapshot.IsFresh(maximumRadiationSnapshotAgeSeconds);
    }

    private void RefreshRadiationSnapshotVisibilityIfExpired()
    {
        bool snapshotIsFresh = IsRadiationSnapshotFresh();
        if (snapshotIsFresh == radiationSnapshot.LastFreshnessState)
            return;

        radiationSnapshot.RememberFreshness(snapshotIsFresh);
        foreach (KeyValuePair<string, SourceMarker> pair in markers)
            ApplyMarkerVisibility(pair.Value);
    }
}
