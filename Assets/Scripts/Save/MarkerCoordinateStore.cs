using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reads and writes marker coordinates in the database, in world and in room space.
/// </summary>
public class MarkerCoordinateStore
{
    private DetectorCoordinateDatabase database;
    private RoomCoordinateSystem roomCoordinateSystem;

    public void Configure(DetectorCoordinateDatabase coordinateDatabase, RoomCoordinateSystem roomFrame)
    {
        database = coordinateDatabase;
        roomCoordinateSystem = roomFrame;
    }

    public void Save(
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation,
        float estimatedDistance,
        float qrPixelSize,
        Vector2 imageCenter,
        int imageWidth,
        int imageHeight,
        string placementMethod,
        float radiationValueToPersist)
    {
        if (database == null)
            return;

        database.SaveOrUpdateCoordinate(
            detectorId,
            worldPosition,
            worldRotation,
            estimatedDistance,
            qrPixelSize,
            imageCenter,
            imageWidth,
            imageHeight,
            placementMethod);

        SaveRoomCoordinateIfCalibrated(detectorId, worldPosition, worldRotation);

        // A server snapshot may have arrived before this detector's first
        // coordinate record existed. Persist the value used by the marker now
        // so an app restart does not fall back to an unknown/hidden reading.
        if (radiationValueToPersist >= 0f &&
            !float.IsNaN(radiationValueToPersist) &&
            !float.IsInfinity(radiationValueToPersist))
        {
            database.UpdateRadiationValue(DetectorId.Normalize(detectorId), radiationValueToPersist);
        }
    }

    public bool HasStoredRoomPose(string detectorId)
    {
        return database != null &&
               database.TryGetRecord(detectorId, out DetectorCoordinateRecord record) &&
               record != null &&
               record.HasRoomPose();
    }

    public void SaveRoomCoordinateIfCalibrated(
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        if (database == null ||
            roomCoordinateSystem == null ||
            !roomCoordinateSystem.IsCalibrated)
        {
            return;
        }

        float calibrationFactor = 1f;
        if (database.TryGetRecord(detectorId, out DetectorCoordinateRecord record) &&
            record != null &&
            record.calibrationFactor > 0f &&
            !float.IsNaN(record.calibrationFactor) &&
            !float.IsInfinity(record.calibrationFactor))
        {
            calibrationFactor = record.calibrationFactor;
        }

        database.SaveOrUpdateRoomCoordinate(
            detectorId,
            roomCoordinateSystem.RoomId,
            roomCoordinateSystem.WorldToRoomPoint(worldPosition),
            roomCoordinateSystem.WorldToRoomRotation(worldRotation),
            calibrationFactor);
    }

    public void RemoveRecordsOutsideSourceKey(string sourceMarkerKey)
    {
        if (database == null)
            return;

        string sourceKey = DetectorId.Normalize(sourceMarkerKey);
        if (string.IsNullOrEmpty(sourceKey))
            return;

        List<string> staleIds = new List<string>();
        IReadOnlyList<DetectorCoordinateRecord> records = database.GetAllRecords();
        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            if (record != null &&
                !string.IsNullOrWhiteSpace(record.detectorId) &&
                !DetectorId.Equals(record.detectorId, sourceKey))
            {
                staleIds.Add(record.detectorId);
            }
        }

        for (int i = 0; i < staleIds.Count; i++)
            database.RemoveCoordinate(staleIds[i]);

        if (staleIds.Count > 0)
            Debug.LogWarning($"[MarkerCoordinateStore] Removed {staleIds.Count} saved record(s) keyed by sticker text; only '{sourceKey}' is kept.");
    }

    public IReadOnlyList<DetectorCoordinateRecord> GetRecordsToRestoreWithoutAnchors(bool useSpatialAnchors)
    {
        List<DetectorCoordinateRecord> restorable = new List<DetectorCoordinateRecord>();

        if (database == null)
            return restorable;

        IReadOnlyList<DetectorCoordinateRecord> records = database.GetAllRecords();

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
                    $"[MarkerCoordinateStore] {legacyCoordinateCount} saved detector coordinate(s) " +
                    "have no persistent spatial anchor and will not be restored at a misleading session-space pose. " +
                    "Scan and place them once to create anchors.");
            }

            // Records with GUIDs are materialized only by HandleAnchorLoaded.
            // This avoids a coordinate-fallback blink or jump before XREAL
            // finishes relocalizing the persisted local anchor.
            return restorable;
        }

        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            if (record == null || string.IsNullOrWhiteSpace(record.detectorId))
                continue;

            // A room-local coordinate has no valid Unity-world pose until
            // the user localizes ROOM_ORIGIN in this session.
            if (record.HasRoomPose())
                continue;

            restorable.Add(record);
        }

        return restorable;
    }
}
