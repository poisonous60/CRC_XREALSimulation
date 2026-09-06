using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ARDetectorHud
{

    private void PullCurrentReceiverState()
    {
        if (radiationReceiver == null)
            return;

        serverStatus = string.IsNullOrWhiteSpace(radiationReceiver.CurrentStatusMessage)
            ? (radiationReceiver.IsConnected ? "Connected" : "Disconnected")
            : radiationReceiver.CurrentStatusMessage;
        serverStatusColor = radiationReceiver.CurrentStatusColor;

        if (radiationReceiver.HasFreshRadiationData)
            ReplaceLatestDeviceData(radiationReceiver.LatestDeviceData);
        else
            latestDeviceData.Clear();
    }

    private void HandleServerStatusChanged(string message, Color color)
    {
        serverStatus = string.IsNullOrWhiteSpace(message) ? "Disconnected" : message;
        serverStatusColor = color;

        if (message.IndexOf("Connecting", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            latestDeviceData.Clear();
        }
    }

    private void HandleRadiationDataReceived(Dictionary<string, float> data)
    {
        ReplaceLatestDeviceData(data);
    }

    private void HandleRadiationDataFreshnessChanged(bool isFresh)
    {
        if (!isFresh)
            latestDeviceData.Clear();
    }

    private void ReplaceLatestDeviceData(IEnumerable<KeyValuePair<string, float>> data)
    {
        latestDeviceData.Clear();
        if (data == null)
            return;

        foreach (var pair in data)
        {
            string detectorId = string.IsNullOrWhiteSpace(pair.Key) ? "" : pair.Key.Trim();
            if (!string.IsNullOrEmpty(detectorId))
                latestDeviceData[detectorId] = pair.Value;
        }
    }
}
