using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Averages each detector's readings, then draws the source disc at the strongest one.</summary>
[DisallowMultipleComponent]
public sealed class DetectorPeakScene : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Threshold and window length of the strongest-detector rule. Empty draws nothing.")]
    [SerializeField] private DetectorPeakConfig config;

    [Header("References")]
    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private RadiationReceiver radiationReceiver;

    private sealed class WindowSlot
    {
        public string serverTime;
        public readonly Dictionary<string, float> values =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private readonly List<WindowSlot> slots = new List<WindowSlot>();
    private readonly Dictionary<string, float> lastReadingTime =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> windowSums =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> windowCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private bool subscribed;
    private string winnerDetectorId = "";
    private float winnerMeanCps = -1f;
    private Vector3 shownSourceWorldPoint;
    private bool sourceShown;

    private void Awake()
    {
        ResolveReferences();

        if (config == null)
            Debug.LogWarning("[DetectorPeakScene] No DetectorPeakConfig assigned; the source disc is never drawn.");
    }

    private void OnEnable()
    {
        if (subscribed)
            return;

        RadiationReceiver.OnRadiationDataReceived += HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged += HandleServerConnectionChanged;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        RadiationReceiver.OnRadiationDataReceived -= HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged -= HandleServerConnectionChanged;
        subscribed = false;

        Reset();
    }

    private void LateUpdate()
    {
        if (!sourceShown)
            return;

        // A ROOM_ORIGIN re-alignment moves the winner's marker without a new reading.
        if (!TryResolveWinnerPoint(out Vector3 worldPoint))
        {
            HideSource();
            return;
        }

        if ((worldPoint - shownSourceWorldPoint).sqrMagnitude <= 1e-8f)
            return;

        ShowSource(worldPoint);
    }

    private void ResolveReferences()
    {
        if (markerManager == null)
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>();

        if (radiationReceiver == null)
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();
    }

    private void HandleServerConnectionChanged(bool connected)
    {
        Reset();
    }

    private void HandleRadiationDataReceived(Dictionary<string, float> deviceData)
    {
        if (config == null || deviceData == null)
            return;

        ResolveReferences();

        Push(radiationReceiver != null ? radiationReceiver.LatestServerTime : "", deviceData);
        PickWinner();

        if (TryResolveWinnerPoint(out Vector3 worldPoint))
            ShowSource(worldPoint);
        else
            HideSource();
    }

    private void Push(string serverTime, Dictionary<string, float> deviceData)
    {
        float now = Time.unscaledTime;

        // The mock server sends no time field, so its updates are grouped by wall-clock second.
        // The prefix keeps that key out of the server's own string space.
        string slotKey = string.IsNullOrEmpty(serverTime)
            ? "local:" + Mathf.FloorToInt(now).ToString(CultureInfo.InvariantCulture)
            : "server:" + serverTime;

        WindowSlot slot = null;

        for (int index = slots.Count - 1; index >= 0; index--)
        {
            if (string.Equals(slots[index].serverTime, slotKey, StringComparison.Ordinal))
            {
                slot = slots[index];
                break;
            }
        }

        if (slot == null)
        {
            slot = new WindowSlot { serverTime = slotKey };
            slots.Add(slot);
        }

        foreach (KeyValuePair<string, float> pair in deviceData)
        {
            string detectorId = DetectorId.Normalize(pair.Key);

            if (string.IsNullOrEmpty(detectorId) ||
                pair.Value < 0f ||
                float.IsNaN(pair.Value) ||
                float.IsInfinity(pair.Value))
            {
                continue;
            }

            slot.values[detectorId] = pair.Value;
            lastReadingTime[detectorId] = now;
        }

        while (slots.Count > config.WindowSeconds + 1)
            slots.RemoveAt(0);
    }

    private void PickWinner()
    {
        windowSums.Clear();
        windowCounts.Clear();

        winnerDetectorId = "";
        winnerMeanCps = -1f;

        // The newest slot is still being filled, so it is left out of the mean.
        int countedSlots = slots.Count - 1;

        if (countedSlots < config.WindowSeconds)
            return;

        for (int index = 0; index < countedSlots; index++)
        {
            foreach (KeyValuePair<string, float> pair in slots[index].values)
            {
                windowSums.TryGetValue(pair.Key, out float sum);
                windowCounts.TryGetValue(pair.Key, out int count);
                windowSums[pair.Key] = sum + pair.Value;
                windowCounts[pair.Key] = count + 1;
            }
        }

        float now = Time.unscaledTime;
        string bestDetectorId = "";
        float bestMean = -1f;

        foreach (KeyValuePair<string, float> pair in windowSums)
        {
            if (!lastReadingTime.TryGetValue(pair.Key, out float readingTime) ||
                now - readingTime > config.FreshnessSeconds)
            {
                continue;
            }

            float mean = pair.Value / Mathf.Max(1, windowCounts[pair.Key]);

            if (mean > bestMean)
            {
                bestMean = mean;
                bestDetectorId = pair.Key;
            }
        }

        winnerDetectorId = bestDetectorId;
        winnerMeanCps = bestMean;
    }

    private bool TryResolveWinnerPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        return config != null &&
               markerManager != null &&
               !string.IsNullOrEmpty(winnerDetectorId) &&
               winnerMeanCps >= config.ThresholdCps &&
               markerManager.TryGetPlacedMarkerPosition(winnerDetectorId, out worldPoint);
    }

    private void ShowSource(Vector3 worldPoint)
    {
        if (!markerManager.TryShowComputedSource(worldPoint, winnerMeanCps, out string resultMessage))
        {
            Debug.LogWarning($"[DetectorPeakScene] {resultMessage}");
            return;
        }

        shownSourceWorldPoint = worldPoint;
        sourceShown = true;
    }

    private void HideSource()
    {
        if (!sourceShown)
            return;

        sourceShown = false;

        if (markerManager != null)
            markerManager.HideComputedSource();
    }

    private void Reset()
    {
        slots.Clear();
        lastReadingTime.Clear();
        winnerDetectorId = "";
        winnerMeanCps = -1f;
        HideSource();
    }
}
