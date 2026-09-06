using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ARDetectorHud
{

    private void EnsureReferences()
    {
        if (markerManager == null)
            markerManager = GetComponent<DetectorWorldMarkerManager>();

        if (markerManager == null)
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>();

        if (radiationReceiver == null)
        {
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();
            if (radiationReceiver != null)
                PullCurrentReceiverState();
        }

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void SubscribeEvents()
    {
        if (eventsSubscribed)
            return;

        RadiationReceiver.OnServerStatusChanged += HandleServerStatusChanged;
        RadiationReceiver.OnRadiationDataReceived += HandleRadiationDataReceived;
        RadiationReceiver.OnRadiationDataFreshnessChanged +=
            HandleRadiationDataFreshnessChanged;
        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
            return;

        RadiationReceiver.OnServerStatusChanged -= HandleServerStatusChanged;
        RadiationReceiver.OnRadiationDataReceived -= HandleRadiationDataReceived;
        RadiationReceiver.OnRadiationDataFreshnessChanged -=
            HandleRadiationDataFreshnessChanged;
        eventsSubscribed = false;
    }
}
