using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{
    private readonly PlacementGeometry placementGeometry = new PlacementGeometry();

    private void HandleQrDetected(string qrText, Vector2 imageCenter, int imageWidth, int imageHeight, float qrPixelSize)
    {
        // The printed ROOM_ORIGIN sheet is read by the QR scanner as well as the image
        // tracker, and a sighting of it here cancels the placement the operator is aiming.
        if (HasActivePlacement || HasActiveDetectorMove)
            return;

        AbortControllerDetectorInteraction(true);

        // ROOM_ORIGIN is a coordinate-frame command, not a radiation detector ID.
        // RoomCoordinateSystem owns its preview/confirmation transaction.
        if (RoomCoordinateSystem.IsRoomOriginCode(qrText))
        {
            CancelActivePlacementOnly();
            return;
        }

        if (roomCoordinateSystem != null)
            roomCoordinateSystem.CancelPendingPlacementForDetectorScan();

        if (!IsRoomReadyForPlacement())
        {
            CancelActivePlacementOnly();
            Debug.LogWarning(
                "[DetectorWorldMarkerManager] Detector QR ignored until ROOM_ORIGIN " +
                "is scanned and placed on its vertical wall.");
            RoomCoordinateSystem.PublishStatus(
                "Place ROOM_ORIGIN before scanning detectors",
                Color.yellow);
            return;
        }

        BeginSourcePlacement(sourceMarkerKey, imageCenter, imageWidth, imageHeight, qrPixelSize);
    }

    public bool TryBeginSourcePlacement(out string resultMessage)
    {
        return TryBeginSourcePlacement(sourceMarkerKey, out resultMessage);
    }

    public bool TryBeginSourcePlacement(string detectorId, out string resultMessage)
    {
        AbortControllerDetectorInteraction(true);

        if (roomCoordinateSystem != null)
            roomCoordinateSystem.CancelPendingPlacementForDetectorScan();

        string placementName = DetectorIdsEqual(detectorId, sourceMarkerKey)
            ? "the Source"
            : NormalizeDetectorId(detectorId);

        if (!IsRoomReadyForPlacement())
        {
            resultMessage = $"Place ROOM_ORIGIN before adding {placementName}";
            return false;
        }

        if (string.IsNullOrEmpty(NormalizeDetectorId(detectorId)))
        {
            resultMessage = "Source Marker key is empty";
            return false;
        }

        bool started = BeginSourcePlacement(detectorId, Vector2.zero, 0, 0, 0f);
        resultMessage = !started
            ? $"Placement of {placementName} did not start"
            : followPreviewCenterUntilPlaced
                ? $"Aim the glasses at {placementName}, then release"
                : $"{placementName} placed ahead of the glasses";
        return started;
    }

    private bool BeginSourcePlacement(
        string requestedDetectorId,
        Vector2 imageCenter,
        int imageWidth,
        int imageHeight,
        float qrPixelSize)
    {
        string detectorId = NormalizeDetectorId(requestedDetectorId);
        if (string.IsNullOrEmpty(detectorId))
            return false;

        // A new scan starts a new transaction; a later Cancel must never fall
        // through to a detector committed before this scan.
        lastInteractedDetectorId = "";

        if (!updateExistingMarkerOnRescan &&
            markers.TryGetValue(detectorId, out SourceMarker existingMarker) &&
            existingMarker != null && existingMarker.root != null && existingMarker.isPlaced)
        {
            // The QR scanner has already switched out of scan mode. End any other
            // pending transaction too, otherwise Place would act on the wrong ID.
            RollbackActivePlacementSession();
            Debug.Log($"[DetectorWorldMarkerManager] Rescan ignored because updating existing markers is disabled: {existingMarker.detectorId}");
            return false;
        }

        if (followPreviewCenterUntilPlaced)
        {
            detectorId = BeginPlacementSession(detectorId);
        }
        else
        {
            // Immediate-placement mode cannot share state with a pending preview.
            // Restore/remove that preview before touching the incoming detector.
            RollbackActivePlacementSession();
        }

        Vector2 placementImagePoint = imageCenter;
        string placementMethod = "QrImageCenterProjection";

        if (usePreviewCenterPlacement)
        {
            placementImagePoint = new Vector2(imageWidth * 0.5f, imageHeight * 0.5f);
            placementMethod = "PreviewCenterProjection";
        }

        float estimatedDistance = GetPlacementDistance(imageWidth, qrPixelSize);
        Vector3 worldPosition = CalculateWorldPosition(placementImagePoint, imageWidth, imageHeight, estimatedDistance);
        Quaternion worldRotation = CalculateMarkerRotation(worldPosition);

        SourceMarker marker = CreateOrMoveMarker(detectorId, worldPosition, estimatedDistance, qrPixelSize, null);
        if (marker == null)
            return false;

        marker.lastPlacementImagePoint = placementImagePoint;
        marker.lastImageWidth = imageWidth;
        marker.lastImageHeight = imageHeight;
        marker.lastPlacementMethod = placementMethod;
        marker.lastEstimatedDistance = estimatedDistance;

        if (followPreviewCenterUntilPlaced)
        {
            currentFollowingDetectorId = marker.detectorId;
            marker.isFollowingPlacementOrigin = true;
            marker.isPlaced = false;
            marker.anchorState = followingStateLabel;
            marker.lastEstimatedDistance = defaultPlacementDistanceMeters;

            if (marker.root != null)
            {
                SetMarkerRequestedVisibility(marker, true);
            }

            UpdateMarkerVisual(marker, marker.lastRadiationValue);
            UpdateFollowingMarkerPosition(marker);

            string previewMode = usePlaneIntersectionPlacement
                ? "gaze-center plane intersection"
                : $"fixed distance ({defaultPlacementDistanceMeters:F2}m)";
            Debug.Log($"[DetectorWorldMarkerManager] Detector preview started: {detectorId}, mode={previewMode}");
            return true;
        }

        marker.isFollowingPlacementOrigin = false;
        marker.isPlaced = true;
        marker.anchorState = useSpatialAnchors ? "anchor saving..." : placementMethod;
        lastInteractedDetectorId = marker.detectorId;
        UpdateMarkerVisual(marker, marker.lastRadiationValue);

        // This branch commits immediately, so it is already a placed detector.
        // Persist it regardless of whether the button-driven flow is configured to
        // defer saves until its Place action.
        SaveCoordinate(detectorId, worldPosition, worldRotation, estimatedDistance, qrPixelSize, placementImagePoint, imageWidth, imageHeight, placementMethod);
        RecordPlacedDetector(marker.detectorId, true);

        FinalizeSpatialBinding(detectorId, marker, worldPosition, worldRotation);

        Debug.Log($"[DetectorWorldMarkerManager] Detector placed from projection: {detectorId}, method={placementMethod}, pos={worldPosition}, distance={estimatedDistance:F2}m, qrPixelSize={qrPixelSize:F1}px");
        return true;
    }

    /// <summary>
    /// Starts one isolated placement transaction. A new detector automatically rolls
    /// back the previous transaction so an unreachable, unplaced sphere cannot remain.
    /// Re-scanning an already placed detector snapshots its committed state first.
    /// </summary>
    private string BeginPlacementSession(string detectorId)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (string.IsNullOrEmpty(detectorId))
            return "";

        if (activePlacementSession != null &&
            DetectorIdsEqual(activePlacementSession.detectorId, detectorId) &&
            markers.TryGetValue(activePlacementSession.detectorId, out SourceMarker activeMarker) &&
            activeMarker != null && activeMarker.root != null)
        {
            currentFollowingDetectorId = activePlacementSession.detectorId;
            return activePlacementSession.detectorId;
        }

        RollbackActivePlacementSession();

        SourceMarker existing = null;
        if (markers.TryGetValue(detectorId, out existing) &&
            (existing == null || existing.root == null))
        {
            markers.Remove(detectorId);
            existing = null;
        }

        string canonicalDetectorId = existing != null
            ? existing.detectorId
            : detectorId;

        if (useSpatialAnchors)
        {
            EnsureSpatialAnchorManager();
            if (spatialAnchorManager != null)
                spatialAnchorManager.InvalidatePendingOperationForDetector(canonicalDetectorId);
        }

        activePlacementSession = CapturePlacementSession(canonicalDetectorId, existing);
        if (activePlacementSession.restoresCommittedMarker &&
            existing.anchor == null &&
            string.Equals(existing.anchorState, "anchor saving...", StringComparison.OrdinalIgnoreCase))
        {
            // The generation invalidation above cancelled that save. If this rescan
            // is cancelled too, restore an honest coordinate-fallback state.
            activePlacementSession.anchorState = $"{placedStateLabel} (coordinate)";
        }

        currentFollowingDetectorId = canonicalDetectorId;
        return canonicalDetectorId;
    }

    private PlacementSession CapturePlacementSession(string detectorId, SourceMarker marker)
    {
        PlacementSession session = new PlacementSession
        {
            detectorId = detectorId,
            restoresCommittedMarker = marker != null && marker.root != null && marker.isPlaced
        };

        if (!session.restoresCommittedMarker)
            return session;

        session.parent = marker.root.transform.parent;
        session.worldPosition = marker.root.transform.position;
        session.worldRotation = marker.root.transform.rotation;
        session.localPosition = marker.root.transform.localPosition;
        session.localRotation = marker.root.transform.localRotation;
        session.localScale = marker.root.transform.localScale;
        session.visibilityRequested = marker.visibilityRequested;
        session.savedPosition = marker.savedPosition;
        session.lastEstimatedDistance = marker.lastEstimatedDistance;
        session.lastQrPixelSize = marker.lastQrPixelSize;
        session.lastPlacementImagePoint = marker.lastPlacementImagePoint;
        session.lastImageWidth = marker.lastImageWidth;
        session.lastImageHeight = marker.lastImageHeight;
        session.lastPlacementMethod = marker.lastPlacementMethod;
        session.hasValidPlaneHit = marker.hasValidPlaneHit;
        session.anchorState = marker.anchorState;
        return session;
    }

    /// <summary>
    /// Restores a detector that was already committed, or removes a brand-new preview.
    /// Persistent coordinate/anchor data is deliberately left untouched.
    /// </summary>
    private bool RollbackActivePlacementSession()
    {
        PlacementSession session = activePlacementSession;
        string detectorId = session != null
            ? session.detectorId
            : currentFollowingDetectorId;

        activePlacementSession = null;
        currentFollowingDetectorId = "";

        detectorId = NormalizeDetectorId(detectorId);
        if (string.IsNullOrEmpty(detectorId) ||
            !markers.TryGetValue(detectorId, out SourceMarker marker))
            return false;

        if (marker == null)
        {
            markers.Remove(detectorId);
            return false;
        }

        if (session != null && session.restoresCommittedMarker && marker.root != null)
        {
            if (session.parent != null)
            {
                // Restore in the anchor's local coordinate system so any anchor
                // relocalization that happened during the preview is respected.
                marker.root.transform.SetParent(session.parent, false);
                marker.root.transform.localPosition = session.localPosition;
                marker.root.transform.localRotation = session.localRotation;
            }
            else
            {
                marker.root.transform.SetParent(transform, true);
                marker.root.transform.SetPositionAndRotation(session.worldPosition, session.worldRotation);
            }

            marker.root.transform.localScale = session.localScale;

            marker.savedPosition = session.savedPosition;
            marker.lastEstimatedDistance = session.lastEstimatedDistance;
            marker.lastQrPixelSize = session.lastQrPixelSize;
            marker.lastPlacementImagePoint = session.lastPlacementImagePoint;
            marker.lastImageWidth = session.lastImageWidth;
            marker.lastImageHeight = session.lastImageHeight;
            marker.lastPlacementMethod = session.lastPlacementMethod;
            marker.isFollowingPlacementOrigin = false;
            marker.isPlaced = true;
            marker.hasValidPlaneHit = session.hasValidPlaneHit;
            marker.anchorState = session.anchorState;

            UpdateMarkerVisual(marker, marker.lastRadiationValue);

            // UpdateMarkerVisual enforces the preview scale/visibility, so restore
            // the exact committed transform and visibility afterwards.
            if (session.parent != null)
            {
                marker.root.transform.localPosition = session.localPosition;
                marker.root.transform.localRotation = session.localRotation;
            }
            else
            {
                marker.root.transform.SetPositionAndRotation(session.worldPosition, session.worldRotation);
            }

            marker.root.transform.localScale = session.localScale;
            marker.visibilityRequested = session.visibilityRequested;
            ApplyMarkerVisibility(marker);

            UpdateLabel(marker, marker.lastRadiationValue, false);
            return true;
        }

        markers.Remove(detectorId);
        marker.isFollowingPlacementOrigin = false;
        marker.isPlaced = false;
        DestroyMarkerVisualResources(marker);

        if (marker.root != null)
        {
            SetMarkerRequestedVisibility(marker, false);
            Destroy(marker.root);
        }

        return false;
    }

    private void UpdateFollowingMarkerPosition()
    {
        if (string.IsNullOrEmpty(currentFollowingDetectorId))
            return;

        if (!markers.TryGetValue(currentFollowingDetectorId, out SourceMarker marker) || marker == null)
            return;

        if (!marker.isFollowingPlacementOrigin || marker.isPlaced)
            return;

        UpdateFollowingMarkerPosition(marker);
    }

    private void UpdateFollowingMarkerPosition(SourceMarker marker)
    {
        if (marker == null || marker.root == null)
            return;

        EnsurePlacementOrigin();
        if (placementOrigin == null)
        {
            Debug.LogWarning("[DetectorWorldMarkerManager] Cannot update detector preview. Placement Origin is missing.");
            return;
        }

        if (usePlaneIntersectionPlacement)
        {
            UpdateFollowingMarkerFromPlaneIntersection(marker);
            return;
        }

        float distance = defaultPlacementDistanceMeters;

        Vector3 direction = placementOrigin.forward.sqrMagnitude > 0.0001f
            ? placementOrigin.forward.normalized
            : Vector3.forward;

        Vector3 worldPosition = placementOrigin.position + direction * distance;
        worldPosition += placementOrigin.up * markerVerticalOffsetMeters;

        SetMarkerRequestedVisibility(marker, true);
        marker.root.transform.position = worldPosition;
        marker.root.transform.rotation = Quaternion.LookRotation(direction, placementOrigin.up);
        marker.root.transform.localScale = Vector3.one * fixedMarkerSize;

        marker.savedPosition = worldPosition;
        marker.lastEstimatedDistance = distance;
        marker.anchorState = followingStateLabel;
        marker.hasValidPlaneHit = false;
    }

    private void UpdateFollowingMarkerFromPlaneIntersection(SourceMarker marker)
    {
        if (!TryGetGazePlaneIntersection(out Vector3 hitPosition, out Quaternion hitRotation, out float hitDistance))
        {
            marker.hasValidPlaneHit = false;
            marker.anchorState = waitingForPlaneStateLabel;

            if (hidePreviewWithoutPlaneHit)
            {
                if (marker.root != null)
                    SetMarkerRequestedVisibility(marker, false);

                return;
            }

            // Committable on purpose: refusing to place at all is worse than a guessed depth.
            Vector3 fallbackDirection = placementOrigin.forward.sqrMagnitude > 0.0001f
                ? placementOrigin.forward.normalized
                : Vector3.forward;

            Vector3 fallbackPosition =
                placementOrigin.position +
                fallbackDirection * defaultPlacementDistanceMeters +
                placementOrigin.up * markerVerticalOffsetMeters;

            SetMarkerRequestedVisibility(marker, true);
            marker.root.transform.position = fallbackPosition;
            marker.root.transform.rotation =
                Quaternion.LookRotation(fallbackDirection, placementOrigin.up);
            marker.root.transform.localScale = Vector3.one * fixedMarkerSize;

            marker.savedPosition = fallbackPosition;
            marker.lastEstimatedDistance = defaultPlacementDistanceMeters;
            marker.lastPlacementMethod = "GazeCenterFallbackDistance";
            return;
        }

        marker.hasValidPlaneHit = true;
        SetMarkerRequestedVisibility(marker, true);
        marker.root.transform.position = hitPosition;
        marker.root.transform.rotation = hitRotation;
        marker.root.transform.localScale = Vector3.one * fixedMarkerSize;

        marker.savedPosition = hitPosition;
        marker.lastEstimatedDistance = hitDistance;
        marker.lastPlacementMethod = "GazeCenterPlaneIntersection";
        marker.anchorState = $"{followingStateLabel} (plane)";
    }

    public bool TryPlaceCurrentDetector(out string resultMessage)
    {
        string detectorId = NormalizeDetectorId(ActivePlacementDetectorId);
        if (string.IsNullOrEmpty(detectorId))
        {
            resultMessage = "Add the Source before placing";
            return false;
        }

        if (!markers.TryGetValue(detectorId, out SourceMarker marker) ||
            marker == null || marker.root == null)
        {
            resultMessage = $"Detector preview not found: {detectorId}";
            return false;
        }

        if (marker.isFollowingPlacementOrigin && !marker.isPlaced)
            UpdateFollowingMarkerPosition(marker);

        PlaceDetector(marker.detectorId);
        bool placed = marker.isPlaced && !marker.isFollowingPlacementOrigin;
        string placementName = DetectorIdsEqual(marker.detectorId, sourceMarkerKey)
            ? "Source"
            : marker.detectorId;
        resultMessage = placed
            ? $"{placementName} placed"
            : $"{placementName} could not be placed";
        return placed;
    }

    public void PlaceDetector(string detectorId)
    {
        detectorId = NormalizeDetectorId(detectorId);
        if (string.IsNullOrEmpty(detectorId))
            return;

        if (!markers.TryGetValue(detectorId, out SourceMarker marker) || marker == null || marker.root == null)
        {
            Debug.LogWarning($"[DetectorWorldMarkerManager] PlaceDetector failed. Marker not found: {detectorId}");
            return;
        }

        detectorId = marker.detectorId;

        if (activePlacementSession != null &&
            !DetectorIdsEqual(activePlacementSession.detectorId, detectorId))
        {
            Debug.LogWarning($"[DetectorWorldMarkerManager] PlaceDetector ignored. Active preview belongs to {activePlacementSession.detectorId}, not {detectorId}.");
            return;
        }

        if (marker.isFollowingPlacementOrigin && !marker.isPlaced)
            UpdateFollowingMarkerPosition(marker);

        marker.isFollowingPlacementOrigin = false;
        marker.isPlaced = true;
        marker.savedPosition = marker.root.transform.position;
        marker.anchorState = placedStateLabel;
        lastInteractedDetectorId = detectorId;

        if (DetectorIdsEqual(currentFollowingDetectorId, detectorId))
            currentFollowingDetectorId = "";

        if (activePlacementSession != null &&
            DetectorIdsEqual(activePlacementSession.detectorId, detectorId))
        {
            activePlacementSession = null;
        }

        // Preserve the AR plane orientation captured by the placement preview.
        // Local Y remains the plane normal, so the detector pattern does not turn
        // toward the viewer after it has been placed.
        Quaternion worldRotation = marker.root.transform.rotation;
        string placementMethod = string.IsNullOrEmpty(marker.lastPlacementMethod)
            ? "PreviewCenterButtonPlaced"
            : marker.lastPlacementMethod + "+ButtonPlaced";

        SaveCoordinate(
            detectorId,
            marker.savedPosition,
            worldRotation,
            marker.lastEstimatedDistance,
            marker.lastQrPixelSize,
            marker.lastPlacementImagePoint,
            marker.lastImageWidth,
            marker.lastImageHeight,
            placementMethod
        );
        RecordPlacedDetector(detectorId, true);

        FinalizeSpatialBinding(detectorId, marker, marker.savedPosition, worldRotation);

        ForceMarkerVisible(marker);
        UpdateMarkerVisual(marker, marker.lastRadiationValue);
        Debug.Log($"[DetectorWorldMarkerManager] Detector fixed: {detectorId}, pos={marker.savedPosition}, distance={marker.lastEstimatedDistance:F2}m, method={placementMethod}");
    }

    public bool TryCancelCurrentDetector(out string resultMessage)
    {
        if (activePlacementSession != null || !string.IsNullOrEmpty(currentFollowingDetectorId))
        {
            string previewDetectorId = activePlacementSession != null
                ? activePlacementSession.detectorId
                : currentFollowingDetectorId;

            bool restoredCommittedMarker = RollbackActivePlacementSession();
            lastInteractedDetectorId = "";

            string result = restoredCommittedMarker
                ? "restored to its previous placed pose"
                : "new preview removed";
            Debug.Log($"[DetectorWorldMarkerManager] Detector placement cancelled: {previewDetectorId}, {result}.");
            resultMessage = restoredCommittedMarker
                ? $"Placement cancelled: {previewDetectorId} restored"
                : $"Placement cancelled: {previewDetectorId} preview removed";
            return true;
        }

        resultMessage = "Nothing to cancel";
        return false;
    }

    /// <summary>
    /// Cancels only an active preview transaction. It never falls through to the
    /// committed-detector LIFO removal path.
    /// </summary>
    public bool CancelActivePlacementOnly()
    {
        if (activePlacementSession == null &&
            string.IsNullOrEmpty(currentFollowingDetectorId))
        {
            return false;
        }

        RollbackActivePlacementSession();
        lastInteractedDetectorId = "";
        return true;
    }

    private PlacementGeometry PlacementGeometry
    {
        get
        {
            placementGeometry.usePreviewCenterPlacement = usePreviewCenterPlacement;
            placementGeometry.markerVerticalOffsetMeters = markerVerticalOffsetMeters;
            placementGeometry.cameraHorizontalFovDegrees = cameraHorizontalFovDegrees;
            placementGeometry.cameraVerticalFovDegrees = cameraVerticalFovDegrees;
            placementGeometry.useQrSizeToEstimateDistance = useQrSizeToEstimateDistance;
            placementGeometry.defaultPlacementDistanceMeters = defaultPlacementDistanceMeters;
            placementGeometry.realQrSizeMeters = realQrSizeMeters;
            placementGeometry.qrEffectiveSizeRatio = qrEffectiveSizeRatio;
            placementGeometry.distanceCalibrationMultiplier = distanceCalibrationMultiplier;
            placementGeometry.minEstimatedDistanceMeters = minEstimatedDistanceMeters;
            placementGeometry.maxEstimatedDistanceMeters = maxEstimatedDistanceMeters;
            return placementGeometry;
        }
    }

    private Vector3 CalculateWorldPosition(Vector2 imagePoint, int imageWidth, int imageHeight, float distance)
    {
        EnsurePlacementOrigin();

        if (placementOrigin == null)
            return transform.position;

        return PlacementGeometry.CalculateWorldPosition(
            placementOrigin,
            imagePoint,
            imageWidth,
            imageHeight,
            distance);
    }

    private Quaternion CalculateMarkerRotation(Vector3 worldPosition)
    {
        EnsurePlacementOrigin();
        return PlacementGeometry.CalculateMarkerRotation(placementOrigin, worldPosition);
    }

    private float GetPlacementDistance(int imageWidth, float qrPixelSize)
    {
        return PlacementGeometry.GetPlacementDistance(imageWidth, qrPixelSize);
    }
}
