using System.Collections.Generic;
using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    private readonly MarkerCoordinateStore coordinateStore = new MarkerCoordinateStore();

    private MarkerCoordinateStore CoordinateStore
    {
        get
        {
            coordinateStore.Configure(
                useCoordinateDatabase ? coordinateDatabase : null,
                roomCoordinateSystem);
            return coordinateStore;
        }
    }

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
        float radiationValueToPersist = -1f;

        if (markers.TryGetValue(NormalizeDetectorId(detectorId), out SourceMarker marker) && marker != null)
            radiationValueToPersist = marker.lastRadiationValue;

        CoordinateStore.Save(
            detectorId,
            worldPosition,
            worldRotation,
            estimatedDistance,
            qrPixelSize,
            imageCenter,
            imageWidth,
            imageHeight,
            placementMethod,
            radiationValueToPersist);
    }

    private bool HasStoredRoomPose(string detectorId)
    {
        return CoordinateStore.HasStoredRoomPose(detectorId);
    }

    private void SaveRoomCoordinateIfCalibrated(
        string detectorId,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        CoordinateStore.SaveRoomCoordinateIfCalibrated(detectorId, worldPosition, worldRotation);
    }

    private void RemoveRecordsOutsideSourceKey()
    {
        CoordinateStore.RemoveRecordsOutsideSourceKey(sourceMarkerKey);
    }

    private void LoadSavedCoordinatesWithoutAnchors()
    {
        IReadOnlyList<DetectorCoordinateRecord> records =
            CoordinateStore.GetRecordsToRestoreWithoutAnchors(useSpatialAnchors);

        for (int i = 0; i < records.Count; i++)
        {
            DetectorCoordinateRecord record = records[i];
            SourceMarker marker = CreateOrMoveMarker(
                record.detectorId,
                record.GetPosition(),
                record.estimatedDistanceMeters,
                record.qrPixelSize,
                null);

            if (marker == null)
                continue;

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

        if (records.Count > 0)
            Debug.Log($"[DetectorWorldMarkerManager] Loaded fallback markers from coordinate database: {records.Count}");
    }
}
