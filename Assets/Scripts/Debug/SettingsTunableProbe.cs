using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Traces one settings-screen slider end to end: UI event, config write, and what the marker reads back.
/// </summary>
[DisallowMultipleComponent]
public class SettingsTunableProbe : MonoBehaviour
{
    private readonly List<RuntimeTunableField> tunables = new List<RuntimeTunableField>();
    private readonly Dictionary<string, float> lastTunableValues =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Slider> hookedSliders = new HashSet<Slider>();
    private readonly Dictionary<Transform, float> lastMarkerScales = new Dictionary<Transform, float>();
    private readonly Dictionary<DistanceProximityPresentation, float> lastPresentationSizes =
        new Dictionary<DistanceProximityPresentation, float>();

    private SettingsScreenBuilder builder;
    private bool reportedMissingConfig;
    private bool reportedMissingBuilder;

    private void OnEnable()
    {
        if (MarkVisualConfig.TryLoad(out MarkVisualConfig config))
        {
            Debug.Log(
                $"[SettingsTunableProbe] scene config = {config.name} " +
                $"id={config.GetInstanceID()} markerSize={config.MarkerSizeMeters}");
        }
        else
        {
            Debug.LogWarning("[SettingsTunableProbe] MarkVisualConfig.TryLoad failed at startup.");
        }
    }

    private void Update()
    {
        HookNewSliders();
        LogTunableChanges();
        LogMarkerScaleChanges();
        LogPresentationChanges();
    }

    private void HookNewSliders()
    {
        if (builder == null)
        {
            builder = FindFirstObjectByType<SettingsScreenBuilder>(FindObjectsInactive.Include);

            if (builder == null)
            {
                if (!reportedMissingBuilder)
                {
                    reportedMissingBuilder = true;
                    Debug.LogWarning("[SettingsTunableProbe] No SettingsScreenBuilder in the running scene yet.");
                }

                return;
            }

            Debug.Log($"[SettingsTunableProbe] found builder on {builder.gameObject.name}");
        }

        Slider[] sliders = builder.GetComponentsInChildren<Slider>(true);

        for (int index = 0; index < sliders.Length; index++)
        {
            Slider slider = sliders[index];

            if (slider == null || !hookedSliders.Add(slider))
                continue;

            string rowName = slider.transform.parent != null ? slider.transform.parent.name : slider.name;

            Debug.Log(
                $"[SettingsTunableProbe] hooked slider on {rowName} " +
                $"range={slider.minValue}..{slider.maxValue} value={slider.value} " +
                $"interactable={slider.interactable} handle={(slider.handleRect != null)} " +
                $"listeners={slider.onValueChanged.GetPersistentEventCount()}");

            Slider boundSlider = slider;
            string boundRowName = rowName;

            slider.onValueChanged.AddListener(value =>
                Debug.Log($"[SettingsTunableProbe] slider event {boundRowName} raw={value} read={boundSlider.value}"));
        }
    }

    private void LogTunableChanges()
    {
        if (!MarkVisualConfig.TryLoad(out MarkVisualConfig config))
        {
            if (!reportedMissingConfig)
            {
                reportedMissingConfig = true;
                Debug.LogWarning("[SettingsTunableProbe] MarkVisualConfig.TryLoad failed during Update.");
            }

            return;
        }

        RuntimeTunable.Collect(config, tunables);

        for (int index = 0; index < tunables.Count; index++)
        {
            RuntimeTunableField tunable = tunables[index];

            if (lastTunableValues.TryGetValue(tunable.SaveKey, out float previous) &&
                Mathf.Approximately(previous, tunable.Value))
            {
                continue;
            }

            lastTunableValues[tunable.SaveKey] = tunable.Value;
            Debug.Log($"[SettingsTunableProbe] config write {tunable.Label} = {tunable.Value} key={tunable.SaveKey}");
        }
    }

    private void LogMarkerScaleChanges()
    {
        SourceMarker[] markers =
            FindObjectsByType<SourceMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < markers.Length; index++)
        {
            SourceMarker marker = markers[index];

            if (marker == null)
                continue;

            Transform root = marker.transform;
            float scale = root.localScale.x;

            if (lastMarkerScales.TryGetValue(root, out float previous) && Mathf.Approximately(previous, scale))
                continue;

            lastMarkerScales[root] = scale;
            Debug.Log(
                $"[SettingsTunableProbe] marker {marker.detectorId} localScale.x={scale} " +
                $"lossy={root.lossyScale.x} active={marker.gameObject.activeInHierarchy}");
        }
    }

    private void LogPresentationChanges()
    {
        DistanceProximityPresentation[] presentations =
            FindObjectsByType<DistanceProximityPresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < presentations.Length; index++)
        {
            DistanceProximityPresentation presentation = presentations[index];

            if (presentation == null || presentation.Config == null)
                continue;

            float size = presentation.Config.TextSizePixels;

            if (lastPresentationSizes.TryGetValue(presentation, out float previous) &&
                Mathf.Approximately(previous, size))
            {
                continue;
            }

            lastPresentationSizes[presentation] = size;

            // The instance id says whether the slider and this presentation hold the same asset.
            Debug.Log(
                $"[SettingsTunableProbe] presentation {presentation.name} reads " +
                $"{presentation.Config.name} id={presentation.Config.GetInstanceID()} textSize={size} " +
                $"range={presentation.Config.MaximumVisibleDistanceMeters}");
        }
    }
}
