using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

    private int lastConfiguredScreenWidth = -1;
    private int lastConfiguredScreenHeight = -1;
    private Rect lastConfiguredSafeArea = new Rect(-1f, -1f, -1f, -1f);

    private void ConfigureControllerDetectorList()
    {
        if (controllerRadiationDisplayText == null)
            return;

        float minFontSize = Mathf.Max(6f, detectorListMinFontSize);
        float maxFontSize = Mathf.Max(minFontSize, detectorListMaxFontSize);

        controllerRadiationDisplayText.enableAutoSizing = true;
        controllerRadiationDisplayText.fontSizeMin = minFontSize;
        controllerRadiationDisplayText.fontSizeMax = maxFontSize;
        controllerRadiationDisplayText.fontSize = maxFontSize;
        controllerRadiationDisplayText.alignment = TextAlignmentOptions.TopLeft;
        controllerRadiationDisplayText.textWrappingMode = TextWrappingModes.NoWrap;
        controllerRadiationDisplayText.overflowMode = TextOverflowModes.Overflow;
        controllerRadiationDisplayText.richText = false;
        controllerRadiationDisplayText.raycastTarget = false;
        controllerRadiationDisplayText.margin = new Vector4(8f, 8f, 8f, 8f);

        RectTransform rect = controllerRadiationDisplayText.rectTransform;
        Rect safeArea = Screen.safeArea;
        float safeLeft = 0f;
        float safeRight = 1f;

        if (Screen.width > 0 && safeArea.width > 0f)
        {
            safeLeft = Mathf.Clamp01(safeArea.xMin / Screen.width);
            safeRight = Mathf.Clamp01(safeArea.xMax / Screen.width);
        }

        if (safeRight <= safeLeft)
        {
            safeLeft = 0f;
            safeRight = 1f;
        }

        float safeMidpoint = (safeLeft + safeRight) * 0.5f;
        rect.anchorMin = new Vector2(safeLeft, 0.5f);
        rect.anchorMax = new Vector2(safeMidpoint, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, detectorListCenterYOffset);
        rect.sizeDelta = new Vector2(
            -2f * Mathf.Max(0f, detectorListHorizontalPadding),
            Mathf.Max(128f, detectorListHeight));

        lastConfiguredScreenWidth = Screen.width;
        lastConfiguredScreenHeight = Screen.height;
        lastConfiguredSafeArea = safeArea;
    }

    private void ConfigureControllerWorkflowGuide()
    {
        if (controllerStatusText == null)
            return;

        float minFontSize = Mathf.Max(8f, workflowGuideMinFontSize);
        float maxFontSize = Mathf.Max(minFontSize, workflowGuideMaxFontSize);

        controllerStatusText.enableAutoSizing = true;
        controllerStatusText.fontSizeMin = minFontSize;
        controllerStatusText.fontSizeMax = maxFontSize;
        controllerStatusText.fontSize = maxFontSize;
        controllerStatusText.alignment = TextAlignmentOptions.MidlineLeft;
        controllerStatusText.textWrappingMode = TextWrappingModes.Normal;
        controllerStatusText.overflowMode = TextOverflowModes.Ellipsis;
        controllerStatusText.richText = false;
        controllerStatusText.raycastTarget = false;
        controllerStatusText.margin = new Vector4(12f, 8f, 12f, 8f);

        Rect safeArea = Screen.safeArea;
        float safeLeft = 0f;
        float safeRight = 1f;
        if (Screen.width > 0 && safeArea.width > 0f)
        {
            safeLeft = Mathf.Clamp01(safeArea.xMin / Screen.width);
            safeRight = Mathf.Clamp01(safeArea.xMax / Screen.width);
        }

        if (safeRight <= safeLeft)
        {
            safeLeft = 0f;
            safeRight = 1f;
        }

        float safeMidpoint = (safeLeft + safeRight) * 0.5f;
        RectTransform rect = controllerStatusText.rectTransform;
        rect.anchorMin = new Vector2(safeMidpoint, 0.5f);
        rect.anchorMax = new Vector2(safeRight, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, workflowGuideCenterYOffset);
        rect.sizeDelta = new Vector2(
            -2f * Mathf.Max(0f, detectorListHorizontalPadding),
            Mathf.Max(128f, workflowGuideHeight));

        lastConfiguredScreenWidth = Screen.width;
        lastConfiguredScreenHeight = Screen.height;
        lastConfiguredSafeArea = safeArea;
    }

    private void ConfigureWorkflowButtonLabels()
    {
        ConfigureWorkflowButtonText(controllerQrScanButtonText);
        ConfigureWorkflowButtonText(controllerPlaceButtonText);
        ConfigureWorkflowButtonText(controllerCancelButtonText);

        if (controllerIpInputField == null)
            return;

        if (controllerIpInputField.placeholder is TMP_Text placeholderText)
        {
            placeholderText.text = "Server IP";
            ConfigureSingleLineText(
                placeholderText,
                workflowButtonMinFontSize,
                workflowButtonMaxFontSize);
        }

        if (controllerIpInputField.textComponent != null)
        {
            ConfigureSingleLineText(
                controllerIpInputField.textComponent,
                workflowButtonMinFontSize,
                workflowButtonMaxFontSize);
        }
    }

    private void ConfigureWorkflowButtonText(TMP_Text label)
    {
        if (label == null)
            return;

        ConfigureSingleLineText(
            label,
            workflowButtonMinFontSize,
            workflowButtonMaxFontSize);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = new Vector4(3f, 1f, 3f, 1f);
    }

    private static void ConfigureSingleLineText(
        TMP_Text label,
        float requestedMinFontSize,
        float requestedMaxFontSize)
    {
        float minFontSize = Mathf.Max(6f, requestedMinFontSize);
        float maxFontSize = Mathf.Max(minFontSize, requestedMaxFontSize);
        label.enableAutoSizing = true;
        label.fontSizeMin = minFontSize;
        label.fontSizeMax = maxFontSize;
        label.fontSize = maxFontSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.richText = false;
    }
}
