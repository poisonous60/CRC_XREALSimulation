using UnityEngine;

public partial class DetectorWorldMarkerManager
{
    private readonly GazePlaneProbe gazePlaneProbe = new GazePlaneProbe();

    private bool TryGetGazePlaneIntersection(
        out Vector3 hitPosition,
        out Quaternion hitRotation,
        out float hitDistance,
        bool requireVerticalSurface = false,
        float maximumVerticalNormalDot = 0.35f)
    {
        EnsurePlacementOrigin();
        EnsurePlaneDetectionManager();

        gazePlaneProbe.Configure(
            placementOrigin,
            planeManager,
            minPlaneHitDistanceMeters,
            maxPlaneHitDistanceMeters);

        return gazePlaneProbe.TryGetIntersection(
            out hitPosition,
            out hitRotation,
            out hitDistance,
            requireVerticalSurface,
            maximumVerticalNormalDot);
    }

    /// <summary>
    /// Exposes the same center-gaze/AR-plane pose used by detector placement so a
    /// ROOM_ORIGIN calibration cannot drift from the detector placement method.
    /// </summary>
    public bool TryGetCurrentGazePlanePose(
        out Vector3 hitPosition,
        out Quaternion hitRotation,
        out Vector3 surfaceNormal,
        out float hitDistance,
        bool requireVerticalSurface = false,
        float maximumVerticalNormalDot = 0.35f)
    {
        bool found = TryGetGazePlaneIntersection(
            out hitPosition,
            out hitRotation,
            out hitDistance,
            requireVerticalSurface,
            maximumVerticalNormalDot);

        surfaceNormal = found
            ? (hitRotation * Vector3.up).normalized
            : Vector3.zero;
        return found;
    }
}
