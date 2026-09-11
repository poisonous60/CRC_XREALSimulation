using System;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Places the room origin on the tracked ROOM_ORIGIN image, on a wall or lying flat,
/// and re-aligns it, with every placed source, on each later sighting.
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

    private Camera glassesCamera;
    private bool subscribed;
    private bool guideShown;
    private bool cameraWarned;
    private Pose committedPose = Pose.identity;
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

        if (roomCoordinateSystem.IsCalibrated)
        {
            guideShown = false;

            // Disabling the manager stops the image subsystem itself, so no sighting reaches
            // any listener while the operator is aiming a detector. Only once the origin
            // stands, or the marker could never place it.
            SetImageTrackingEnabled(!IsPlacementRunning());
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

        if (!TryBuildRoomPose(image, out Pose worldPose))
            return;

        if (!roomCoordinateSystem.IsCalibrated)
        {
            if (roomCoordinateSystem.TryCalibrateFromPose(markerName, worldPose, out _))
                CommitPose(worldPose);

            return;
        }

        if (Time.time < nextRealignTime)
            return;

        if (Vector3.Distance(worldPose.position, committedPose.position) < realignPositionThresholdMeters &&
            Quaternion.Angle(worldPose.rotation, committedPose.rotation) < realignRotationThresholdDegrees)
        {
            return;
        }

        if (roomCoordinateSystem.TryRealignCalibratedFrame(worldPose))
            CommitPose(worldPose);
    }

    private void CommitPose(Pose worldPose)
    {
        committedPose = worldPose;
        nextRealignTime = Time.time + realignIntervalSeconds;
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
