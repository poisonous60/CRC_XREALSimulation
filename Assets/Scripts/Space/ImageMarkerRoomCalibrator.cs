using System;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Places the room origin on the tracked ROOM_ORIGIN image, on a wall or lying flat, at its first sighting.
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

    [Header("Marker")]
    [Tooltip("Reference image name in the library. Also used as the room id, so records saved by the wall-QR flow stay compatible.")]
    [SerializeField] private string markerName = "ROOM_ORIGIN";

    private Camera glassesCamera;
    private bool subscribed;
    private bool guideShown;
    private string lastRejectReason = "";

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
            Reject($"No tracked image named {markerName}. Seen: {DescribeImages(args)}", false);
            return;
        }

        if (image.trackingState != TrackingState.Tracking)
        {
            Reject($"{image.referenceImage.name} seen with trackingState={image.trackingState}; waiting for Tracking", false);
            return;
        }

        if (!TryBuildRoomPose(image, out Pose worldPose))
            return;

        if (roomCoordinateSystem.TryCalibrateFromPose(markerName, worldPose, out string message))
        {
            lastRejectReason = "";
            Debug.Log($"[ImageMarkerRoomCalibrator] {message} from tracked image at {worldPose.position}");
        }
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
        {
            Reject($"{markerName} marker orientation is degenerate (up={imageTransform.up}, forward={imageTransform.forward})", true);
            return false;
        }

        forward.Normalize();

        if (!lyingFlat)
        {
            if (glassesCamera == null)
                glassesCamera = Camera.main;

            if (glassesCamera == null)
            {
                Reject("No main camera; the wall normal sign cannot be resolved", false);
                return false;
            }

            Vector3 viewerOffset = Vector3.ProjectOnPlane(
                glassesCamera.transform.position - imageTransform.position,
                Vector3.up);
            if (viewerOffset.magnitude < MinimumViewerOffsetMeters)
            {
                Reject($"Viewer is {viewerOffset.magnitude:F2} m from the marker horizontally; need {MinimumViewerOffsetMeters} m", false);
                return false;
            }

            if (Vector3.Dot(forward, viewerOffset) < 0f)
                forward = -forward;
        }

        worldPose = new Pose(imageTransform.position, Quaternion.LookRotation(forward, Vector3.up));
        return true;
    }

    private void Reject(string reason, bool showOnPanel)
    {
        if (string.Equals(reason, lastRejectReason, StringComparison.Ordinal))
            return;

        lastRejectReason = reason;
        Debug.LogWarning($"[ImageMarkerRoomCalibrator] {reason}");
        if (showOnPanel)
            RoomCoordinateSystem.PublishStatus(reason, Color.yellow);
    }

    private static string DescribeImages(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        foreach (ARTrackedImage image in args.added)
            builder.Append('+').Append(image.referenceImage.name).Append('/').Append(image.trackingState).Append(' ');
        foreach (ARTrackedImage image in args.updated)
            builder.Append('~').Append(image.referenceImage.name).Append('/').Append(image.trackingState).Append(' ');
        return builder.Length > 0 ? builder.ToString().TrimEnd() : "none";
    }
}
