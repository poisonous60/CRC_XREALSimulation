using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    public void StartQrScan()
    {
        ResolveReferences();

        if ((roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement) ||
            (markerManager != null && markerManager.HasActivePlacement))
        {
            Warn("Place or cancel the current preview first");
            return;
        }

        if (markerManager == null)
        {
            Warn("DetectorWorldMarkerManager not found. Cannot start a placement or a scan.");
            return;
        }

        if (!NeedsRoomQrScan())
        {
            ConnectIfNeeded();
            radiationReceiver?.CancelPendingQrScan();
            if (markerManager.TryBeginSourcePlacement(out string sourceMessage))
            {
                ShowControllerActionStatus(sourceMessage, Color.white);
                Log(sourceMessage);
            }
            else
            {
                ShowControllerActionStatus(sourceMessage, Color.yellow);
                Warn(sourceMessage);
            }
            return;
        }

        if (captureManager != null &&
            captureManager.IsBusy)
        {
            Warn(
                "Cannot start QR scan while capture camera is busy."
            );
            return;
        }

        // The controller prefab has no separate Connect button. Starting a scan
        // must therefore also use the entered/saved IP when no connection exists.
        ConnectIfNeeded();

        if (qrScanner == null)
        {
            Warn("QRScanner not found. Cannot start QR scan.");
            return;
        }

        // This button starts the scanner directly, so suppress the receiver's
        // delayed post-connect auto-start for the same connection attempt.
        radiationReceiver?.CancelPendingQrScan();
        qrScanner.StartScanning();
        Log("StartQrScan");
    }

    public void StopQrScan()
    {
        ResolveReferences();

        bool stoppedPendingStart =
            radiationReceiver != null && radiationReceiver.CancelPendingQrScan();

        if (qrScanner == null)
        {
            if (!stoppedPendingStart)
                Warn("QRScanner not found. Cannot stop QR scan.");
            return;
        }

        qrScanner.StopScanning();
        Log("StopQrScan");
    }

    public void RestartQrScan()
    {
        StopQrScan();
        StartQrScan();
    }

    private bool NeedsRoomQrScan()
    {
        bool roomRequired = markerManager == null || markerManager.RequiresRoomCalibration;
        bool roomCalibrated = roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated;
        return roomRequired && !roomCalibrated;
    }
}
