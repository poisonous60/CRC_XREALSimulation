using System.Collections.Generic;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Debug only. Logs every tracked image's state and axes on change and every few seconds, so a device log settles marker-orientation questions.
/// </summary>
[DisallowMultipleComponent]
public sealed class TrackedImageAxesLogger : MonoBehaviour
{
    private const float RepeatIntervalSeconds = 5f;
    private const float FlatNormalVerticalDot = 0.30f;

    [Header("References")]
    [Tooltip("Optional. The first ARTrackedImageManager in the scene is used when empty.")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    private readonly Dictionary<TrackableId, TrackingState> lastState = new Dictionary<TrackableId, TrackingState>();
    private readonly Dictionary<TrackableId, float> lastLogTime = new Dictionary<TrackableId, float>();
    private Camera glassesCamera;
    private bool subscribed;

    private void OnEnable()
    {
        if (subscribed)
            return;

        if (trackedImageManager == null)
            trackedImageManager = FindFirstObjectByType<ARTrackedImageManager>(FindObjectsInactive.Include);

        if (trackedImageManager == null)
        {
            Debug.LogWarning("[TrackedImageAxesLogger] No ARTrackedImageManager in the scene; nothing to log.");
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

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
    {
        LogImages(args.added, "added");
        LogImages(args.updated, "updated");

        foreach (KeyValuePair<TrackableId, ARTrackedImage> removed in args.removed)
        {
            lastState.Remove(removed.Key);
            lastLogTime.Remove(removed.Key);
            Debug.Log($"[TrackedImageAxesLogger] removed {removed.Key}");
        }
    }

    private void LogImages(ReadOnlyList<ARTrackedImage> images, string change)
    {
        foreach (ARTrackedImage image in images)
        {
            TrackableId id = image.trackableId;
            bool stateChanged =
                !lastState.TryGetValue(id, out TrackingState previous) ||
                previous != image.trackingState;
            bool intervalElapsed =
                !lastLogTime.TryGetValue(id, out float loggedAt) ||
                Time.unscaledTime - loggedAt >= RepeatIntervalSeconds;

            if (!stateChanged && !intervalElapsed)
                continue;

            lastState[id] = image.trackingState;
            lastLogTime[id] = Time.unscaledTime;
            Debug.Log(Describe(image, change));
        }
    }

    private string Describe(ARTrackedImage image, string change)
    {
        Transform imageTransform = image.transform;
        float normalY = Vector3.Dot(imageTransform.up, Vector3.up);
        bool lyingFlat = Mathf.Abs(normalY) > FlatNormalVerticalDot;

        if (glassesCamera == null)
            glassesCamera = Camera.main;

        string viewer = "camera=none";
        if (glassesCamera != null)
        {
            Vector3 offset = Vector3.ProjectOnPlane(
                glassesCamera.transform.position - imageTransform.position,
                Vector3.up);
            viewer = $"viewerOffset={offset.magnitude:F2}m";
        }

        return $"[TrackedImageAxesLogger] {change} {image.referenceImage.name} {image.trackingState} " +
               $"pos={imageTransform.position} up={imageTransform.up} forward={imageTransform.forward} " +
               $"normal.y={normalY:F2} flat={lyingFlat} size={image.size} {viewer}";
    }
}
