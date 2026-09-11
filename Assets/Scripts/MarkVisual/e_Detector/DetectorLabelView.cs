using TMPro;
using UnityEngine;

/// <summary>(e) detector look: the detector's name and newest reading, floating above its point.</summary>
[DisallowMultipleComponent]
public class DetectorLabelView : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Text size, offset and color for this look. Empty draws nothing.")]
    [SerializeField] private DetectorLabelConfig config;

    private SourceMarker marker;
    private Camera head;
    private TMP_Text labelText;
    private float sampleLineHeight;
    private string shownDetectorId;
    private int shownCps = int.MinValue;

    public DetectorLabelConfig Config => config;

    private void OnDestroy()
    {
        if (labelText != null)
            Destroy(labelText.gameObject);
    }

    private void LateUpdate()
    {
        if (config == null)
            return;

        if (marker == null)
            marker = GetComponent<SourceMarker>();

        if (head == null)
            head = Camera.main;

        if (marker == null || head == null)
            return;

        EnsureText();

        if (labelText.gameObject.activeSelf != marker.isVisible)
            labelText.gameObject.SetActive(marker.isVisible);

        if (!marker.isVisible)
            return;

        int cps = marker.lastRadiationValue >= 0f
            ? Mathf.RoundToInt(marker.lastRadiationValue)
            : int.MinValue;

        if (cps != shownCps || marker.detectorId != shownDetectorId)
        {
            shownCps = cps;
            shownDetectorId = marker.detectorId;
            labelText.text = cps != int.MinValue
                ? $"{marker.detectorId}  {cps} cps"
                : marker.detectorId;
        }

        Transform cameraTransform = head.transform;
        Transform textTransform = labelText.transform;
        textTransform.position = transform.position + Vector3.up * config.VerticalOffsetMeters;
        textTransform.rotation = cameraTransform.rotation;
        textTransform.localScale = Vector3.one *
            GetWorldScale(Vector3.Distance(cameraTransform.position, textTransform.position));
    }

    // Kept outside the marker root, where the manager's status color write and its
    // center-hiding rule would both reach this renderer.
    private void EnsureText()
    {
        if (labelText != null)
            return;

        GameObject textObject = new GameObject($"DetectorLabel_{name}", typeof(TextMeshPro));
        labelText = textObject.GetComponent<TMP_Text>();
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = config.TextColor;
        labelText.outlineColor = config.OutlineColor;
        labelText.outlineWidth = config.OutlineWidth;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;
    }

    private float GetWorldScale(float textDistance)
    {
        if (sampleLineHeight <= 0f)
            sampleLineHeight = labelText.GetPreferredValues("MSV-000  0 cps").y;

        if (sampleLineHeight <= 0f)
            return 0f;

        float sizingDistance = config.GetSizingDistanceMeters(textDistance);
        return config.TextSizePixels * WorldHeightPerPixel(head, sizingDistance) / sampleLineHeight;
    }

    // fieldOfView is wrong on XREAL's asymmetric frustum; m11 is 2 * near / (top - bottom).
    private static float WorldHeightPerPixel(Camera camera, float distance)
    {
        float verticalScale = camera.projectionMatrix.m11;

        if (Mathf.Abs(verticalScale) < 0.0001f)
            return 0f;

        return 2f * distance / (verticalScale * Mathf.Max(1f, camera.pixelHeight));
    }
}
