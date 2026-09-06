using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private void HandleServerConnectionChanged(bool connected)
    {
        SetServerConnectionState(connected);
    }

    private void SetServerConnectionState(bool connected)
    {
        if (!connected)
            AbortControllerDetectorInteraction(true);

        bool changed = serverConnected != connected;
        serverConnected = connected;

        if (changed)
        {
            // A snapshot from the previous WebSocket generation must never make
            // restored detector values look live on the replacement connection.
            hasReceivedRadiationSnapshot = false;
            lastSnapshotFreshnessState = false;
            lastRadiationSnapshotTime = float.NegativeInfinity;
            liveRadiationDetectorIds.Clear();
            latestAggregateRadiationValue = -1f;
        }

        foreach (var pair in markers)
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

        hasReceivedRadiationSnapshot = true;
        lastRadiationSnapshotTime = Time.unscaledTime;
        liveRadiationDetectorIds.Clear();

        float maximumValue = -1f;
        foreach (var kvp in data)
        {
            string detectorId = NormalizeDetectorId(kvp.Key);

            if (string.IsNullOrEmpty(detectorId) ||
                kvp.Value < 0f ||
                float.IsNaN(kvp.Value) ||
                float.IsInfinity(kvp.Value))
            {
                continue;
            }

            liveRadiationDetectorIds.Add(detectorId);
            if (kvp.Value > maximumValue)
                maximumValue = kvp.Value;
        }

        latestAggregateRadiationValue = maximumValue;

        // Temporary: every sphere shows the maximum of all live sensors until the aggregation rule is decided.
        if (maximumValue >= 0f)
        {
            foreach (var pair in markers)
            {
                UpdateMarkerVisual(pair.Value, maximumValue);
                if (useCoordinateDatabase && coordinateDatabase != null)
                    coordinateDatabase.UpdateRadiationValue(pair.Key, maximumValue);
            }
        }

        lastSnapshotFreshnessState = IsRadiationSnapshotFresh();

        // A complete server snapshot can omit a detector that was present in the
        // preceding snapshot. Re-evaluate every marker so that detector is hidden.
        foreach (var pair in markers)
            ApplyMarkerVisibility(pair.Value);
    }

    private float GetLatestRadiationValue(string detectorId, float fallbackValue)
    {
        return IsRadiationSnapshotFresh() && latestAggregateRadiationValue >= 0f
            ? latestAggregateRadiationValue
            : fallbackValue;
    }
}
