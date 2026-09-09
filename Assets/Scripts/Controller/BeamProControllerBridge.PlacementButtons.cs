using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    public bool HasPendingPlacement =>
        (roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement) ||
        (markerManager != null && markerManager.HasActivePlacement);

    public void CancelPendingPlacement()
    {
        ResolveReferences();

        bool scanActive =
            (qrScanner != null && qrScanner.IsScanActive) ||
            (radiationReceiver != null && radiationReceiver.IsQrScanPending);

        if (!HasPendingPlacement && !scanActive)
            return;

        CancelPlace();
    }

    public void PlaceDetector()
    {
        ResolveReferences();

        if (roomCoordinateSystem != null &&
            roomCoordinateSystem.HasPendingPlacement)
        {
            if (roomCoordinateSystem.TryConfirmPendingPlacement(out string roomMessage))
            {
                ShowControllerActionStatus(roomMessage, Color.green);
                Log(roomMessage);
            }
            else
            {
                ShowControllerActionStatus(roomMessage, Color.yellow);
                Warn(roomMessage);
            }

            return;
        }

        if (markerManager == null)
        {
            Warn("DetectorWorldMarkerManager not found. Cannot place detector.");
            return;
        }

        if (markerManager.TryPlaceCurrentDetector(out string detectorMessage))
        {
            ShowControllerActionStatus(detectorMessage, Color.green);
            Log(detectorMessage);
        }
        else
        {
            ShowControllerActionStatus(detectorMessage, Color.yellow);
            Warn(detectorMessage);
        }
    }

    public void PlaceCurrentDetector()
    {
        PlaceDetector();
    }

    public void CancelPlace()
    {
        ResolveReferences();

        bool qrCameraWasActive = qrScanner != null && qrScanner.IsScanActive;
        bool delayedScanWasPending =
            radiationReceiver != null && radiationReceiver.IsQrScanPending;
        bool scanWasActive = qrCameraWasActive || delayedScanWasPending;
        bool roomPlacementWasActive =
            roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement;
        bool placementWasActive = roomPlacementWasActive ||
                                  (markerManager != null && markerManager.HasActivePlacement);

        if (delayedScanWasPending)
            radiationReceiver.CancelPendingQrScan();

        if (qrCameraWasActive)
            qrScanner.StopScanning();

        if (scanWasActive && !placementWasActive)
        {
            ShowControllerActionStatus("QR scan cancelled", Color.white);
            Log("QR scan cancelled");
            return;
        }

        // ROOM_ORIGIN owns Cancel while its transaction is active. Returning here
        // is essential: cancellation must never fall through and delete the most
        // recently committed detector.
        if (roomPlacementWasActive && roomCoordinateSystem != null)
        {
            if (roomCoordinateSystem.TryCancelPendingPlacement(out string roomMessage))
            {
                ShowControllerActionStatus(roomMessage, Color.white);
                Log(roomMessage);
            }
            else
            {
                ShowControllerActionStatus(roomMessage, Color.yellow);
                Warn(roomMessage);
            }

            return;
        }

        if (markerManager == null)
        {
            Warn("DetectorWorldMarkerManager not found. Cannot cancel placement.");
            return;
        }

        if (markerManager.TryCancelCurrentDetector(out string resultMessage))
        {
            ShowControllerActionStatus(resultMessage, Color.white);
            Log(resultMessage);
        }
        else
        {
            ShowControllerActionStatus(resultMessage, Color.yellow);
            Warn(resultMessage);
        }
    }

    public void CancelPlacement()
    {
        CancelPlace();
    }

    public void ClearSavedMarkers()
    {
        ResolveReferences();

        if (markerManager == null)
        {
            Warn("DetectorWorldMarkerManager not found. Cannot clear markers.");
            return;
        }

        markerManager.ClearSavedMarkers();
        Log("ClearSavedMarkers");
    }

    public void PrintSavedCoordinates()
    {
        ResolveReferences();

        if (markerManager == null)
        {
            Warn("DetectorWorldMarkerManager not found. Cannot print coordinates.");
            return;
        }

        markerManager.PrintSavedCoordinatesToLog();
        Log("PrintSavedCoordinates");
    }
}
