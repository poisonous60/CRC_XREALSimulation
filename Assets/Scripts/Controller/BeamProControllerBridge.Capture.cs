using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{
    private void HandleCaptureStateChanged(
        string message,
        bool recording)
    {
        if (controllerCaptureStatusText != null)
            controllerCaptureStatusText.text = message;

        if (controllerRecordButtonText != null)
        {
            controllerRecordButtonText.text =
                recording
                    ? "Stop Record"
                    : "Start Record";
        }

        workflowUiDirty = true;
    }

    public void ToggleVideoRecording()
    {
        ResolveReferences();

        if (captureManager == null)
        {
            Warn("XREALCaptureManager not found.");
            return;
        }

        captureManager.ToggleVideoRecording();
        Log("ToggleVideoRecording");
    }

    public void TakeJpegPhoto()
    {
        ResolveReferences();

        if (captureManager == null)
        {
            Warn("XREALCaptureManager not found.");
            return;
        }

        captureManager.TakeJpegPhoto();
        Log("TakeJpegPhoto");
    }
}
