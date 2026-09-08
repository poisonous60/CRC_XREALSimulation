using TMPro;
using UnityEngine;

/// <summary>
/// (c) proximity information, distance only: one line of head-to-source distance under the marker.
/// </summary>
[DisallowMultipleComponent]
public class DistanceProximityPresentation : SourcePresentation
{
    [Header("Look")]
    [Tooltip("Text size, color and visible range for this look. Empty draws nothing.")]
    [SerializeField] private DistanceTextConfig config;

    [Header("Placement")]
    [Tooltip("Camera-relative world offset from the source center. Negative Y sits below the marker.")]
    [SerializeField] private Vector3 cameraOffsetMeters = new Vector3(0f, -0.16f, -0.01f);

    private Transform source;
    private Camera head;
    private TMP_Text distanceText;
    private float sampleLineHeight;

    public DistanceTextConfig Config => config;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;

        if (config == null)
        {
            Debug.LogWarning($"[DistanceProximityPresentation] {name} has no config; nothing is drawn.");
            return;
        }

        EnsureText();
    }

    private void EnsureText()
    {
        if (distanceText != null)
            return;

        GameObject textObject = new GameObject("DistanceText", typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);

        distanceText = textObject.GetComponent<TMP_Text>();
        distanceText.alignment = TextAlignmentOptions.Center;
        distanceText.color = config.TextColor;
        distanceText.outlineColor = config.OutlineColor;
        distanceText.outlineWidth = config.OutlineWidth;
        distanceText.textWrappingMode = TextWrappingModes.NoWrap;
        distanceText.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (config == null || source == null || head == null || distanceText == null)
            return;

        Transform cameraTransform = head.transform;
        float distance = Vector3.Distance(cameraTransform.position, source.position);

        bool withinRange = config.MaximumVisibleDistanceMeters <= 0f ||
                           distance <= config.MaximumVisibleDistanceMeters;

        if (distanceText.enabled != withinRange)
            distanceText.enabled = withinRange;

        if (!withinRange)
            return;

        distanceText.text = $"{distance:0.#} m";

        Transform textTransform = distanceText.transform;
        textTransform.position =
            source.position +
            cameraTransform.right * cameraOffsetMeters.x +
            cameraTransform.up * cameraOffsetMeters.y +
            cameraTransform.forward * cameraOffsetMeters.z;
        textTransform.rotation = cameraTransform.rotation;

        float worldScale = GetWorldScale(Vector3.Distance(cameraTransform.position, textTransform.position));
        Vector3 parentScale = transform.lossyScale;
        textTransform.localScale = new Vector3(
            SafeScaleDivision(worldScale, parentScale.x),
            SafeScaleDivision(worldScale, parentScale.y),
            SafeScaleDivision(worldScale, parentScale.z));
    }

    // Scaling to a pixel size needs the font's own line height measured once; it cancels
    // the font asset's point size out.
    private float GetWorldScale(float textDistance)
    {
        if (sampleLineHeight <= 0f)
            sampleLineHeight = distanceText.GetPreferredValues("0.0 m").y;

        if (sampleLineHeight <= 0f)
            return 0f;

        float sizingDistance = config.ConstantScreenSize ? textDistance : config.ReferenceDistanceMeters;
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

    private static float SafeScaleDivision(float target, float parentScale)
    {
        return Mathf.Abs(parentScale) < 0.0001f ? target : target / parentScale;
    }
}
