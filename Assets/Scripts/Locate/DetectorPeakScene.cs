using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>Averages each detector's readings, then draws the source disc at the strongest one, or at every one above the threshold.</summary>
[DisallowMultipleComponent]
public sealed class DetectorPeakScene : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Threshold, window length and single-or-all mode of the detector rule. Empty draws nothing.")]
    [SerializeField] private DetectorPeakConfig config;

    [Header("References")]
    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private RadiationReceiver radiationReceiver;

    // Key of the single Source Marker. In show-all mode each disc is keyed by its detector id instead.
    private const string SingleSourceKey = "";

    private sealed class WindowSlot
    {
        public string serverTime;
        public readonly Dictionary<string, float> values =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ShownSource
    {
        public string detectorId;
        public float meanCps;
        public Vector3 worldPoint;
    }

    private readonly List<WindowSlot> slots = new List<WindowSlot>();
    private readonly Dictionary<string, float> lastReadingTime =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> windowSums =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> windowCounts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> windowMeans =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, float> targetMeans =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShownSource> shownSources =
        new Dictionary<string, ShownSource>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> staleKeys = new List<string>();

    private bool subscribed;
    private bool shownAllAboveThreshold;
    private string winnerDetectorId = "";
    private float winnerMeanCps = -1f;

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
        // The settings checkbox writes the config directly, so a mode flip is noticed here, not on the next packet.
        if (config != null && config.ShowAllAboveThreshold != shownAllAboveThreshold)
        {
            shownAllAboveThreshold = config.ShowAllAboveThreshold;
            RefreshSources();
        }

        if (shownSources.Count == 0)
            return;

        staleKeys.Clear();

        // A ROOM_ORIGIN re-alignment moves the detector markers without a new reading.
        foreach (KeyValuePair<string, ShownSource> pair in shownSources)
        {
            ShownSource shown = pair.Value;

            if (!TryResolvePoint(shown.detectorId, out Vector3 worldPoint))
            {
                staleKeys.Add(pair.Key);
                continue;
            }

            if ((worldPoint - shown.worldPoint).sqrMagnitude <= 1e-8f)
                continue;

            Show(pair.Key, shown.detectorId, worldPoint, shown.meanCps);
        }

        HideKeys(staleKeys);
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
        ComputeWindowMeans();
        PickWinner();
        RefreshSources();
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

    private void ComputeWindowMeans()
    {
        windowSums.Clear();
        windowCounts.Clear();
        windowMeans.Clear();

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

        foreach (KeyValuePair<string, float> pair in windowSums)
        {
            if (!lastReadingTime.TryGetValue(pair.Key, out float readingTime) ||
                now - readingTime > config.FreshnessSeconds)
            {
                continue;
            }

            windowMeans[pair.Key] = pair.Value / Mathf.Max(1, windowCounts[pair.Key]);
        }
    }

    private void PickWinner()
    {
        string standingDetectorId = winnerDetectorId;
        string bestDetectorId = "";
        float bestMean = -1f;
        float standingMean = -1f;

        foreach (KeyValuePair<string, float> pair in windowMeans)
        {
            if (string.Equals(pair.Key, standingDetectorId, StringComparison.OrdinalIgnoreCase))
                standingMean = pair.Value;

            if (pair.Value > bestMean)
            {
                bestMean = pair.Value;
                bestDetectorId = pair.Key;
            }
        }

        // Poisson scatter alone reorders two detectors that read almost the same.
        if (standingMean >= 0f && bestMean < standingMean * (1f + config.SwitchMarginRatio))
        {
            bestDetectorId = standingDetectorId;
            bestMean = standingMean;
        }

        winnerDetectorId = bestDetectorId;
        winnerMeanCps = bestMean;
    }

    private void RefreshSources()
    {
        targetMeans.Clear();

        if (config.ShowAllAboveThreshold)
        {
            foreach (KeyValuePair<string, float> pair in windowMeans)
            {
                if (pair.Value >= config.ThresholdCps)
                    targetMeans[pair.Key] = pair.Value;
            }
        }
        else if (!string.IsNullOrEmpty(winnerDetectorId) && winnerMeanCps >= config.ThresholdCps)
        {
            targetMeans[SingleSourceKey] = winnerMeanCps;
        }

        staleKeys.Clear();

        foreach (KeyValuePair<string, ShownSource> pair in shownSources)
        {
            if (!targetMeans.ContainsKey(pair.Key))
                staleKeys.Add(pair.Key);
        }

        HideKeys(staleKeys);

        foreach (KeyValuePair<string, float> pair in targetMeans)
        {
            string detectorId = pair.Key == SingleSourceKey ? winnerDetectorId : pair.Key;

            if (TryResolvePoint(detectorId, out Vector3 worldPoint))
                Show(pair.Key, detectorId, worldPoint, pair.Value);
            else
                Hide(pair.Key);
        }
    }

    private bool TryResolvePoint(string detectorId, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        return markerManager != null &&
               !string.IsNullOrEmpty(detectorId) &&
               markerManager.TryGetPlacedMarkerPosition(detectorId, out worldPoint);
    }

    private void Show(string key, string detectorId, Vector3 worldPoint, float meanCps)
    {
        if (!markerManager.TryShowComputedSourceAt(key, worldPoint, meanCps, out string resultMessage))
        {
            Debug.LogWarning($"[DetectorPeakScene] {resultMessage}");
            return;
        }

        if (!shownSources.TryGetValue(key, out ShownSource shown))
        {
            shown = new ShownSource();
            shownSources.Add(key, shown);
        }

        shown.detectorId = detectorId;
        shown.meanCps = meanCps;
        shown.worldPoint = worldPoint;
    }

    private void Hide(string key)
    {
        if (!shownSources.Remove(key))
            return;

        if (markerManager != null)
            markerManager.HideComputedSourceAt(key);
    }

    private void HideKeys(List<string> keys)
    {
        for (int index = 0; index < keys.Count; index++)
            Hide(keys[index]);
    }

    private void HideAll()
    {
        staleKeys.Clear();
        staleKeys.AddRange(shownSources.Keys);
        HideKeys(staleKeys);
    }

    private void Reset()
    {
        slots.Clear();
        lastReadingTime.Clear();
        windowMeans.Clear();
        winnerDetectorId = "";
        winnerMeanCps = -1f;
        HideAll();
    }
}
