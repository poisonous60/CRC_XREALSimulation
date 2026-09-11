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

        if (usePerDetectorRadiationValues)
        {
            ApplyPerDetectorRadiationValues(data);
        }
        // Temporary: every sphere shows the maximum of all live sensors until the aggregation rule is decided.
        else if (maximumValue >= 0f)
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

    // The Source Marker is not a reporting detector here; the scene's rule component owns its value.
    private void ApplyPerDetectorRadiationValues(IReadOnlyDictionary<string, float> data)
    {
        foreach (KeyValuePair<string, SourceMarker> pair in markers)
        {
            if (DetectorIdsEqual(pair.Key, sourceMarkerKey))
                continue;

            float value = data.TryGetValue(pair.Key, out float reported) ? reported : -1f;

            UpdateMarkerVisual(pair.Value, value);
            if (useCoordinateDatabase && coordinateDatabase != null)
                coordinateDatabase.UpdateRadiationValue(pair.Key, value);
        }
    }

    private float GetLatestRadiationValue(string detectorId, float fallbackValue)
    {
        if (!IsRadiationSnapshotFresh())
            return fallbackValue;

        if (usePerDetectorRadiationValues)
        {
            return radiationReceiver != null &&
                   radiationReceiver.LatestDeviceData.TryGetValue(detectorId, out float reported)
                ? reported
                : fallbackValue;
        }

        return radiationSnapshot.LatestAggregateValue >= 0f
            ? radiationSnapshot.LatestAggregateValue
            : fallbackValue;
    }
}
