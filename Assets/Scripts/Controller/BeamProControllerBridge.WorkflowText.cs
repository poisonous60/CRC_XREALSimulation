using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    private void RefreshWorkflowUi(bool force)
    {
        float now = Time.unscaledTime;
        nextWorkflowRefreshTime = now + WorkflowRefreshIntervalSeconds;

        if (!string.IsNullOrEmpty(transientActionMessage) &&
            now >= transientActionExpiresAt)
        {
            transientActionMessage = "";
        }

        WorkflowPresentation presentation = BuildWorkflowPresentation();

        bool showGuide = !string.IsNullOrWhiteSpace(presentation.guideText);
        if (controllerStatusText != null &&
            controllerStatusText.gameObject.activeSelf != showGuide)
        {
            controllerStatusText.gameObject.SetActive(showGuide);
        }

        if (controllerStatusText != null &&
            (force || !string.Equals(
                lastRenderedWorkflowText,
                presentation.guideText,
                StringComparison.Ordinal)))
        {
            controllerStatusText.text = presentation.guideText;
            lastRenderedWorkflowText = presentation.guideText;
        }

        if (controllerStatusText != null)
            controllerStatusText.color = presentation.guideColor;

        ApplyWorkflowButton(
            controllerSourceActionButton,
            controllerSourceActionButtonText,
            presentation.actionLabel,
            presentation.actionEnabled);

        UpdateDetectorButtons();

        workflowUiDirty = false;
    }

    private static void ApplyWorkflowButton(
        Button button,
        TMP_Text label,
        string labelText,
        bool interactable)
    {
        if (label != null && !string.Equals(label.text, labelText, StringComparison.Ordinal))
            label.text = labelText;

        if (button != null && button.interactable != interactable)
            button.interactable = interactable;
    }

    private WorkflowPresentation BuildWorkflowPresentation()
    {
        bool serverAvailable = radiationReceiver != null;
        bool connected = serverAvailable && radiationReceiver.IsConnected;
        bool connecting = serverAvailable && radiationReceiver.IsConnecting;
        bool freshData =
            connected &&
            radiationReceiver.HasFreshRadiationData &&
            HasValidRadiationValue(radiationReceiver.LatestDeviceData);
        bool roomPending =
            roomCoordinateSystem != null && roomCoordinateSystem.HasPendingPlacement;
        bool roomPoseValid =
            roomPending && roomCoordinateSystem.HasValidPendingPose;
        bool roomCalibrated = !NeedsRoomQrScan();
        bool detectorPending =
            markerManager != null && markerManager.HasActivePlacement;
        bool detectorPoseValid =
            detectorPending && markerManager.HasValidActivePlacementPose;
        bool scanActive =
            (qrScanner != null && qrScanner.IsScanActive) ||
            (radiationReceiver != null && radiationReceiver.IsQrScanPending);
        bool captureBusy = captureManager != null && captureManager.IsBusy;

        int placedCount = markerManager != null
            ? markerManager.CurrentRoomPlacedDetectorCount
            : 0;
        string requiredAction = "";
        string requiredInstruction = "";
        Color guideColor = new Color(0.72f, 0.90f, 1f, 1f);

        bool hasBlockingActionStatus =
            !string.IsNullOrWhiteSpace(transientActionMessage) &&
            Time.unscaledTime < transientActionExpiresAt &&
            transientActionColor != Color.green &&
            transientActionColor != Color.white;

        if (ControllerButtonOverride.HidePlacementButtons)
        {
            if (!serverAvailable)
            {
                requiredAction = "SERVER SETUP REQUIRED";
                requiredInstruction = "Restart the app or check the RadiationReceiver setup.";
                guideColor = new Color(1f, 0.55f, 0.50f, 1f);
            }
            else if (connecting)
            {
                requiredAction = "WAIT FOR SERVER CONNECTION";
                requiredInstruction =
                    $"Connecting to {SafeUiValue(radiationReceiver.CurrentServerIp, "server")}.";
                guideColor = new Color(1f, 0.86f, 0.38f, 1f);
            }
            else if (!connected)
            {
                requiredAction = "CONNECT SERVER";
                requiredInstruction = "Check Server IP, then tap CONNECT.";
                guideColor = latestServerStatusColor.a > 0.01f
                    ? latestServerStatusColor
                    : new Color(1f, 0.55f, 0.50f, 1f);
            }
            else if (!roomCalibrated)
            {
                requiredAction = "LOOK AT THE ROOM MARKER";
                requiredInstruction = "The printed marker sets the room origin.";
            }
            else if (!freshData)
            {
                requiredAction = receivedRadiationThisConnection
                    ? "RESTORE THE CPS STREAM"
                    : "START THE CPS STREAM";
                requiredInstruction = "No fresh CPS is arriving from the server.";
                guideColor = new Color(1f, 0.78f, 0.34f, 1f);
            }
        }
        else if (hasBlockingActionStatus)
        {
            requiredAction = "ACTION REQUIRED";
            requiredInstruction = CompactUiMessage(transientActionMessage, 88);
            guideColor = transientActionColor;
        }
        else if (!serverAvailable)
        {
            requiredAction = "SERVER SETUP REQUIRED";
            requiredInstruction = "Restart the app or check the RadiationReceiver setup.";
            guideColor = new Color(1f, 0.55f, 0.50f, 1f);
        }
        else if (markerManager == null)
        {
            requiredAction = "APP SETUP NOT READY";
            requiredInstruction = "Restart the app; the placement manager is missing.";
            guideColor = new Color(1f, 0.55f, 0.50f, 1f);
        }
        else if (scanActive)
        {
            requiredAction = roomCalibrated
                ? "SCAN A DETECTOR STICKER"
                : "SCAN ROOM QR";
            requiredInstruction = roomCalibrated
                ? "Any Detector sticker starts the Source placement."
                : "Point the Beam Pro camera at the room reference QR.";
        }
        else if (roomPending)
        {
            string roomId = SafeUiValue(roomCoordinateSystem.PendingRoomId, "ROOM QR");
            if (roomPoseValid)
            {
                requiredAction = "TAP PLACE ROOM";
                requiredInstruction = $"{roomId} is aligned on the vertical wall.";
                guideColor = new Color(0.55f, 1f, 0.68f, 1f);
            }
            else
            {
                requiredAction = "AIM AT THE ROOM QR";
                requiredInstruction =
                    $"Center the glasses on {roomId} attached to a vertical wall.";
                guideColor = new Color(1f, 0.86f, 0.38f, 1f);
            }
        }
        else if (detectorPending)
        {
            if (detectorPoseValid)
            {
                requiredAction = "TAP PLACE SOURCE";
                requiredInstruction = "The gray preview sits on the Source.";
                guideColor = new Color(0.55f, 1f, 0.68f, 1f);
            }
            else
            {
                requiredAction = "AIM AT THE SOURCE";
                requiredInstruction =
                    "No surface detected; placing now uses the default distance.";
                guideColor = new Color(1f, 0.86f, 0.38f, 1f);
            }
        }
        else if (connecting)
        {
            requiredAction = "WAIT FOR SERVER CONNECTION";
            requiredInstruction = roomCalibrated
                ? $"Connecting to {SafeUiValue(radiationReceiver.CurrentServerIp, "server")}. ADD SOURCE already works."
                : $"Connecting to {SafeUiValue(radiationReceiver.CurrentServerIp, "server")}.";
            guideColor = new Color(1f, 0.86f, 0.38f, 1f);
        }
        else if (!connected)
        {
            requiredAction = "CONNECT SERVER";
            requiredInstruction = roomCalibrated
                ? "Check Server IP, then tap ADD SOURCE."
                : "Check Server IP, then tap CONNECT & SCAN.";
            guideColor = latestServerStatusColor.a > 0.01f
                ? latestServerStatusColor
                : new Color(1f, 0.55f, 0.50f, 1f);
        }
        else if (!roomCalibrated && (qrScanner == null || roomCoordinateSystem == null))
        {
            requiredAction = "APP SETUP NOT READY";
            requiredInstruction = "Restart the app before scanning a QR.";
            guideColor = new Color(1f, 0.55f, 0.50f, 1f);
        }
        else if (captureBusy && !roomCalibrated)
        {
            requiredAction = "FINISH CAMERA CAPTURE";
            requiredInstruction = "QR scanning is locked while the capture camera is busy.";
            guideColor = new Color(1f, 0.86f, 0.38f, 1f);
        }
        else if (!roomCalibrated)
        {
            string lastRoomId = SafeUiValue(roomCoordinateSystem.LastRoomId, "");
            requiredAction = "SCAN ROOM QR";
            requiredInstruction = string.IsNullOrEmpty(lastRoomId)
                ? "Tap SCAN ROOM QR."
                : $"Tap SCAN ROOM QR and scan {lastRoomId}.";
        }
        else if (placedCount == 0)
        {
            requiredAction = "ADD THE SOURCE";
            requiredInstruction = "Tap ADD SOURCE, then aim the glasses at the Source.";
        }
        else if (!freshData)
        {
            requiredAction = receivedRadiationThisConnection
                ? "RESTORE THE CPS STREAM"
                : "START THE CPS STREAM";
            requiredInstruction = receivedRadiationThisConnection
                ? "No fresh CPS is arriving from the server."
                : "The server must send a CPS snapshot before measurement starts.";
            guideColor = new Color(1f, 0.78f, 0.34f, 1f);
        }

        string guideText = string.IsNullOrWhiteSpace(requiredAction)
            ? ""
            : string.IsNullOrWhiteSpace(requiredInstruction)
                ? requiredAction
                : requiredAction + "\n" + requiredInstruction;

        return new WorkflowPresentation
        {
            guideText = guideText,
            guideColor = guideColor,
            actionLabel = scanActive
                ? "Scanning..."
                : roomPending
                    ? "Place Room"
                    : detectorPending
                        ? "Place"
                        : roomCalibrated
                            ? "Place"
                            : !connected
                                ? "Connect & Scan"
                                : "Scan Room QR",
            // Left interactable on an invalid pose: the only button greying out mid-placement
            // reads as a frozen app, and PlaceDetector answers a premature tap with a warning.
            actionEnabled = roomPending || detectorPending ||
                (radiationReceiver != null && markerManager != null &&
                 (roomCalibrated || (qrScanner != null && !captureBusy)) &&
                 !scanActive)
        };
    }

    private static bool HasValidRadiationValue(IReadOnlyDictionary<string, float> deviceData)
    {
        if (deviceData == null)
            return false;

        foreach (KeyValuePair<string, float> pair in deviceData)
        {
            if (pair.Value >= 0f && !float.IsNaN(pair.Value) && !float.IsInfinity(pair.Value))
                return true;
        }

        return false;
    }

    private static string SafeUiValue(string value, string fallback)
    {
        string safe = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
        return CompactUiMessage(safe, 32);
    }

    private static string CompactUiMessage(string message, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "";

        string compact = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        if (compact.Length <= maximumLength)
            return compact;

        return compact.Substring(0, Mathf.Max(1, maximumLength - 3)) + "...";
    }

    private struct WorkflowPresentation
    {
        public string guideText;
        public Color guideColor;
        public string actionLabel;
        public bool actionEnabled;
    }

    private void ShowControllerActionStatus(string message, Color color)
    {
        transientActionMessage = message ?? "";
        transientActionColor = color;
        transientActionExpiresAt =
            Time.unscaledTime + ActionStatusLifetimeSeconds;
        workflowUiDirty = true;
        RefreshWorkflowUi(true);
    }
}
