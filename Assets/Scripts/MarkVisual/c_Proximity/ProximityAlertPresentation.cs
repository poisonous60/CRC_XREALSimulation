using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (c) proximity information as a warning: a head-locked screen border that grows as the source nears.
/// </summary>
[DisallowMultipleComponent]
public class ProximityAlertPresentation : SourcePresentation
{
    [Header("Range")]
    [Tooltip("Distance where the border first appears.")]
    [SerializeField, Min(0.1f)] private float alertStartMeters = 6f;

    [Tooltip("Distance at and below which the border is at full strength.")]
    [SerializeField, Min(0.05f)] private float fullAlertMeters = 1.5f;

    [Header("Look")]
    [Tooltip("Border color. The reading's own color stays the marker's job.")]
    [SerializeField] private Color alertColor = new Color(1f, 0.25f, 0.15f, 1f);

    [Tooltip("Border opacity at full strength.")]
    [SerializeField, Range(0f, 1f)] private float maximumAlpha = 0.35f;

    [Tooltip("Border thickness as a fraction of the shorter screen side.")]
    [SerializeField, Range(0.02f, 0.5f)] private float thicknessFraction = 0.18f;

    [Header("Pulse")]
    [Tooltip("Seconds per pulse at full strength. 0 keeps the border steady.")]
    [SerializeField, Min(0f)] private float pulseSeconds = 1.25f;

    [Tooltip("How much of the opacity the pulse swings.")]
    [SerializeField, Range(0f, 1f)] private float pulseAmplitude = 0.35f;

    [Header("Placement")]
    [Tooltip("Distance from the head at which the border canvas is parked.")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 1.5f;

    private Transform source;
    private Camera head;
    private SourceMarker owner;
    private Canvas alertCanvas;
    private ScreenEdgeGlowGraphic glow;
    private RectTransform canvasRect;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;
        owner = GetComponentInParent<SourceMarker>();
        EnsureCanvas();
    }

    private void EnsureCanvas()
    {
        if (alertCanvas != null || head == null)
            return;

        GameObject canvasObject = new GameObject("ProximityAlertCanvas", typeof(Canvas));
        alertCanvas = canvasObject.GetComponent<Canvas>();
        alertCanvas.renderMode = RenderMode.WorldSpace;
        alertCanvas.worldCamera = head;

        canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.localScale = Vector3.one;

        GameObject glowObject = new GameObject("EdgeGlow", typeof(RectTransform));
        glowObject.transform.SetParent(canvasObject.transform, false);

        RectTransform glowRect = (RectTransform)glowObject.transform;
        glowRect.anchorMin = Vector2.zero;
        glowRect.anchorMax = Vector2.one;
        glowRect.offsetMin = Vector2.zero;
        glowRect.offsetMax = Vector2.zero;

        glow = glowObject.AddComponent<ScreenEdgeGlowGraphic>();
        glow.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (source == null || head == null || glow == null || canvasRect == null)
            return;

        float distance = Vector3.Distance(head.transform.position, source.position);
        float strength = Mathf.SmoothStep(
            0f, 1f, Mathf.InverseLerp(alertStartMeters, fullAlertMeters, distance));

        bool markerVisible = owner == null || owner.isVisible;
        bool showing = markerVisible && strength > 0.001f;

        if (glow.enabled != showing)
            glow.enabled = showing;

        if (!showing)
            return;

        FollowHead();
        glow.SetThicknessFraction(thicknessFraction);

        float alpha = maximumAlpha * strength * PulseFactor(strength);
        Color frameColor = alertColor;
        frameColor.a = Mathf.Clamp01(alpha);
        glow.color = frameColor;
    }

    private float PulseFactor(float strength)
    {
        if (pulseSeconds <= 0f || pulseAmplitude <= 0f)
            return 1f;

        float phase = Mathf.Sin(Time.time * Mathf.PI * 2f / pulseSeconds);
        return 1f + pulseAmplitude * strength * phase;
    }

    // XREAL's projection is asymmetric, so the visible rect is sampled rather than derived
    // from fieldOfView. ARDetectorHud.Canvas.cs sizes its own canvas the same way.
    private void FollowHead()
    {
        float distance = Mathf.Max(0.2f, canvasDistanceMeters);
        Vector3 bottomLeft = head.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
        Vector3 topLeft = head.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
        Vector3 bottomRight = head.ViewportToWorldPoint(new Vector3(1f, 0f, distance));
        Vector3 center = head.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

        canvasRect.position = center;
        canvasRect.rotation = head.transform.rotation;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = new Vector2(
            Vector3.Distance(bottomLeft, bottomRight),
            Vector3.Distance(bottomLeft, topLeft));
    }

    private void OnDestroy()
    {
        if (alertCanvas != null)
            Destroy(alertCanvas.gameObject);
    }
}
