using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    private void ResolveReferences()
    {
        if (!autoFindReferences)
            return;

        if (markerManager == null)
            markerManager = UnityEngine.Object.FindFirstObjectByType<DetectorWorldMarkerManager>();

        if (roomCoordinateSystem == null)
            roomCoordinateSystem = UnityEngine.Object.FindFirstObjectByType<RoomCoordinateSystem>();

        if (qrScanner == null)
            qrScanner = UnityEngine.Object.FindFirstObjectByType<QRScanner>();

        if (radiationReceiver == null)
            radiationReceiver = UnityEngine.Object.FindFirstObjectByType<RadiationReceiver>();

        if (captureManager == null)
        {
            captureManager = UnityEngine.Object.FindFirstObjectByType<XREALCaptureManager>();
        }
    }

    private void ResolveControllerControls()
    {
        Transform searchRoot = transform.root;
        if (searchRoot == null)
            return;

        if (controllerQrScanButton == null)
            controllerQrScanButton = FindButtonByName(searchRoot, "QRScanButton");

        if (controllerPlaceButton == null)
            controllerPlaceButton = FindButtonByName(searchRoot, "PlaceDetectorButton");

        if (controllerCancelButton == null)
            controllerCancelButton = FindButtonByName(searchRoot, "CancelPlaceButton");

        if (controllerQrScanButtonText == null && controllerQrScanButton != null)
            controllerQrScanButtonText = controllerQrScanButton.GetComponentInChildren<TMP_Text>(true);

        if (controllerPlaceButtonText == null && controllerPlaceButton != null)
            controllerPlaceButtonText = controllerPlaceButton.GetComponentInChildren<TMP_Text>(true);

        if (controllerCancelButtonText == null && controllerCancelButton != null)
            controllerCancelButtonText = controllerCancelButton.GetComponentInChildren<TMP_Text>(true);

        ApplyPlacementButtonVisibility();
    }

    // The controller prefab outlives a scene under SupportMultiResume, so both the hiding
    // and the restoring branch run on every resolve instead of once at startup.
    private void ApplyPlacementButtonVisibility()
    {
        bool visible = !ControllerButtonOverride.HidePlacementButtons;

        SetButtonVisible(controllerQrScanButton, visible);
        SetButtonVisible(controllerPlaceButton, visible);
        SetButtonVisible(controllerCancelButton, visible);
    }

    private static void SetButtonVisible(Button button, bool visible)
    {
        if (button == null || button.gameObject.activeSelf == visible)
            return;

        button.gameObject.SetActive(visible);
    }

    private static Button FindButtonByName(Transform root, string buttonName)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate != null &&
                string.Equals(candidate.name, buttonName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }
}
