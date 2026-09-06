using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private bool CreateAnchorForMarker(string detectorId, Vector3 worldPosition, Quaternion worldRotation)
    {
        EnsureSpatialAnchorManager();
        SubscribeSpatialEvents();

        return MarkerAnchorBinder.TryCreateAnchor(
            spatialAnchorManager,
            detectorId,
            worldPosition,
            worldRotation);
    }

    private void FinalizeSpatialBinding(
        string detectorId,
        SourceMarker marker,
        Vector3 worldPosition,
        Quaternion worldRotation,
        bool forceCreateSpatialAnchor = false)
    {
        if (marker == null)
            return;


        // Once a stable room pose exists, ROOM_ORIGIN is the single authoritative
        // reference frame. Do not create a second detector-local pose source.
        if (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated)
        {
            if (spatialAnchorManager != null)
                spatialAnchorManager.InvalidatePendingOperationForDetector(detectorId);

            MarkerAnchorBinder.DetachToRoom(marker, transform, "room coordinate saved");
            return;
        }

        if (!useSpatialAnchors)
            return;

        // Detach the marker while the replacement anchor is being created. The
        // spatial manager keeps the previous persisted anchor until the new save
        // succeeds, so a transient mapping failure cannot destroy the last known
        // physical-space restoration point.
        if (marker.root != null)
            marker.root.transform.SetParent(transform, true);

        EnsureSpatialAnchorManager();
        if (spatialAnchorManager != null)
            spatialAnchorManager.InvalidatePendingOperationForDetector(detectorId);

        marker.anchor = null;

        if ((createSpatialAnchorOnQr || forceCreateSpatialAnchor) &&
            CreateAnchorForMarker(detectorId, worldPosition, worldRotation))
        {
            marker.anchorState = "anchor saving...";
        }
        else
        {
            marker.anchorState = $"{placedStateLabel} (anchor unavailable)";
        }
    }

    private void HandleAnchorCreatedAndSaved(string detectorId, ARAnchor anchor, string persistentGuid)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (anchor == null || IsActivePlacementForDetector(detectorId))
            return;

        if (HasStoredRoomPose(detectorId))
        {
            if (markers.TryGetValue(detectorId, out SourceMarker roomMarker) &&
                roomMarker != null)
            {
                roomMarker.anchor = null;
                roomMarker.anchorState = "room coordinate saved";
            }

            return;
        }

        if (!markers.TryGetValue(detectorId, out SourceMarker marker) || marker == null)
            return;

        MarkerAnchorBinder.BindToAnchor(marker, anchor, persistentGuid, "anchor saved", parentMarkerToAnchor);

        UpdateMarkerVisual(marker, marker.lastRadiationValue);
        Debug.Log($"[DetectorWorldMarkerManager] Anchor created and saved: {detectorId}, {persistentGuid}");
    }

    private void HandleAnchorLoaded(string detectorId, ARAnchor anchor, string persistentGuid)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (anchor == null || IsActivePlacementForDetector(detectorId))
            return;

        // A room-relative record is restored only after ROOM_ORIGIN is localized.
        // Loading its older detector-local anchor here would create a startup ghost
        // and later fight the authoritative room transform.
        if (HasStoredRoomPose(detectorId))
            return;

        SourceMarker marker = CreateOrMoveMarker(
            detectorId,
            anchor.transform.position,
            0f,
            0f,
            anchor.transform,
            true);
        if (marker == null)
            return;

        MarkerAnchorBinder.BindToAnchor(marker, anchor, persistentGuid, "anchor loaded", parentMarkerToAnchor);

        float restoredRadiationValue = marker.lastRadiationValue;
        if (coordinateDatabase != null &&
            coordinateDatabase.TryGetRecord(detectorId, out DetectorCoordinateRecord record) &&
            record.lastRadiationValue >= 0f)
        {
            restoredRadiationValue = record.lastRadiationValue;
        }

        // The room may have been calibrated while this asynchronous legacy anchor
        // was still loading. Capture it now so it is not omitted from the estimator
        // or the next room-relative restoration, then detach the visual from the
        // legacy per-detector pose source.
        if (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated)
        {
            SaveRoomCoordinateIfCalibrated(
                detectorId,
                anchor.transform.position,
                anchor.transform.rotation);

            MarkerAnchorBinder.DetachToRoom(marker, transform, "room coordinate captured");
        }

        UpdateMarkerVisual(
            marker,
            GetLatestRadiationValue(detectorId, restoredRadiationValue));
    }

    private void HandleAnchorSaveFailed(string detectorId, string message)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (IsActivePlacementForDetector(detectorId))
            return;

        if (markers.TryGetValue(detectorId, out SourceMarker marker))
        {
            marker.anchorState = "anchor save failed";
            UpdateLabel(marker, marker.lastRadiationValue, false);
        }

        Debug.LogWarning($"[DetectorWorldMarkerManager] Anchor save failed for {detectorId}: {message}");
    }

    private void HandleAnchorLoadFailed(string detectorId, string message)
    {
        detectorId = NormalizeDetectorId(detectorId);

        if (IsActivePlacementForDetector(detectorId))
            return;

        if (HasStoredRoomPose(detectorId))
        {
            Debug.LogWarning(
                $"[DetectorWorldMarkerManager] Legacy anchor load failed for {detectorId}; " +
                $"ROOM_ORIGIN calibration will restore its room-relative pose. {message}");
            return;
        }

        // A serialized Unity world pose is session-relative and is not the same
        // physical location after an app restart. Do not show that coordinate as a
        // fallback; it creates the startup "ghost" sphere and can visibly jump once
        // relocalization completes.
        Debug.LogWarning(
            $"[DetectorWorldMarkerManager] Anchor load failed for {detectorId}; " +
            $"the detector stays hidden until it is placed again. {message}");
    }

    private bool IsActivePlacementForDetector(string detectorId)
    {
        bool activePlacement =
            activePlacementSession != null &&
            DetectorIdsEqual(activePlacementSession.detectorId, detectorId);
        bool activeControllerMove =
            activeDetectorMoveSession != null &&
            activeDetectorMoveSession.marker != null &&
            DetectorIdsEqual(
                activeDetectorMoveSession.marker.detectorId,
                detectorId);
        return activePlacement || activeControllerMove;
    }
}
