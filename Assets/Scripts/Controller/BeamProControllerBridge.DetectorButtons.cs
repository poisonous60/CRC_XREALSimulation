using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{
    [Header("Detector Place Buttons")]
    [Tooltip("Panel-pixel gap between two rows. Row position and size come from the Source button in the prefab.")]
    [SerializeField, Min(0f)] private float detectorRowGapPixels = 20f;

    private readonly List<string> knownDetectorIds = new List<string>();
    private readonly List<Button> detectorButtons = new List<Button>();
    private bool detectorButtonsDirty;

    private void TrackDetectorIds(IReadOnlyDictionary<string, float> deviceData)
    {
        if (deviceData == null)
            return;

        foreach (KeyValuePair<string, float> pair in deviceData)
        {
            string detectorId = DetectorId.Normalize(pair.Key);

            if (string.IsNullOrEmpty(detectorId) || IsDetectorIdKnown(detectorId))
                continue;

            knownDetectorIds.Add(detectorId);
            detectorButtonsDirty = true;
        }

        if (detectorButtonsDirty)
            knownDetectorIds.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private bool IsDetectorIdKnown(string detectorId)
    {
        for (int index = 0; index < knownDetectorIds.Count; index++)
        {
            if (DetectorId.Equals(knownDetectorIds[index], detectorId))
                return true;
        }

        return false;
    }

    private void UpdateDetectorButtons()
    {
        if (!ControllerButtonOverride.ShowDetectorPlaceButtons)
        {
            if (detectorButtons.Count > 0)
                ClearDetectorButtons();

            return;
        }

        if (detectorButtonsDirty)
            RebuildDetectorButtons();

        bool connected = radiationReceiver != null && radiationReceiver.IsConnected;

        for (int index = 0; index < detectorButtons.Count; index++)
        {
            Button button = detectorButtons[index];

            if (button != null && button.interactable != connected)
                button.interactable = connected;
        }
    }

    // Cloning the Source button keeps the row's image, hold handler and text child wired
    // without a second template inside the controller prefab.
    private void RebuildDetectorButtons()
    {
        if (controllerSourceActionButton == null)
            return;

        ClearDetectorButtons();
        detectorButtonsDirty = false;

        RectTransform templateRect = controllerSourceActionButton.transform as RectTransform;
        if (templateRect == null)
            return;

        Vector3 templateScale = templateRect.localScale;
        Vector2 templateCenter = templateRect.anchoredPosition;
        float rowStride =
            templateRect.sizeDelta.y * Mathf.Abs(templateScale.y) + detectorRowGapPixels;

        for (int index = 0; index < knownDetectorIds.Count; index++)
        {
            string detectorId = knownDetectorIds[index];

            GameObject row = Instantiate(
                controllerSourceActionButton.gameObject,
                templateRect.parent);
            row.name = $"DetectorPlaceButton_{detectorId}";
            row.SetActive(true);

            RectTransform rect = row.transform as RectTransform;
            rect.localScale = templateScale;
            rect.anchoredPosition = templateCenter + Vector2.down * (index * rowStride);

            SourceHoldPlaceButton hold = row.GetComponent<SourceHoldPlaceButton>();
            if (hold != null)
                hold.SetDetectorId(detectorId);

            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = $"{detectorId} Place";
                ConfigureWorkflowButtonText(label);
            }

            detectorButtons.Add(row.GetComponent<Button>());
        }
    }

    // The controller prefab outlives the scene, so rows cleared on a scene change have to
    // be rebuilt when a scene that wants them comes back with the same Detector IDs.
    private void ClearDetectorButtons()
    {
        for (int index = 0; index < detectorButtons.Count; index++)
        {
            if (detectorButtons[index] != null)
                Destroy(detectorButtons[index].gameObject);
        }

        detectorButtons.Clear();
        detectorButtonsDirty = true;
    }
}
