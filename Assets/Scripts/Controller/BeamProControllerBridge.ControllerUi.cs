using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class BeamProControllerBridge
{

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
    }

    private void ConfigureWorkflowButtonLabels()
    {
        ConfigureWorkflowButtonText(controllerSourceActionButtonText);

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
