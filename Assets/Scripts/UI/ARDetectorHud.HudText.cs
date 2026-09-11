using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ARDetectorHud
{

    private int FindGazeSelectedMarker()
    {
        int bestIndex = -1;
        float bestAngle = gazeSelectionAngleDegrees;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 cameraForward = targetCamera.transform.forward;

        for (int i = 0; i < markerStates.Count; i++)
        {
            Vector3 toMarker = markerStates[i].worldPosition - cameraPosition;
            if (Vector3.Dot(cameraForward, toMarker) <= 0f)
                continue;

            Vector3 viewport = targetCamera.WorldToViewportPoint(markerStates[i].worldPosition);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                continue;

            float angle = Vector3.Angle(cameraForward, toMarker);
            if (angle <= bestAngle)
            {
                bestAngle = angle;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void UpdateHudText(int selectedMarkerIndex)
    {
        if (hudText == null)
            return;

        string selectedId = selectedMarkerIndex >= 0
            ? markerStates[selectedMarkerIndex].detectorId
            : "";
        Color selectedColor = selectedMarkerIndex >= 0
            ? markerStates[selectedMarkerIndex].color
            : Color.white;

        textBuilder.Clear();
        textBuilder.Append("<color=#")
            .Append(ColorUtility.ToHtmlStringRGB(serverStatusColor))
            .Append("><b>SERVER</b>  ")
            .Append(EscapeRichText(serverStatus))
            .Append("</color>");

        BuildDisplayDetectorIndex();

        if (sortedDisplayDetectorIds.Count == 0)
        {
            textBuilder.Append("\n<color=#B8B8B8>Waiting for detector data...</color>");
        }
        else
        {
            textBuilder.Append("\n<color=#A8A8A8><b>DETECTOR   CPS</b></color>");
            int selectedDisplayIndex = FindDisplayDetectorIndex(selectedId);
            int rowLimit = maxVisibleDetectorRows > 0 ? maxVisibleDetectorRows : 9;
            int visibleRowCount = Mathf.Min(
                sortedDisplayDetectorIds.Count,
                rowLimit);

            for (int rowIndex = 0; rowIndex < visibleRowCount; rowIndex++)
            {
                int displayIndex = rowIndex;
                if (rowIndex == visibleRowCount - 1 && selectedDisplayIndex >= visibleRowCount)
                    displayIndex = selectedDisplayIndex;

                string detectorId = sortedDisplayDetectorIds[displayIndex];
                bool selected = string.Equals(detectorId, selectedId, StringComparison.OrdinalIgnoreCase);
                bool hasPlacedMarker =
                    markerIndexByDetectorId.TryGetValue(detectorId, out int markerIndex);
                DetectorWorldMarkerManager.DetectorHudMarkerState markerState = hasPlacedMarker
                    ? markerStates[markerIndex]
                    : default;

                textBuilder.Append("\n");
                if (selected)
                {
                    textBuilder.Append("<color=#")
                        .Append(ColorUtility.ToHtmlStringRGB(selectedColor))
                        .Append("><b>> ");
                }
                else
                {
                    textBuilder.Append("<color=#E0E0E0>  ");
                }

                textBuilder.Append(EscapeRichText(detectorId)).Append("   ");

                bool hasRadiation =
                    latestDeviceData.TryGetValue(detectorId, out float radiationValue) &&
                    radiationValue >= 0f &&
                    IsFinite(radiationValue);

                if (!hasRadiation && hasPlacedMarker)
                {
                    radiationValue = markerState.radiationValue;
                    hasRadiation = radiationValue >= 0f && IsFinite(radiationValue);
                }

                if (hasRadiation)
                    textBuilder.Append(radiationValue.ToString("F3"));
                else
                    textBuilder.Append("--");

                if (!string.IsNullOrWhiteSpace(radiationUnit))
                    textBuilder.Append(" ").Append(EscapeRichText(radiationUnit));

                textBuilder.Append(selected ? "</b></color>" : "</color>");
            }

            int hiddenRowCount = sortedDisplayDetectorIds.Count - visibleRowCount;
            if (hiddenRowCount > 0)
            {
                textBuilder.Append("\n<color=#909090>+")
                    .Append(hiddenRowCount)
                    .Append(" more detector")
                    .Append(hiddenRowCount == 1 ? "" : "s")
                    .Append("</color>");
            }
        }

        hudText.fontSize = hudFontSize;
        hudText.text = textBuilder.ToString();
    }

    private void BuildDisplayDetectorIndex()
    {
        markerIndexByDetectorId.Clear();
        displayDetectorIdSet.Clear();
        sortedDisplayDetectorIds.Clear();

        // Add placed markers first so their canonical/display casing wins when the
        // server sends the same ID with different casing.
        for (int i = 0; i < markerStates.Count; i++)
        {
            string detectorId = NormalizeDetectorId(markerStates[i].detectorId);
            if (string.IsNullOrEmpty(detectorId) || IsSourceMarkerId(detectorId))
                continue;

            markerIndexByDetectorId[detectorId] = i;
            if (displayDetectorIdSet.Add(detectorId))
                sortedDisplayDetectorIds.Add(detectorId);
        }

        int placedDetectorCount = sortedDisplayDetectorIds.Count;

        foreach (string detectorId in latestDeviceData.Keys)
        {
            if (IsSourceMarkerId(detectorId))
                continue;

            if (displayDetectorIdSet.Add(detectorId))
                sortedDisplayDetectorIds.Add(detectorId);
        }

        // Keep every placed detector ahead of server-only rows so finite HUD space
        // is used for entries that have a sphere in the room.
        if (placedDetectorCount > 1)
            sortedDisplayDetectorIds.Sort(0, placedDetectorCount, StringComparer.OrdinalIgnoreCase);

        int serverOnlyDetectorCount = sortedDisplayDetectorIds.Count - placedDetectorCount;
        if (serverOnlyDetectorCount > 1)
        {
            sortedDisplayDetectorIds.Sort(
                placedDetectorCount,
                serverOnlyDetectorCount,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    private int FindDisplayDetectorIndex(string detectorId)
    {
        if (string.IsNullOrEmpty(detectorId))
            return -1;

        for (int i = 0; i < sortedDisplayDetectorIds.Count; i++)
        {
            if (string.Equals(sortedDisplayDetectorIds[i], detectorId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private bool IsSourceMarkerId(string detectorId)
    {
        if (markerManager != null)
            return markerManager.IsSourceMarkerKey(detectorId);

        return string.Equals(NormalizeDetectorId(detectorId), "SOURCE", StringComparison.OrdinalIgnoreCase);
    }

    private string NormalizeDetectorId(string detectorId)
    {
        return string.IsNullOrWhiteSpace(detectorId) ? "" : detectorId.Trim();
    }

    private bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private string EscapeRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return value.Replace("<", "[").Replace(">", "]");
    }
}
