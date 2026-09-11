using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    /// <summary>
    /// Writes every currently materialized detector into the calibrated room frame.
    /// Hidden-by-server markers are included because visibility is unrelated to pose.
    /// </summary>
    public int CapturePlacedDetectorRoomCoordinates(RoomCoordinateSystem roomFrame)
    {
        if (roomFrame == null ||
            !roomFrame.IsCalibrated ||
            coordinateDatabase == null)
        {
            return 0;
        }

        int savedCount = 0;
        foreach (var pair in markers)
        {
            SourceMarker marker = pair.Value;
            if (marker == null || marker.root == null || !marker.isPlaced)
                continue;

            // The computed source disc is a live readout, not a placed marker. Saving it
            // would restore a source at last session's winner before any reading arrives.
            if (string.Equals(
                    marker.lastPlacementMethod,
                    ComputedPlacementMethod,
                    StringComparison.Ordinal))
            {
                continue;
            }

            // Never reinterpret a detector already assigned to a room. A room
            // re-scan refines the frame, not the detector's stored local pose.
            if (coordinateDatabase.TryGetRecord(
                    marker.detectorId,
                    out DetectorCoordinateRecord existingRecord) &&
                existingRecord != null &&
                existingRecord.HasRoomPose())
            {
                continue;
            }

            SaveRoomCoordinateIfCalibrated(
                marker.detectorId,
                marker.root.transform.position,
                marker.root.transform.rotation);
            savedCount++;
        }

        return savedCount;
    }

    /// <summary>
    /// Materializes detectors that could not be restored by their persistent XREAL
    /// anchor, using the newly calibrated ROOM_ORIGIN frame. Existing anchored
    /// markers are deliberately left untouched; the room pose is a safe fallback,
    /// not an offset applied on top of a working anchor.
    /// </summary>
    public int RestoreMissingMarkersFromRoomCoordinates(RoomCoordinateSystem roomFrame)
    {
        AbortControllerDetectorInteraction(true);

        if (roomFrame == null ||
            !roomFrame.IsCalibrated ||
            coordinateDatabase == null)
        {
            return 0;
        }

        IReadOnlyList<DetectorCoordinateRecord> records = coordinateDatabase.GetAllRecords();
        int restoredCount = 0;

        // Markers belonging to another calibrated room must not leak into the
        // active room. Remove only their runtime visuals; saved DB/anchor data is
        // kept so switching back to that room remains possible.
        List<string> markersOutsideRoom = new List<string>();
        foreach (var pair in markers)
        {
            if (!coordinateDatabase.TryGetRecord(
                    pair.Key,
                    out DetectorCoordinateRecord markerRecord) ||
                markerRecord == null ||
                !markerRecord.HasRoomPose() ||
                markerRecord.HasRoomPose(roomFrame.RoomId))
            {
                continue;
            }

            markersOutsideRoom.Add(pair.Key);
        }

        for (int i = 0; i < markersOutsideRoom.Count; i++)
        {
            string detectorId = markersOutsideRoom[i];
            if (!markers.TryGetValue(detectorId, out SourceMarker marker) || marker == null)
                continue;

            markers.Remove(detectorId);
            DestroyMarkerVisualResources(marker);
            if (marker.root != null)
                Destroy(marker.root);
        }

        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            if (record == null ||
                string.IsNullOrWhiteSpace(record.detectorId) ||
                !record.HasRoomPose(roomFrame.RoomId))
            {
                continue;
            }

            string detectorId = NormalizeDetectorId(record.detectorId);
            markers.TryGetValue(detectorId, out SourceMarker existing);
            if (existing != null && existing.root == null)
            {
                DestroyMarkerVisualResources(existing);
                markers.Remove(detectorId);
                existing = null;
            }

            Vector3 worldPosition = roomFrame.RoomToWorldPoint(record.GetRoomPosition());
            Quaternion worldRotation =
                roomFrame.RoomToWorldRotation(record.GetRoomRotation());

            SourceMarker marker = existing;
            bool newlyRestored = marker == null;
            if (newlyRestored)
            {
                marker = CreateOrMoveMarker(
                    detectorId,
                    worldPosition,
                    record.estimatedDistanceMeters,
                    record.qrPixelSize,
                    null,
                    true);
            }

            if (marker == null || marker.root == null)
                continue;

            // Room coordinates are authoritative after ROOM_ORIGIN calibration.
            // Detach from an old detector anchor so the two pose sources cannot
            // apply offsets on top of one another.
            marker.root.transform.SetParent(transform, true);
            marker.root.transform.SetPositionAndRotation(worldPosition, worldRotation);
            marker.savedPosition = worldPosition;
            marker.lastPlacementImagePoint = record.GetQrImageCenter();
            marker.lastImageWidth = record.qrImageWidth;
            marker.lastImageHeight = record.qrImageHeight;
            marker.lastPlacementMethod = "RoomCoordinateFallback";
            marker.anchorState = "room coordinate restored";
            marker.anchor = null;
            marker.isFollowingPlacementOrigin = false;
            marker.isPlaced = true;

            float restoredRadiationValue = record.lastRadiationValue >= 0f
                ? record.lastRadiationValue
                : marker.lastRadiationValue;
            UpdateMarkerVisual(
                marker,
                GetLatestRadiationValue(detectorId, restoredRadiationValue));
            if (newlyRestored)
                restoredCount++;
        }

        if (restoredCount > 0)
        {
            Debug.Log(
                $"[DetectorWorldMarkerManager] Restored {restoredCount} detector(s) " +
                $"from room coordinates for {roomFrame.RoomId}.");
        }

        return restoredCount;
    }

    /// <summary>
    /// Drops only session-world visuals after an XR tracking reset. Persistent room
    /// coordinates and LIFO placement order remain intact and are materialized again
    /// after ROOM_ORIGIN is calibrated in the new session frame.
    /// </summary>
    public void InvalidateRoomLocalization(string reason)
    {
        AbortControllerDetectorInteraction(true);
        RollbackActivePlacementSession();

        foreach (var pair in markers)
        {
            SourceMarker marker = pair.Value;
            if (marker == null)
                continue;

            SetMarkerRequestedVisibility(marker, false);
            DestroyMarkerVisualResources(marker);
            if (marker.root != null)
                Destroy(marker.root);
        }

        markers.Clear();
        currentFollowingDetectorId = "";
        activePlacementSession = null;
        lastInteractedDetectorId = "";

        radiationSnapshot.Reset();

        Debug.LogWarning(
            $"[DetectorWorldMarkerManager] Room localization invalidated; " +
            $"runtime detector visuals were cleared. {reason}");
    }
}
