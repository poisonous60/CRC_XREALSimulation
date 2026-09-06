using UnityEngine;

/// <summary>
/// Turns a gaze direction or a QR image point into the world pose a marker is placed at.
/// </summary>
public class PlacementGeometry
{
    public bool usePreviewCenterPlacement = true;
    public float markerVerticalOffsetMeters;
    public float cameraHorizontalFovDegrees = 70f;
    public float cameraVerticalFovDegrees = 50f;

    public bool useQrSizeToEstimateDistance;
    public float defaultPlacementDistanceMeters = 2f;
    public float realQrSizeMeters = 0.08f;
    public float qrEffectiveSizeRatio = 0.70f;
    public float distanceCalibrationMultiplier = 1f;
    public float minEstimatedDistanceMeters = 0.3f;
    public float maxEstimatedDistanceMeters = 5f;

    public Vector3 CalculateWorldPosition(
        Transform placementOrigin,
        Vector2 imagePoint,
        int imageWidth,
        int imageHeight,
        float distance)
    {
        if (placementOrigin == null)
            return Vector3.zero;

        if (usePreviewCenterPlacement)
        {
            Vector3 direct = placementOrigin.position + placementOrigin.forward.normalized * distance;
            direct += placementOrigin.up * markerVerticalOffsetMeters;
            return direct;
        }

        float viewportX = imageWidth > 0 ? Mathf.Clamp01(imagePoint.x / imageWidth) : 0.5f;
        float viewportY = imageHeight > 0 ? Mathf.Clamp01(1f - (imagePoint.y / imageHeight)) : 0.5f;

        float xFromCenter = viewportX - 0.5f;
        float yFromCenter = viewportY - 0.5f;

        float tanX = Mathf.Tan(cameraHorizontalFovDegrees * Mathf.Deg2Rad * 0.5f);
        float tanY = Mathf.Tan(cameraVerticalFovDegrees * Mathf.Deg2Rad * 0.5f);

        Vector3 direction =
            placementOrigin.forward +
            placementOrigin.right * (xFromCenter * 2f * tanX) +
            placementOrigin.up * (yFromCenter * 2f * tanY);

        direction.Normalize();

        Vector3 worldPosition = placementOrigin.position + direction * distance;
        worldPosition += placementOrigin.up * markerVerticalOffsetMeters;
        return worldPosition;
    }

    public Quaternion CalculateMarkerRotation(Transform placementOrigin, Vector3 worldPosition)
    {
        if (placementOrigin == null)
            return Quaternion.identity;

        Vector3 direction = worldPosition - placementOrigin.position;
        if (direction.sqrMagnitude < 0.0001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    public float GetPlacementDistance(int imageWidth, float qrPixelSize)
    {
        if (!useQrSizeToEstimateDistance || imageWidth <= 0 || qrPixelSize <= 1f || realQrSizeMeters <= 0f)
            return defaultPlacementDistanceMeters;

        float horizontalFovRad = cameraHorizontalFovDegrees * Mathf.Deg2Rad;
        float focalLengthPixels = (imageWidth * 0.5f) / Mathf.Tan(horizontalFovRad * 0.5f);

        float effectiveQrSizeMeters = realQrSizeMeters * qrEffectiveSizeRatio;
        float estimatedDistance = (effectiveQrSizeMeters * focalLengthPixels) / qrPixelSize;
        estimatedDistance *= distanceCalibrationMultiplier;

        return Mathf.Clamp(estimatedDistance, minEstimatedDistanceMeters, maxEstimatedDistanceMeters);
    }
}
