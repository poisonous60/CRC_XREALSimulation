using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Everything the app does while the XREAL glasses are off: active placements and QR scans are
/// cancelled, new placements are refused, and plane and image tracking pause until they are back on.
/// </summary>
public static class GlassesOffGuard
{
    private static ARPlaneManager pausedPlaneManager;
    private static PlaneDetectionMode pausedPlaneMode;
    private static ARTrackedImageManager pausedImageManager;
    private static ImageMarkerRoomCalibrator pausedCalibrator;

    // The glasses report only changes and the SDK has no getter for the current state, so they
    // count as worn until the first take-off.
    public static bool IsWorn { get; private set; } = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Subscribe()
    {
        IsWorn = true;
        pausedPlaneManager = null;
        pausedImageManager = null;
        pausedCalibrator = null;
        XREALCallbackHandler.OnXREALGlassesWearingState -= HandleWearingState;
        XREALCallbackHandler.OnXREALGlassesWearingState += HandleWearingState;
    }

    private static void HandleWearingState(XREALWearingStatus state)
    {
        bool worn = state == XREALWearingStatus.PUT_ON;

        if (worn == IsWorn)
            return;

        IsWorn = worn;

        if (worn)
            Resume();
        else
            Pause();
    }

    private static void Pause()
    {
        DetectorWorldMarkerManager markerManager = Object.FindFirstObjectByType<DetectorWorldMarkerManager>();

        if (markerManager != null && markerManager.HasActivePlacement)
            markerManager.TryCancelCurrentDetector(out _);

        RoomCoordinateSystem roomCoordinateSystem = Object.FindFirstObjectByType<RoomCoordinateSystem>();

        if (roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement)
            roomCoordinateSystem.TryCancelPendingPlacement(out _);

        QRScanner qrScanner = Object.FindFirstObjectByType<QRScanner>();

        if (qrScanner != null && qrScanner.IsScanActive)
            qrScanner.StopScanning();

        // The calibrator re-enables image tracking every frame, so it stops first.
        pausedCalibrator = Object.FindFirstObjectByType<ImageMarkerRoomCalibrator>();

        if (pausedCalibrator != null && pausedCalibrator.enabled)
            pausedCalibrator.enabled = false;
        else
            pausedCalibrator = null;

        pausedImageManager = Object.FindFirstObjectByType<ARTrackedImageManager>();

        if (pausedImageManager != null && pausedImageManager.enabled)
            pausedImageManager.enabled = false;
        else
            pausedImageManager = null;

        pausedPlaneManager = Object.FindFirstObjectByType<ARPlaneManager>();

        if (pausedPlaneManager == null || !pausedPlaneManager.enabled)
        {
            pausedPlaneManager = null;
            return;
        }

        // The manager's own getter reads the native mode back, which is the state being paused.
        pausedPlaneMode = markerManager != null
            ? markerManager.DesiredPlaneDetectionMode
            : pausedPlaneManager.requestedDetectionMode;
        // XREAL's plane provider has an empty Stop, so disabling the manager alone leaves native
        // plane finding running; the None mode reaches it through SetPlaneDetectionMode.
        pausedPlaneManager.requestedDetectionMode = PlaneDetectionMode.None;
        pausedPlaneManager.enabled = false;
    }

    private static void Resume()
    {
        if (pausedPlaneManager != null)
        {
            pausedPlaneManager.requestedDetectionMode = pausedPlaneMode;
            pausedPlaneManager.enabled = true;
        }

        if (pausedImageManager != null)
            pausedImageManager.enabled = true;

        if (pausedCalibrator != null)
            pausedCalibrator.enabled = true;

        pausedPlaneManager = null;
        pausedImageManager = null;
        pausedCalibrator = null;
    }
}
