using TMPro;
using UnityEngine;

/// <summary>
/// (c) proximity information, distance only: one line of head-to-source distance under the marker.
/// </summary>
[DisallowMultipleComponent]
public class DistanceProximityPresentation : SourcePresentation
{
    [Header("Placement")]
    [Tooltip("Camera-relative world offset from the source center. Negative Y sits below the marker.")]
    [SerializeField] private Vector3 cameraOffsetMeters = new Vector3(0f, -0.16f, -0.01f);

    [Tooltip("World height of the text, kept independent of the marker's own scale.")]
    [SerializeField, Min(0.001f)] private float worldScale = 0.04f;

    [Header("Text")]
    [Tooltip("Font size before the world scale above is applied.")]
    [SerializeField, Min(0.1f)] private float fontSize = 4.5f;

    [Tooltip("Color of the distance line. The source color is not copied so the reading stays the marker's job.")]
    [SerializeField] private Color textColor = Color.white;

    [Tooltip("Outline thickness that keeps the text readable against a bright room.")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;

    [Tooltip("Outline color.")]
    [SerializeField] private Color outlineColor = Color.black;

    [Header("Range")]
    [Tooltip("Distance above which the line is hidden. 0 keeps it visible at every distance.")]
    [SerializeField, Min(0f)] private float maximumVisibleDistanceMeters = 0f;

    private Transform source;
    private Camera head;
    private TMP_Text distanceText;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;
        EnsureText();
    }

    private void EnsureText()
    {
        if (distanceText != null)
            return;

        GameObject textObject = new GameObject("DistanceText", typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);

        distanceText = textObject.GetComponent<TMP_Text>();
        distanceText.fontSize = fontSize;
        distanceText.alignment = TextAlignmentOptions.Center;
        distanceText.color = textColor;
        distanceText.outlineColor = outlineColor;
        distanceText.outlineWidth = outlineWidth;
        distanceText.textWrappingMode = TextWrappingModes.NoWrap;
        distanceText.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (source == null || head == null || distanceText == null)
            return;

        Transform cameraTransform = head.transform;
        float distance = Vector3.Distance(cameraTransform.position, source.position);

        bool withinRange = maximumVisibleDistanceMeters <= 0f ||
                           distance <= maximumVisibleDistanceMeters;

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

        Vector3 parentScale = transform.lossyScale;
        textTransform.localScale = new Vector3(
            SafeScaleDivision(worldScale, parentScale.x),
            SafeScaleDivision(worldScale, parentScale.y),
            SafeScaleDivision(worldScale, parentScale.z));
    }

    private static float SafeScaleDivision(float target, float parentScale)
    {
        return Mathf.Abs(parentScale) < 0.0001f ? target : target / parentScale;
    }
}
