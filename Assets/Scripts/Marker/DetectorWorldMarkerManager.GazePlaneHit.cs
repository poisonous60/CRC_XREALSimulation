using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private bool TryGetGazePlaneIntersection(
        out Vector3 hitPosition,
        out Quaternion hitRotation,
        out float hitDistance,
        bool requireVerticalSurface = false,
        float maximumVerticalNormalDot = 0.35f)
    {
        hitPosition = Vector3.zero;
        hitRotation = Quaternion.identity;
        hitDistance = 0f;

        EnsurePlacementOrigin();
        EnsurePlaneDetectionManager();

        if (placementOrigin == null || planeManager == null || !planeManager.enabled)
            return false;

        Vector3 gazeDirection = placementOrigin.forward.normalized;
        if (gazeDirection.sqrMagnitude < 0.0001f)
            return false;

        Ray gazeRay = new Ray(placementOrigin.position, gazeDirection);
        float closestDistance = float.PositiveInfinity;
        ARPlane closestPlane = null;
        Vector3 closestPosition = Vector3.zero;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane == null || !plane.gameObject.activeInHierarchy || plane.trackingState == TrackingState.None)
                continue;

            Vector3 planeNormal = plane.transform.up;

            // ROOM_ORIGIN must use a wall. Filter candidates before choosing the
            // nearest intersection; otherwise a nearer floor can permanently mask
            // the valid wall behind it.
            if (requireVerticalSurface &&
                Mathf.Abs(Vector3.Dot(planeNormal.normalized, Vector3.up)) >
                Mathf.Clamp01(maximumVerticalNormalDot))
            {
                continue;
            }

            float denominator = Vector3.Dot(gazeRay.direction, planeNormal);
            if (Mathf.Abs(denominator) < 0.0001f)
                continue;

            float distance = Vector3.Dot(plane.transform.position - gazeRay.origin, planeNormal) / denominator;
            if (distance < minPlaneHitDistanceMeters ||
                distance > maxPlaneHitDistanceMeters ||
                distance >= closestDistance)
            {
                continue;
            }

            Vector3 candidatePosition = gazeRay.GetPoint(distance);
            if (!IsPointInsidePlaneBoundary(plane, candidatePosition))
                continue;

            closestDistance = distance;
            closestPosition = candidatePosition;
            closestPlane = plane;
        }

        if (closestPlane == null)
            return false;

        hitPosition = closestPosition;
        hitRotation = closestPlane.transform.rotation;
        hitDistance = closestDistance;
        return true;
    }

    private bool IsPointInsidePlaneBoundary(ARPlane plane, Vector3 worldPoint)
    {
        Vector3 localPoint3D = plane.transform.InverseTransformPoint(worldPoint);
        Vector2 localPoint = new Vector2(localPoint3D.x, localPoint3D.z);
        var boundary = plane.boundary;

        if (boundary.IsCreated && boundary.Length >= 3)
        {
            bool inside = false;
            int previous = boundary.Length - 1;

            for (int current = 0; current < boundary.Length; current++)
            {
                Vector2 a = boundary[current];
                Vector2 b = boundary[previous];

                bool crossesY = (a.y > localPoint.y) != (b.y > localPoint.y);
                if (crossesY)
                {
                    float intersectionX =
                        (b.x - a.x) * (localPoint.y - a.y) /
                        (b.y - a.y) + a.x;

                    if (localPoint.x < intersectionX)
                        inside = !inside;
                }

                previous = current;
            }

            return inside;
        }

        // Boundary data can be briefly unavailable on the first tracking frame.
        // Fall back to the plane's rectangular size until its polygon arrives.
        Vector2 halfSize = plane.size * 0.5f;
        Vector3 planeCenter3D = plane.center;
        Vector2 planeCenter = new Vector2(planeCenter3D.x, planeCenter3D.z);
        Vector2 pointFromCenter = localPoint - planeCenter;
        return Mathf.Abs(pointFromCenter.x) <= halfSize.x &&
               Mathf.Abs(pointFromCenter.y) <= halfSize.y;
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
