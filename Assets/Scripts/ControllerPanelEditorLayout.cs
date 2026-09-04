using UnityEngine;

/// <summary>
/// Editor-only replacement for XREALControllerLayout that re-docks the Beam Pro panel after the CanvasScaler applies a new screen size.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ControllerPanelEditorLayout : MonoBehaviour
{
    [Header("Editor Layout")]
    [Tooltip("Trigger pad rect. Its scaled width sets the panel width.")]
    [SerializeField] private RectTransform triggerRect;

    [Tooltip("Panel scale while running in the editor. Ignored in builds.")]
    [SerializeField] private Vector2 editorRunningScale = new Vector2(0.5f, 0.5f);

#if UNITY_EDITOR
    private RectTransform panelRect;
    private Canvas rootCanvas;
    private Vector2 lastScreenSize;
    private float lastCanvasScale;

    private void Start()
    {
        panelRect = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>();
        panelRect.localScale = editorRunningScale;
        UpdateLayout();
    }

    private void LateUpdate()
    {
        // CanvasScaler applies a new screen size in preWillRenderCanvases, one frame after Screen changes.
        float canvasScale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        if (Screen.width != lastScreenSize.x ||
            Screen.height != lastScreenSize.y ||
            !Mathf.Approximately(canvasScale, lastCanvasScale))
        {
            UpdateLayout();
        }
    }

    private void UpdateLayout()
    {
        lastScreenSize = new Vector2(Screen.width, Screen.height);
        lastCanvasScale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
        if (triggerRect == null || panelRect.parent == null)
            return;

        panelRect.anchorMin = Vector2.one * 0.5f;
        panelRect.anchorMax = Vector2.one * 0.5f;
        float triggerWidth = triggerRect.sizeDelta.x * triggerRect.lossyScale.x;
        float width = triggerWidth * 1.2f / panelRect.lossyScale.x;
        float height = Screen.height * editorRunningScale.y / panelRect.lossyScale.y;
        panelRect.sizeDelta = new Vector2(width, height);

        float realWidth = width * panelRect.lossyScale.x;
        float realHeight = height * panelRect.lossyScale.y;
        Vector3 parentScale = panelRect.parent.lossyScale;
        panelRect.localPosition = new Vector2(
            (Screen.width * 0.5f - realWidth * 0.5f) / parentScale.x,
            (realHeight * 0.5f - Screen.height * 0.5f) / parentScale.y);
    }
#endif
}
