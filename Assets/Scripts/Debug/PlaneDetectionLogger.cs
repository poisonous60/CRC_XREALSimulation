using System.Collections.Generic;
using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Debug only. Logs plane changes, glasses put on and taken off, and every plane under the gaze when a placement
/// ends, so a device log shows which plane a detector landed on. The drawing of the planes is the f_02_plane_sheets look.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlaneDetectionLogger : MonoBehaviour
{
    private const float HeightChangeLogMeters = 0.02f;
    private const float MaxRayDistanceMeters = 10f;

    [Header("References")]
    [Tooltip("Optional. The first ARPlaneManager in the scene is used when empty.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("Optional. The end of its placement triggers the gaze log. The first one in the scene is used when empty.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    private readonly Dictionary<TrackableId, float> loggedHeight = new Dictionary<TrackableId, float>();
    private readonly Dictionary<TrackableId, TrackingState> loggedState = new Dictionary<TrackableId, TrackingState>();
    private bool placementWasActive;
    private bool subscribed;

    private void OnEnable()
    {
        XREALCallbackHandler.OnXREALGlassesWearingState += HandleWearingState;

        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        if (markerManager == null)
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>(FindObjectsInactive.Include);

        if (planeManager == null)
        {
            Debug.LogWarning("[PlaneDetectionLogger] No ARPlaneManager in the scene; nothing to log.");
            return;
        }

        planeManager.trackablesChanged.AddListener(HandleTrackablesChanged);
        subscribed = true;
    }

    private void OnDisable()
    {
        XREALCallbackHandler.OnXREALGlassesWearingState -= HandleWearingState;

        if (!subscribed)
            return;

        if (planeManager != null)
            planeManager.trackablesChanged.RemoveListener(HandleTrackablesChanged);
        subscribed = false;
    }

    private void LateUpdate()
    {
        if (planeManager == null)
            return;

        // DetectorWorldMarkerManager fixes a placement before LateUpdate, so the camera pose here is
        // the one the placement used.
        bool placementActive = markerManager != null && markerManager.HasActivePlacement;

        if (placementWasActive && !placementActive)
            LogPlanesUnderGaze();

        placementWasActive = placementActive;
    }

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
    {
        foreach (ARPlane plane in args.added)
        {
            Remember(plane);
            Debug.Log($"[PlaneDetectionLogger] added {Describe(plane)}");
        }

        foreach (ARPlane plane in args.updated)
        {
            bool heightMoved =
                plane.alignment.IsHorizontal() &&
                (!loggedHeight.TryGetValue(plane.trackableId, out float height) ||
                 Mathf.Abs(plane.center.y - height) >= HeightChangeLogMeters);
            bool stateChanged =
                !loggedState.TryGetValue(plane.trackableId, out TrackingState state) ||
                state != plane.trackingState;

            if (!heightMoved && !stateChanged)
                continue;

            Remember(plane);
            Debug.Log($"[PlaneDetectionLogger] updated {Describe(plane)}");
        }

        foreach (KeyValuePair<TrackableId, ARPlane> removed in args.removed)
        {
            loggedHeight.Remove(removed.Key);
            loggedState.Remove(removed.Key);
            Debug.Log($"[PlaneDetectionLogger] removed {removed.Key}");
        }
    }

    private void HandleWearingState(XREALWearingStatus state)
    {
        Camera glassesCamera = Camera.main;
        string head = glassesCamera != null ? glassesCamera.transform.position.ToString("F3") : "none";
        int planeCount = planeManager != null ? planeManager.trackables.count : 0;
        Debug.Log($"[PlaneDetectionLogger] glasses {state} head={head} planes={planeCount}");
    }

    private void Remember(ARPlane plane)
    {
        loggedHeight[plane.trackableId] = plane.center.y;
        loggedState[plane.trackableId] = plane.trackingState;
    }

    private void LogPlanesUnderGaze()
    {
        Camera glassesCamera = Camera.main;
        if (glassesCamera == null)
            return;

        Transform head = glassesCamera.transform;
        Ray gaze = new Ray(head.position, head.forward);

        // The manager's own probe, with its own filters and distances, names the plane placement picks.
        bool picked = markerManager.TryGetCurrentGazePlanePose(
            out Vector3 pickPosition, out Quaternion _, out Vector3 _, out float pickDistance);

        string pick = picked ? $"distance={pickDistance:F3} hitY={pickPosition.y:F3}" : "none (fallback distance)";
        Debug.Log($"[PlaneDetectionLogger] placement ended head={head.position:F3} gaze={head.forward:F3} planes={planeManager.trackables.count} pick={pick}");

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (!TryCrossGaze(plane, gaze, out float distance, out Vector3 hit))
                continue;

            bool inside = GazePlaneProbe.IsPointInsidePlaneBoundary(plane, hit);
            bool isPick = picked && (hit - pickPosition).sqrMagnitude < 0.000001f;
            string mark = isPick ? " <- placement pick" : "";
            Debug.Log($"[PlaneDetectionLogger]   crossed {Describe(plane)} distance={distance:F3} hitY={hit.y:F3} inside={inside}{mark}");
        }
    }

    private static bool TryCrossGaze(ARPlane plane, Ray gaze, out float distance, out Vector3 hit)
    {
        distance = 0f;
        hit = Vector3.zero;

        if (plane == null || !plane.gameObject.activeInHierarchy)
            return false;

        Vector3 normal = plane.normal;
        float denominator = Vector3.Dot(gaze.direction, normal);
        if (Mathf.Abs(denominator) < 0.0001f)
            return false;

        distance = Vector3.Dot(plane.transform.position - gaze.origin, normal) / denominator;
        if (distance <= 0f || distance > MaxRayDistanceMeters)
            return false;

        hit = gaze.GetPoint(distance);
        return true;
    }

    private static string Describe(ARPlane plane)
    {
        string subsumed = plane.subsumedBy != null ? $" subsumedBy={plane.subsumedBy.trackableId}" : "";
        return $"{plane.trackableId} {plane.alignment} {plane.trackingState} y={plane.center.y:F3} " +
               $"center={plane.center:F2} size={plane.size.x:F2}x{plane.size.y:F2} boundary={plane.boundary.Length}{subsumed}";
    }
}
