using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ARDetectorHud
{

    private void EnsureCanvas()
    {
        if (canvasObject != null)
            return;

        canvasObject = new GameObject(
            "ARDetectorHUD_Runtime",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.layer = 5;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 100;
        canvas.worldCamera = targetCamera;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        scaler.referencePixelsPerUnit = 100f;

        indicatorLayer = CreateFullScreenLayer(canvasRect);
        hudText = CreateHudText(canvasRect);
    }

    private RectTransform CreateFullScreenLayer(RectTransform parent)
    {
        GameObject layerObject = new GameObject("DetectorDirectionIndicators", typeof(RectTransform));
        layerObject.layer = 5;
        RectTransform rect = layerObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private TMP_Text CreateHudText(RectTransform parent)
    {
        GameObject textObject = new GameObject(
            "ServerAndDetectorStatusText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = 5;

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = hudViewportAnchor;
        rect.anchorMax = hudViewportAnchor;
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = hudPixelSize;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.BottomRight;
        text.fontSize = hudFontSize;
        text.color = Color.white;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;
        text.margin = new Vector4(8f, 8f, 8f, 8f);
        return text;
    }

    private void UpdateCanvasPoseFromRuntimeProjection()
    {
        float distance = Mathf.Max(0.2f, canvasDistanceMeters);
        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
        Vector3 bottomRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 0f, distance));
        Vector3 center = targetCamera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

        float visibleWorldHeight = Vector3.Distance(bottomLeft, topLeft);
        float visibleWorldWidth = Vector3.Distance(bottomLeft, bottomRight);
        float uniformScale = visibleWorldHeight / ReferenceHeight;

        // The scale matches the height alone, so a fixed 16:9 width put the left and right
        // edge anchors outside the viewport on any narrower aspect.
        if (visibleWorldHeight > 0f)
            canvasRect.sizeDelta = new Vector2(ReferenceHeight * visibleWorldWidth / visibleWorldHeight, ReferenceHeight);

        canvasRect.position = center;
        canvasRect.rotation = targetCamera.transform.rotation;
        canvasRect.localScale = Vector3.one * uniformScale;

        if (canvas.worldCamera != targetCamera)
            canvas.worldCamera = targetCamera;
    }
}
