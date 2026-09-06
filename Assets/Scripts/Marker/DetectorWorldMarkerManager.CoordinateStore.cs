using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private void SaveCoordinate(
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float estimatedDistance,
        float qrPixelSize,
        Vector2 imageCenter,
        int imageWidth,
        int imageHeight,
        string placementMethod)
    {
        if (useCoordinateDatabase && coordinateDatabase != null)
        {
            coordinateDatabase.SaveOrUpdateCoordinate(
                detectorId,
                worldPosition,
                worldRotation,
                estimatedDistance,
                qrPixelSize,
                imageCenter,
                imageWidth,
                imageHeight,
                placementMethod
            );

            SaveRoomCoordinateIfCalibrated(
                detectorId,
                worldPosition,
                worldRotation);

            // A server snapshot may have arrived before this detector's first
            // coordinate record existed. Persist the value used by the marker now
            // so an app restart does not fall back to an unknown/hidden reading.
            string normalizedDetectorId = NormalizeDetectorId(detectorId);
            if (markers.TryGetValue(normalizedDetectorId, out SourceMarker marker) &&
                marker != null &&
                !float.IsNaN(marker.lastRadiationValue) &&
                !float.IsInfinity(marker.lastRadiationValue) &&
                marker.lastRadiationValue >= 0f)
            {
                coordinateDatabase.UpdateRadiationValue(
                    normalizedDetectorId,
                    marker.lastRadiationValue);
            }
        }
    }

    private bool HasStoredRoomPose(string detectorId)
    {
        return coordinateDatabase != null &&
               coordinateDatabase.TryGetRecord(detectorId, out DetectorCoordinateRecord record) &&
               record != null &&
               record.HasRoomPose();
    }

    private void SaveRoomCoordinateIfCalibrated(
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        if (!useCoordinateDatabase ||
            coordinateDatabase == null ||
            roomCoordinateSystem == null ||
            !roomCoordinateSystem.IsCalibrated)
        {
            return;
        }

        float calibrationFactor = 1f;
        if (coordinateDatabase.TryGetRecord(detectorId, out DetectorCoordinateRecord record) &&
            record != null &&
            record.calibrationFactor > 0f &&
            !float.IsNaN(record.calibrationFactor) &&
            !float.IsInfinity(record.calibrationFactor))
        {
            calibrationFactor = record.calibrationFactor;
        }

        coordinateDatabase.SaveOrUpdateRoomCoordinate(
            detectorId,
            roomCoordinateSystem.RoomId,
            roomCoordinateSystem.WorldToRoomPoint(worldPosition),
            roomCoordinateSystem.WorldToRoomRotation(worldRotation),
            calibrationFactor);
    }

    private void RemoveRecordsOutsideSourceKey()
    {
        if (!useCoordinateDatabase || coordinateDatabase == null)
            return;

        string sourceKey = NormalizeDetectorId(sourceMarkerKey);
        if (string.IsNullOrEmpty(sourceKey))
            return;

        List<string> staleIds = new List<string>();
        IReadOnlyList<DetectorCoordinateRecord> records = coordinateDatabase.GetAllRecords();
        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            if (record != null &&
                !string.IsNullOrWhiteSpace(record.detectorId) &&
                !DetectorIdsEqual(record.detectorId, sourceKey))
            {
                staleIds.Add(record.detectorId);
            }
        }

        for (int i = 0; i < staleIds.Count; i++)
            coordinateDatabase.RemoveCoordinate(staleIds[i]);

        if (staleIds.Count > 0)
            Debug.LogWarning($"[DetectorWorldMarkerManager] Removed {staleIds.Count} saved record(s) keyed by sticker text; only '{sourceKey}' is kept.");
    }

    private void LoadSavedCoordinatesWithoutAnchors()
    {
        if (useCoordinateDatabase && coordinateDatabase != null)
        {
            IReadOnlyList<DetectorCoordinateRecord> records = coordinateDatabase.GetAllRecords();

            if (useSpatialAnchors)
            {
                int legacyCoordinateCount = 0;
                for (int i = 0; i < records.Count; i++)
                {
                    DetectorCoordinateRecord record = records[i];
                    if (record != null &&
                        !string.IsNullOrWhiteSpace(record.detectorId) &&
                        !record.HasSavedAnchor())
                    {
                        legacyCoordinateCount++;
                    }
                }

                if (legacyCoordinateCount > 0)
                {
                    Debug.LogWarning(
                        $"[DetectorWorldMarkerManager] {legacyCoordinateCount} saved detector coordinate(s) " +
                        "have no persistent spatial anchor and will not be restored at a misleading session-space pose. " +
                        "Scan and place them once to create anchors.");
                }

                // Records with GUIDs are materialized only by HandleAnchorLoaded.
                // This avoids a coordinate-fallback blink or jump before XREAL
                // finishes relocalizing the persisted local anchor.
                return;
            }

            for (int i = 0; i < records.Count; i++)
            {
                DetectorCoordinateRecord record = records[i];
                if (record == null || string.IsNullOrWhiteSpace(record.detectorId))
                    continue;

                if (record.HasRoomPose())
                {
                    // A room-local coordinate has no valid Unity-world pose until
                    // the user localizes ROOM_ORIGIN in this session.
                    continue;
                }

                SourceMarker marker = CreateOrMoveMarker(record.detectorId, record.GetPosition(), record.estimatedDistanceMeters, record.qrPixelSize, null);

                if (marker != null)
                {
                    marker.root.transform.rotation = record.GetRotation();
                    marker.anchorState = record.placementMethod;
                    marker.isFollowingPlacementOrigin = false;
                    marker.isPlaced = true;

                    float restoredRadiationValue = record.lastRadiationValue >= 0f
                        ? record.lastRadiationValue
                        : marker.lastRadiationValue;
                    UpdateMarkerVisual(
                        marker,
                        GetLatestRadiationValue(record.detectorId, restoredRadiationValue));
                }
            }

            Debug.Log($"[DetectorWorldMarkerManager] Loaded fallback markers from coordinate database: {records.Count}");
        }
    }
}
