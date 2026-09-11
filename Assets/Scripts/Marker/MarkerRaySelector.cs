using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which marker a controller ray is pointing at. A direct hit wins over a near miss.
/// </summary>
public class MarkerRaySelector
{
    public float maxDistanceMeters = 10f;
    public float selectionAngleDegrees = 1.5f;
    public float radiusMultiplier = 1.25f;
    public float fixedMarkerSize = 0.2f;

    // A computed source disc is a live readout; moving it would save a source position.
    public bool IsSelectable(SourceMarker marker)
    {
        return marker != null &&
               marker.root != null &&
               marker.isVisible &&
               marker.isPlaced &&
               !string.Equals(marker.lastPlacementMethod, DetectorWorldMarkerManager.ComputedPlacementMethod, StringComparison.Ordinal) &&
               !marker.isFollowingPlacementOrigin &&
               marker.centerVisualRequested &&
               marker.renderer != null &&
               marker.renderer.enabled;
    }

    public SourceMarker Select(Ray pointerRay, Dictionary<string, SourceMarker> markers)
    {
        Vector3 rayDirection = pointerRay.direction;
        if (markers == null || !IsFiniteVector(rayDirection) || rayDirection.sqrMagnitude < 0.0001f)
            return null;

        rayDirection.Normalize();
        float maximumDistance = Mathf.Max(0.5f, maxDistanceMeters);
        float angularAllowance = Mathf.Tan(
            Mathf.Clamp(selectionAngleDegrees, 0.25f, 8f) * Mathf.Deg2Rad);
        bool selectedDirectHit = false;
        float closestEntryDepth = float.PositiveInfinity;
        float closestNormalizedMiss = float.PositiveInfinity;
        SourceMarker selectedMarker = null;

        foreach (KeyValuePair<string, SourceMarker> pair in markers)
        {
            SourceMarker marker = pair.Value;
            if (!IsSelectable(marker))
                continue;

            Vector3 toMarker = marker.root.transform.position - pointerRay.origin;
            float depth = Vector3.Dot(toMarker, rayDirection);
            if (depth <= 0.02f || depth > maximumDistance)
                continue;

            float perpendicularSquared = Mathf.Max(0f, toMarker.sqrMagnitude - depth * depth);
            float visibleRadius = Mathf.Max(0.01f, fixedMarkerSize * 0.5f);
            if (marker.renderer != null)
            {
                Vector3 extents = marker.renderer.bounds.extents;
                visibleRadius = Mathf.Max(
                    visibleRadius,
                    Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)));
            }

            float hitRadius = visibleRadius * Mathf.Max(1f, radiusMultiplier);
            bool directHit = perpendicularSquared <= hitRadius * hitRadius;
            float allowedRadius = Mathf.Max(hitRadius, depth * angularAllowance);
            if (perpendicularSquared > allowedRadius * allowedRadius)
                continue;

            float normalizedMiss = Mathf.Sqrt(perpendicularSquared) / Mathf.Max(0.001f, depth);
            float entryDepth = directHit
                ? depth - Mathf.Sqrt(Mathf.Max(0f, hitRadius * hitRadius - perpendicularSquared))
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

        return selectedMarker;
    }

    public static bool IsFiniteVector(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
