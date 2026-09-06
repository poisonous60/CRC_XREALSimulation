using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    /// <summary>
    /// Updates the controller-ray hover without relying on colliders. Detector
    /// falloff shells intentionally have no colliders, so the center ray is tested
    /// directly against each visible center sphere.
    /// </summary>
    public bool TryUpdateDetectorHover(Ray pointerRay, out string detectorId)
    {
        detectorId = "";

        if (!enableControllerDetectorReposition)
        {
            SetControllerHoveredMarker(null);
            return false;
        }

        if (activeDetectorMoveSession != null)
        {
            MarkerInfo movingMarker = activeDetectorMoveSession.marker;
            if (movingMarker != null && movingMarker.root != null)
            {
                SetControllerHoveredMarker(movingMarker);
                detectorId = movingMarker.detectorId;
                return true;
            }

            AbortControllerDetectorInteraction(false);
            return false;
        }

        if (HasActivePlacement ||
            (roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement))
        {
            SetControllerHoveredMarker(null);
            return false;
        }

        Vector3 rayDirection = pointerRay.direction;
        if (!IsFiniteVector(rayDirection) || rayDirection.sqrMagnitude < 0.0001f)
        {
            SetControllerHoveredMarker(null);
            return false;
        }

        rayDirection.Normalize();
        float maximumDistance = Mathf.Max(0.5f, controllerSelectionMaxDistanceMeters);
        float angularAllowance = Mathf.Tan(
            Mathf.Clamp(controllerSelectionAngleDegrees, 0.25f, 8f) * Mathf.Deg2Rad);
        bool selectedDirectHit = false;
        float closestEntryDepth = float.PositiveInfinity;
        float closestNormalizedMiss = float.PositiveInfinity;
        MarkerInfo selectedMarker = null;

        foreach (var pair in markers)
        {
            MarkerInfo marker = pair.Value;
            if (!IsMarkerSelectableByController(marker))
                continue;

            Vector3 toMarker = marker.root.transform.position - pointerRay.origin;
            float depth = Vector3.Dot(toMarker, rayDirection);
            if (depth <= 0.02f || depth > maximumDistance)
                continue;

            float perpendicularSquared =
                Mathf.Max(0f, toMarker.sqrMagnitude - depth * depth);
            float visibleRadius = Mathf.Max(0.01f, fixedMarkerSize * 0.5f);
            if (marker.renderer != null)
            {
                Vector3 extents = marker.renderer.bounds.extents;
                visibleRadius = Mathf.Max(
                    visibleRadius,
                    Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
            }

            float hitRadius =
                visibleRadius * Mathf.Max(1f, controllerSelectionRadiusMultiplier);
            bool directHit = perpendicularSquared <= hitRadius * hitRadius;
            float allowedRadius = Mathf.Max(
                hitRadius,
                depth * angularAllowance);
            if (perpendicularSquared > allowedRadius * allowedRadius)
                continue;

            float normalizedMiss =
                Mathf.Sqrt(perpendicularSquared) / Mathf.Max(0.001f, depth);
            float entryDepth = directHit
                ? depth - Mathf.Sqrt(
                    Mathf.Max(0f, hitRadius * hitRadius - perpendicularSquared))
                : depth;
            bool shouldSelect =
                selectedMarker == null ||
                (directHit && !selectedDirectHit) ||
                (directHit == selectedDirectHit &&
                 (entryDepth < closestEntryDepth ||
                  (Mathf.Approximately(entryDepth, closestEntryDepth) &&
                   normalizedMiss < closestNormalizedMiss)));
            if (shouldSelect)
            {
                selectedDirectHit = directHit;
                closestEntryDepth = entryDepth;
                closestNormalizedMiss = normalizedMiss;
                selectedMarker = marker;
            }
        }

        SetControllerHoveredMarker(selectedMarker);
        if (selectedMarker == null)
            return false;

        detectorId = selectedMarker.detectorId;
        return true;
    }

    public void ClearDetectorHover()
    {
        if (activeDetectorMoveSession == null)
            SetControllerHoveredMarker(null);
    }

    public bool TryBeginPointedDetectorMove(Ray pointerRay, out string resultMessage)
    {
        resultMessage = "No detector is pointed at";

        if (!enableControllerDetectorReposition)
        {
            resultMessage = "Controller detector movement is disabled";
            return false;
        }

        if (activeDetectorMoveSession != null)
        {
            resultMessage = $"Detector already moving: {ActiveMovedDetectorId}";
            return false;
        }

        if (HasActivePlacement)
        {
            resultMessage = "Place or cancel the detector preview first";
            return false;
        }

        if (roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement)
        {
            resultMessage = "Place or cancel ROOM_ORIGIN first";
            return false;
        }

        if (!TryUpdateDetectorHover(pointerRay, out string detectorId) ||
            controllerHoveredMarker == null ||
            controllerHoveredMarker.root == null)
        {
            return false;
        }

        Vector3 initialDirection = pointerRay.direction;
        if (!IsFiniteVector(initialDirection) || initialDirection.sqrMagnitude < 0.0001f)
        {
            resultMessage = "Controller pointing direction is unavailable";
            return false;
        }

        initialDirection.Normalize();
        MarkerInfo marker = controllerHoveredMarker;
        Transform markerTransform = marker.root.transform;
        activeDetectorMoveSession = new DetectorMoveSession
        {
            marker = marker,
            parent = markerTransform.parent,
            worldPosition = markerTransform.position,
            worldRotation = markerTransform.rotation,
            localPosition = markerTransform.localPosition,
            localRotation = markerTransform.localRotation,
            localScale = markerTransform.localScale,
            visibilityRequested = marker.visibilityRequested,
            savedPosition = marker.savedPosition,
            lastEstimatedDistance = marker.lastEstimatedDistance,
            lastPlacementMethod = marker.lastPlacementMethod,
            anchor = marker.anchor,
            anchorGuid = marker.anchorGuid,
            anchorState = marker.anchorState,
            initialPointerDirection = initialDirection,
            initialOffsetFromRayOrigin = markerTransform.position - pointerRay.origin
        };

        // A marker parented to a legacy detector anchor must be detached while it
        // moves. Its original hierarchy is retained in the transaction for rollback.
        markerTransform.SetParent(transform, true);
        if (useSpatialAnchors && spatialAnchorManager != null)
            spatialAnchorManager.InvalidatePendingOperationForDetector(detectorId);

        marker.isControllerMoving = true;
        marker.isControllerHovered = true;

        UpdateMarkerVisual(marker, marker.lastRadiationValue);
        resultMessage = $"Moving detector: {detectorId}";
        Debug.Log($"[DetectorWorldMarkerManager] Controller move started: {detectorId}");
        return true;
    }

    public bool UpdateActiveDetectorMove(Ray pointerRay)
    {
        if (activeDetectorMoveSession == null ||
            activeDetectorMoveSession.marker == null ||
            activeDetectorMoveSession.marker.root == null)
        {
            return false;
        }

        Vector3 currentDirection = pointerRay.direction;
        if (!IsFiniteVector(pointerRay.origin) ||
            !IsFiniteVector(currentDirection) ||
            currentDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        currentDirection.Normalize();
        Quaternion directionDelta = Quaternion.FromToRotation(
            activeDetectorMoveSession.initialPointerDirection,
            currentDirection);
        Vector3 newPosition =
            pointerRay.origin +
            directionDelta * activeDetectorMoveSession.initialOffsetFromRayOrigin;
        if (!IsFiniteVector(newPosition))
            return false;

        MarkerInfo marker = activeDetectorMoveSession.marker;
        marker.root.transform.SetPositionAndRotation(
            newPosition,
            activeDetectorMoveSession.worldRotation);
        UpdateLabel(marker, marker.lastRadiationValue, true);
        return true;
    }

    public bool TryEndActiveDetectorMove(bool commit, out string resultMessage)
    {
        return EndActiveDetectorMove(commit, out resultMessage);
    }

    private bool EndActiveDetectorMove(bool commit, out string resultMessage)
    {
        if (activeDetectorMoveSession == null)
        {
            resultMessage = "No detector is being moved";
            return false;
        }

        DetectorMoveSession session = activeDetectorMoveSession;
        activeDetectorMoveSession = null;
        MarkerInfo marker = session.marker;
        if (marker == null || marker.root == null)
        {
            resultMessage = "The moving detector is no longer available";
            return false;
        }

        marker.isControllerMoving = false;
        if (!commit)
        {
            RestoreDetectorMoveSession(session);

            resultMessage = $"Detector move cancelled: {marker.detectorId}";
            Debug.Log($"[DetectorWorldMarkerManager] Controller move cancelled: {marker.detectorId}");
            return true;
        }

        Vector3 committedPosition = marker.root.transform.position;
        Quaternion committedRotation = session.worldRotation;
        marker.root.transform.SetPositionAndRotation(committedPosition, committedRotation);
        marker.savedPosition = committedPosition;
        marker.lastPlacementMethod = "ControllerRayRepositioned";

        SaveCoordinate(
            marker.detectorId,
            committedPosition,
            committedRotation,
            marker.lastEstimatedDistance,
            marker.lastQrPixelSize,
            marker.lastPlacementImagePoint,
            marker.lastImageWidth,
            marker.lastImageHeight,
            marker.lastPlacementMethod);

        // ROOM_ORIGIN remains authoritative when calibrated. In the legacy anchor
        // path this creates a replacement anchor and preserves restart persistence.
        FinalizeSpatialBinding(
            marker.detectorId,
            marker,
            committedPosition,
            committedRotation,
            true);

        ForceMarkerVisible(marker);
        UpdateMarkerVisual(marker, marker.lastRadiationValue);

        resultMessage = $"Detector moved: {marker.detectorId}";
        Debug.Log(
            $"[DetectorWorldMarkerManager] Controller move committed: " +
            $"{marker.detectorId}, pos={committedPosition}");
        return true;
    }

    private void RestoreDetectorMoveSession(DetectorMoveSession session)
    {
        if (session == null || session.marker == null || session.marker.root == null)
            return;

        MarkerInfo marker = session.marker;
        Transform markerTransform = marker.root.transform;
        markerTransform.SetParent(session.parent, false);
        if (session.parent != null)
        {
            markerTransform.localPosition = session.localPosition;
            markerTransform.localRotation = session.localRotation;
        }
        else
        {
            markerTransform.SetPositionAndRotation(
                session.worldPosition,
                session.worldRotation);
        }

        markerTransform.localScale = session.localScale;
        marker.savedPosition = session.savedPosition;
        marker.lastEstimatedDistance = session.lastEstimatedDistance;
        marker.lastPlacementMethod = session.lastPlacementMethod;
        marker.visibilityRequested = session.visibilityRequested;
        marker.anchor = session.anchor;
        marker.anchorGuid = session.anchorGuid;
        marker.anchorState = session.anchorState;
        marker.isControllerMoving = false;

        UpdateMarkerVisual(marker, marker.lastRadiationValue);
        if (session.parent != null)
        {
            markerTransform.localPosition = session.localPosition;
            markerTransform.localRotation = session.localRotation;
        }
        else
        {
            markerTransform.SetPositionAndRotation(
                session.worldPosition,
                session.worldRotation);
        }

        markerTransform.localScale = session.localScale;
        marker.visibilityRequested = session.visibilityRequested;
        ApplyMarkerVisibility(marker);
        UpdateLabel(marker, marker.lastRadiationValue, false);
    }

    private void AbortControllerDetectorInteraction(bool restoreActiveMove)
    {
        if (activeDetectorMoveSession != null)
        {
            if (restoreActiveMove)
            {
                EndActiveDetectorMove(false, out _);
            }
            else
            {
                if (activeDetectorMoveSession.marker != null)
                {
                    activeDetectorMoveSession.marker.isControllerMoving = false;
                    activeDetectorMoveSession.marker.isControllerHovered = false;
                }

                activeDetectorMoveSession = null;
            }
        }

        SetControllerHoveredMarker(null);
    }

    private void SetControllerHoveredMarker(MarkerInfo marker)
    {
        if (ReferenceEquals(controllerHoveredMarker, marker))
            return;

        MarkerInfo previous = controllerHoveredMarker;
        controllerHoveredMarker = marker;

        if (previous != null)
        {
            previous.isControllerHovered = false;
            if (previous.root != null)
                UpdateMarkerVisual(previous, previous.lastRadiationValue);
        }

        if (controllerHoveredMarker != null)
        {
            controllerHoveredMarker.isControllerHovered = true;
            if (controllerHoveredMarker.root != null)
            {
                UpdateMarkerVisual(
                    controllerHoveredMarker,
                    controllerHoveredMarker.lastRadiationValue);
            }
        }
    }

    private bool IsMarkerSelectableByController(MarkerInfo marker)
    {
        return marker != null &&
               marker.root != null &&
               marker.root.activeInHierarchy &&
               marker.isPlaced &&
               !marker.isFollowingPlacementOrigin &&
               marker.centerVisualRequested &&
               marker.renderer != null &&
               marker.renderer.enabled;
    }

    private bool IsFiniteVector(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
