using System.Collections.Generic;
using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    private readonly RadiationSnapshot radiationSnapshot = new RadiationSnapshot();

    private void HandleServerConnectionChanged(bool connected)
    {
        SetServerConnectionState(connected);
    }

    private void SetServerConnectionState(bool connected)
    {
        if (!connected)
            AbortControllerDetectorInteraction(true);

        bool changed = radiationSnapshot.SetServerConnection(connected);

        foreach (KeyValuePair<string, SourceMarker> pair in markers)
            ApplyMarkerVisibility(pair.Value);

        if (changed)
        {
            Debug.Log(
                connected
                    ? "[DetectorWorldMarkerManager] Server connected; detector markers may now be shown."
                    : "[DetectorWorldMarkerManager] Server disconnected; all detector markers are hidden.");
        }
    }

    private void HandleRadiationDataReceived(IReadOnlyDictionary<string, float> data)
    {
        if (data == null)
            return;

        float maximumValue = radiationSnapshot.Ingest(data);

        // Temporary: every sphere shows the maximum of all live sensors until the aggregation rule is decided.
        if (maximumValue >= 0f)
        {
            foreach (KeyValuePair<string, SourceMarker> pair in markers)
            {
                UpdateMarkerVisual(pair.Value, maximumValue);
                if (useCoordinateDatabase && coordinateDatabase != null)
                    coordinateDatabase.UpdateRadiationValue(pair.Key, maximumValue);
            }
        }

        radiationSnapshot.RememberFreshness(IsRadiationSnapshotFresh());

        // A complete server snapshot can omit a detector that was present in the
        // preceding snapshot. Re-evaluate every marker so that detector is hidden.
        foreach (KeyValuePair<string, SourceMarker> pair in markers)
            ApplyMarkerVisibility(pair.Value);
    }

    private float GetLatestRadiationValue(string detectorId, float fallbackValue)
    {
        return IsRadiationSnapshotFresh() && radiationSnapshot.LatestAggregateValue >= 0f
            ? radiationSnapshot.LatestAggregateValue
            : fallbackValue;
    }
}
