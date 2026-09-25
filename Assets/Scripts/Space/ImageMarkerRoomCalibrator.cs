using System;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Places the room origin on the tracked ROOM_ORIGIN image, on a wall or lying flat,
/// and re-aligns it, with every placed source, on each later sighting. Switched on, a spatial
/// anchor holds it while the marker is out of view.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImageMarkerRoomCalibrator : MonoBehaviour
{
    // Same limit as RoomCoordinateSystem.maximumWallNormalVerticalDot: above it the
    // marker counts as lying flat and its printed top edge, not the normal, sets +Z.
    private const float MaximumNormalVerticalDot = 0.30f;
    private const float MinimumViewerOffsetMeters = 0.08f;

    [Header("References")]
    [Tooltip("Optional. The first ARTrackedImageManager in the scene is used when empty.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    [Tooltip("Optional. The first RoomCoordinateSystem in the scene is used when empty.")]
    [SerializeField] private RoomCoordinateSystem roomCoordinateSystem;

    [Tooltip("Optional. The first DetectorWorldMarkerManager in the scene is used when empty. It is asked whether a placement is running.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    [Tooltip("Optional. The first ARAnchorManager in the scene is used when empty. Only the frame anchor uses it.")]
    [SerializeField] private ARAnchorManager anchorManager;

    [Header("Marker")]
    [Tooltip("Reference image name in the library. Also used as the room id, so records saved by the wall-QR flow stay compatible.")]
    [SerializeField] private string markerName = "ROOM_ORIGIN";

    [Header("Re-alignment")]
    [Tooltip("Minimum seconds between two re-alignments of an already placed room origin. 0 re-aligns on every sighting.")]
    [SerializeField, Min(0f)] private float realignIntervalSeconds = 0.5f;

    [Tooltip("Re-align only once the marker has moved this far from the committed frame. Absorbs tracker jitter.")]
    [SerializeField, Min(0f)] private float realignPositionThresholdMeters = 0.01f;

    [Tooltip("Re-align only once the marker has turned this much from the committed frame. Absorbs tracker jitter.")]
    [SerializeField, Min(0f)] private float realignRotationThresholdDegrees = 1f;

    [Header("Frame Anchor")]
    [Tooltip("ON = one unsaved spatial anchor is made at the room origin on the first marker sighting, and the origin follows it while the marker is out of view. The marker still wins whenever it is in view. OFF = the origin moves only on marker sightings.")]
    [RuntimeTunable("Hold origin with anchor")]
    [SerializeField] private bool holdFrameWithAnchor = true;

    private Camera glassesCamera;
    private ARTrackedImage trackedMarker;
    private RoomFrameAnchor frameAnchor;
    private bool subscribed;
    private bool guideShown;
    private bool cameraWarned;
    private float nextRealignTime;

    private void OnEnable()
    {
        if (subscribed)
            return;

        if (trackedImageManager == null)
            trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);

        if (trackedImageManager == null)
        {
            Debug.LogWarning("[ImageMarkerRoomCalibrator] No ARTrackedImageManager in the scene; the marker cannot place the room origin.");
            return;
        }

        trackedImageManager.trackablesChanged.AddListener(HandleTrackablesChanged);
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        if (trackedImageManager != null)
            trackedImageManager.trackablesChanged.RemoveListener(HandleTrackablesChanged);
        subscribed = false;
    }

    private void Update()
    {
        if (!TryResolveRoomCoordinateSystem())
            return;

        if (!holdFrameWithAnchor || !roomCoordinateSystem.IsCalibrated)
            frameAnchor?.Remove();

        if (roomCoordinateSystem.IsCalibrated)
        {
            guideShown = false;

            // Disabling the manager stops the image subsystem itself, so no sighting reaches
            // any listener while the operator is aiming a detector. Only once the origin
            // stands, or the marker could never place it.
            SetImageTrackingEnabled(!IsPlacementRunning());
            FollowFrameAnchor();
            return;
        }

        SetImageTrackingEnabled(true);

        if (guideShown)
            return;

        guideShown = true;
        RoomCoordinateSystem.PublishStatus(
            $"Look at the {markerName} marker to place the room origin",
            Color.cyan);
    }

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        if (!TryResolveRoomCoordinateSystem())
            return;

        if (!TryFindMarker(args.added, out ARTrackedImage image) &&
            !TryFindMarker(args.updated, out image))
        {
            return;
        }

        trackedMarker = image;

        // XREAL does not document what a marker that left the view reports. A stale
        // Tracking pose would undo every anchor follow.
        if (holdFrameWithAnchor && !IsInView(image))
            return;

        if (!TryBuildRoomPose(image, out Pose worldPose))
            return;

        if (!roomCoordinateSystem.IsCalibrated)
        {
            // A resume can invalidate the frame and deliver a sighting before the next Update
            // clears the anchor from the old world.
            frameAnchor?.Remove();

            if (!roomCoordinateSystem.TryCalibrateFromPose(markerName, worldPose, out _))
                return;

            RestartRealignInterval();
        }
        else if (Time.time >= nextRealignTime &&
                 IsPastRealignThreshold(worldPose) &&
                 roomCoordinateSystem.TryRealignCalibratedFrame(worldPose))
        {
            RestartRealignInterval();
        }

        // On every in-view sighting, not only a re-alignment, so a follow that starts when
        // the marker leaves the view starts from where the frame stands.
        if (holdFrameWithAnchor && TryGetFramePose(out Pose framePose))
            ResolveFrameAnchor().Commit(framePose);
    }

    private void FollowFrameAnchor()
    {
        if (!holdFrameWithAnchor || frameAnchor == null || Time.time < nextRealignTime || IsInView(trackedMarker))
            return;

        if (!frameAnchor.TryGetFollowPose(out Pose followPose) || !IsPastRealignThreshold(followPose))
            return;

        if (roomCoordinateSystem.TryRealignCalibratedFrame(followPose))
            RestartRealignInterval();
    }

    private void RestartRealignInterval()
    {
        nextRealignTime = Time.time + realignIntervalSeconds;
    }

    private bool IsPastRealignThreshold(Pose worldPose)
    {
        if (!TryGetFramePose(out Pose framePose))
            return false;

        return Vector3.Distance(worldPose.position, framePose.position) >= realignPositionThresholdMeters ||
               Quaternion.Angle(worldPose.rotation, framePose.rotation) >= realignRotationThresholdDegrees;
    }

    private bool TryGetFramePose(out Pose framePose)
    {
        Transform frame = roomCoordinateSystem.CoordinateFrame;
        framePose = frame != null ? new Pose(frame.position, frame.rotation) : Pose.identity;
        return frame != null;
    }

    private bool IsInView(ARTrackedImage image)
    {
        if (image == null || image.trackingState != TrackingState.Tracking)
            return false;

        if (glassesCamera == null)
            glassesCamera = Camera.main;

        if (glassesCamera == null)
            return false;

        Vector3 viewport = glassesCamera.WorldToViewportPoint(image.transform.position);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
    }

    private RoomFrameAnchor ResolveFrameAnchor()
    {
        if (frameAnchor != null)
            return frameAnchor;

        if (anchorManager == null)
            anchorManager = FindFirstObjectByType<ARAnchorManager>();

        if (anchorManager == null)
            Debug.LogWarning("[ImageMarkerRoomCalibrator] No ARAnchorManager in the scene; the frame anchor cannot be made.");

        frameAnchor = new RoomFrameAnchor(anchorManager);
        return frameAnchor;
    }

    private void SetImageTrackingEnabled(bool value)
    {
        if (trackedImageManager != null && trackedImageManager.enabled != value)
            trackedImageManager.enabled = value;
    }

    private bool IsPlacementRunning()
    {
        if (markerManager == null)
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>();

        return markerManager != null &&
               (markerManager.HasActivePlacement || markerManager.HasActiveDetectorMove);
    }

    private bool TryResolveRoomCoordinateSystem()
    {
        if (roomCoordinateSystem == null)
            roomCoordinateSystem = FindFirstObjectByType<RoomCoordinateSystem>();

        return roomCoordinateSystem != null;
    }

    private bool TryFindMarker(ReadOnlyList<ARTrackedImage> images, out ARTrackedImage image)
    {
        foreach (ARTrackedImage candidate in images)
        {
            if (candidate.trackingState != TrackingState.Tracking)
                continue;

            if (string.Equals(candidate.referenceImage.name, markerName, StringComparison.OrdinalIgnoreCase))
            {
                image = candidate;
                return true;
            }
        }

        image = null;
        return false;
    }

    private bool TryBuildRoomPose(ARTrackedImage image, out Pose worldPose)
    {
        worldPose = Pose.identity;
        Transform imageTransform = image.transform;
        // AR Foundation convention, confirmed on the Air 2 Ultra (device log 2026-09-07):
        // the image lies in the trackable's XZ plane, local +Y is its normal, +Z its top edge.
        Vector3 normal = imageTransform.up;
        bool lyingFlat = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > MaximumNormalVerticalDot;

        Vector3 forward = Vector3.ProjectOnPlane(
            lyingFlat ? imageTransform.forward : normal,
            Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            return false;

        forward.Normalize();

        if (!lyingFlat)
        {
            if (glassesCamera == null)
                glassesCamera = Camera.main;

            if (glassesCamera == null)
            {
                if (!cameraWarned)
                {
                    cameraWarned = true;
                    Debug.LogWarning("[ImageMarkerRoomCalibrator] No main camera; the wall normal sign cannot be resolved.");
                }

                return false;
            }

            Vector3 viewerOffset = Vector3.ProjectOnPlane(
                glassesCamera.transform.position - imageTransform.position,
                Vector3.up);
            if (viewerOffset.magnitude < MinimumViewerOffsetMeters)
                return false;

            if (Vector3.Dot(forward, viewerOffset) < 0f)
                forward = -forward;
        }

        worldPose = new Pose(imageTransform.position, Quaternion.LookRotation(forward, Vector3.up));
        return true;
    }
}
