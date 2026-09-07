using System;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Places the room origin on the tracked ROOM_ORIGIN wall image at its first sighting, replacing the QR aim-and-place step.
/// </summary>
[DisallowMultipleComponent]
public sealed class ImageMarkerRoomCalibrator : MonoBehaviour
{
    // Same limit as RoomCoordinateSystem.maximumWallNormalVerticalDot, so a marker
    // lying on a table is rejected the way a floor plane is.
    private const float MaximumNormalVerticalDot = 0.30f;
    private const float MinimumViewerOffsetMeters = 0.08f;

    [Header("References")]
    [Tooltip("Optional. The first ARTrackedImageManager in the scene is used when empty.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    [Tooltip("Optional. The first RoomCoordinateSystem in the scene is used when empty.")]
    [SerializeField] private RoomCoordinateSystem roomCoordinateSystem;

    [Header("Marker")]
    [Tooltip("Reference image name in the library. Also used as the room id, so records saved by the wall-QR flow stay compatible.")]
    [SerializeField] private string markerName = "ROOM_ORIGIN";

    [Tooltip("On: the image normal is the trackable's local +Y. Off: local +Z. Flip this when the restored marker comes back rotated about the vertical.")]
    [SerializeField] private bool markerNormalIsLocalUp = true;

    private Camera glassesCamera;
    private bool subscribed;
    private bool guideShown;
    private bool tiltWarned;
    private bool cameraWarned;

    private void OnEnable()
    {
        if (subscribed)
            return;

        if (trackedImageManager == null)
            trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);

        if (trackedImageManager == null)
        {
            Debug.LogWarning("[ImageMarkerRoomCalibrator] No ARTrackedImageManager in the scene; the wall marker cannot place the room origin.");
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
            return;
        }

        if (guideShown)
            return;

        guideShown = true;
        RoomCoordinateSystem.PublishStatus(
            $"Look at the {markerName} wall marker to place the room origin",
            Color.cyan);
    }

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        if (!TryResolveRoomCoordinateSystem() || roomCoordinateSystem.IsCalibrated)
            return;

        if (!TryFindMarker(args.added, out ARTrackedImage image) &&
            !TryFindMarker(args.updated, out image))
        {
            return;
        }

        if (!TryBuildRoomPose(image, out Pose worldPose))
            return;

        if (roomCoordinateSystem.TryCalibrateFromPose(markerName, worldPose, out string message))
            Debug.Log($"[ImageMarkerRoomCalibrator] {message} from tracked image at {worldPose.position}");
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
        Vector3 normal = markerNormalIsLocalUp ? imageTransform.up : imageTransform.forward;

        if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > MaximumNormalVerticalDot)
        {
            if (!tiltWarned)
            {
                tiltWarned = true;
                RoomCoordinateSystem.PublishStatus(
                    $"{markerName} marker is not on a vertical wall. Check markerNormalIsLocalUp",
                    Color.yellow);
            }

            return false;
        }

        Vector3 forward = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;

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

        worldPose = new Pose(imageTransform.position, Quaternion.LookRotation(forward, Vector3.up));
        return true;
    }
}
