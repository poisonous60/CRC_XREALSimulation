using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    private void HandleServerStatusChanged(
        string message,
        Color color)
    {
        ResolveReferences();

        // RadiationReceiver may Awake after this prefab. Its first status update
        // is a reliable point at which the PlayerPrefs-backed address is loaded.
        SyncControllerIpField();
        latestServerStatusColor = color;
        workflowUiDirty = true;
    }

    private void HandleDisplayTextChanged(string message)
    {
        workflowUiDirty = true;
        if (controllerRadiationDisplayText == null)
            return;

        ConfigureControllerDetectorList();
        controllerRadiationDisplayText.text = message;
    }

    private void HandleServerConnectionChanged(bool connected)
    {
        if (!connected)
            receivedRadiationThisConnection = false;
        else if (radiationReceiver != null && radiationReceiver.HasFreshRadiationData)
            receivedRadiationThisConnection = true;

        workflowUiDirty = true;
    }

    private void HandleRadiationDataFreshnessChanged(bool fresh)
    {
        if (fresh)
            receivedRadiationThisConnection = true;

        workflowUiDirty = true;
    }

    private void HandleRadiationDataReceived(Dictionary<string, float> deviceData)
    {
        receivedRadiationThisConnection = true;
        workflowUiDirty = true;
        TrackDetectorIds(deviceData);
    }

    private void SyncCurrentReceiverText()
    {
        if (radiationReceiver == null)
        {
            HandleDisplayTextChanged(WaitingForDataMessage);
            return;
        }

        TrackDetectorIds(radiationReceiver.LatestDeviceData);

        HandleServerStatusChanged(
            radiationReceiver.CurrentStatusMessage,
            radiationReceiver.CurrentStatusColor
        );

        HandleDisplayTextChanged(
            radiationReceiver.CurrentDisplayMessage
        );
    }

    private void HandleRoomStatusChanged(string message, Color color)
    {
        ShowControllerActionStatus(message, color);
    }
}
