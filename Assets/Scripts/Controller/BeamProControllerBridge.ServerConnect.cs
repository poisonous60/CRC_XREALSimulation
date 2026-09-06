using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    /// <summary>
    /// Opens the server IP input flow on the receiver and focuses the controller input field if assigned.
    /// </summary>
    public void OpenIpInput()
    {
        if (controllerIpInputField == null)
        {
            Warn("Controller IP input field is missing.");
            return;
        }

        controllerIpInputField.Select();
        controllerIpInputField.ActivateInputField();
        controllerIpInputField.caretPosition =
            controllerIpInputField.text.Length;

        Log("Controller IP input focused");
    }

    /// <summary>
    /// Connects to the server. If the controller field is empty, the receiver's
    /// saved/default server IP is used.
    /// </summary>
    public void ConnectToServer()
    {
        ResolveReferences();

        if (radiationReceiver == null)
        {
            Warn("RadiationReceiver not found.");
            return;
        }

        string ip = controllerIpInputField != null
            ? controllerIpInputField.text.Trim()
            : radiationReceiver.CurrentServerIp;

        if (string.IsNullOrWhiteSpace(ip))
            ip = radiationReceiver.CurrentServerIp;

        if (string.IsNullOrWhiteSpace(ip))
        {
            Warn("Server IP is empty.");
            return;
        }

        radiationReceiver.ConnectToServerWithIp(ip);

        Log($"Connecting with controller IP: {ip}");
    }

    public void Connect()
    {
        ConnectToServer();
    }

    private void HandleControllerIpEndEdit(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return;

        ConnectToServer();
    }

    private void RegisterControllerIpListener()
    {
        if (controllerIpInputField == null)
            return;

        // Remove first so re-enabling the controller cannot register duplicates.
        controllerIpInputField.onEndEdit.RemoveListener(HandleControllerIpEndEdit);
        controllerIpInputField.onEndEdit.AddListener(HandleControllerIpEndEdit);
    }

    private void SyncControllerIpField()
    {
        if (controllerIpInputField == null || radiationReceiver == null)
            return;

        // Do not overwrite text while the user is editing it, but always replace
        // serialized prefab defaults with the last address saved by the receiver.
        if (!controllerIpInputField.isFocused)
            controllerIpInputField.SetTextWithoutNotify(radiationReceiver.CurrentServerIp);
    }

    private void ConnectIfNeeded()
    {
        if (radiationReceiver == null ||
            radiationReceiver.IsConnected ||
            radiationReceiver.IsConnecting)
        {
            return;
        }

        string ip = controllerIpInputField != null
            ? controllerIpInputField.text.Trim()
            : radiationReceiver.CurrentServerIp;

        if (!string.IsNullOrWhiteSpace(ip))
            radiationReceiver.ConnectToServerWithIp(ip);
    }
}
