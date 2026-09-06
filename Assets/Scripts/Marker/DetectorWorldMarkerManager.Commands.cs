using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    public void ClearSavedMarkers()
    {
        AbortControllerDetectorInteraction(true);

        HashSet<string> detectorIds =
            new HashSet<string>(markers.Keys, StringComparer.OrdinalIgnoreCase);

        // Include records whose anchors are still loading or failed to relocalize;
        // those IDs intentionally have no visible marker but their native XREAL map
        // files still need to be erased by a clear-all action.
        if (coordinateDatabase != null)
        {
            IReadOnlyList<DetectorCoordinateRecord> records = coordinateDatabase.GetAllRecords();
            for (int i = 0; i < records.Count; i++)
            {
                DetectorCoordinateRecord record = records[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.detectorId))
                    detectorIds.Add(record.detectorId.Trim());
            }
        }

        foreach (string detectorId in detectorIds)
        {
            if (useSpatialAnchors && spatialAnchorManager != null)
                spatialAnchorManager.EraseAnchorForDetector(detectorId);
        }

        foreach (var kvp in markers)
        {
            DestroyMarkerVisualResources(kvp.Value);
            if (kvp.Value.root != null)
                Destroy(kvp.Value.root);
        }

        markers.Clear();
        placedDetectorOrder.Clear();
        currentFollowingDetectorId = "";
        activePlacementSession = null;
        lastInteractedDetectorId = "";

        if (coordinateDatabase != null)
            coordinateDatabase.ClearAllCoordinates();
    }

    public void PrintSavedCoordinatesToLog()
    {
        if (coordinateDatabase != null)
            coordinateDatabase.LogAllCoordinates();
        else
            Debug.LogWarning("[DetectorWorldMarkerManager] No DetectorCoordinateDatabase found.");
    }

    /// <summary>
    /// Copies placed detector positions and colors for the head-locked AR HUD.
    /// The caller owns and reuses the list to avoid per-frame allocations.
    /// </summary>
    public void FillHudMarkerStates(List<DetectorHudMarkerState> output)
    {
        if (output == null)
            return;

        output.Clear();
        sortedHudDetectorIds.Clear();

        foreach (var pair in markers)
        {
            MarkerInfo marker = pair.Value;
            if (marker == null || marker.root == null || !marker.isPlaced || !marker.root.activeInHierarchy)
                continue;

            sortedHudDetectorIds.Add(marker.detectorId);
        }

        sortedHudDetectorIds.Sort(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sortedHudDetectorIds.Count; i++)
        {
            if (!markers.TryGetValue(sortedHudDetectorIds[i], out MarkerInfo marker) ||
                marker == null || marker.root == null)
            {
                continue;
            }

            Color color = GetRiskColor(marker.lastRadiationValue);
            color.a = 1f;

            output.Add(new DetectorHudMarkerState
            {
                detectorId = marker.detectorId,
                worldPosition = marker.root.transform.position,
                radiationValue = marker.lastRadiationValue,
                color = color
            });
        }
    }
}
