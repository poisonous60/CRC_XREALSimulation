using UnityEngine;

/// <summary>
/// (c) proximity information drawn on the source itself: inside the trigger radius a radiation trefoil fades in over the marker.
/// </summary>
[DisallowMultipleComponent]
public class RadiationSignPresentation : SourcePresentation
{
    [Header("Look")]
    [Tooltip("Trigger radius, fade and color. Point this at c_04_screen_fill_config so the sign and the screen fill switch at one distance.")]
    [SerializeField] private ScreenFillConfig config;

    [Tooltip("Child carrying the sign quad. Empty means the first child.")]
    [SerializeField] private Transform visual;

    [Header("Size")]
    [Tooltip("Width of the sign in meters. Held steady against Marker Size Meters, so the sign keeps this width whatever the marker scale is.")]
    [SerializeField, Min(0.01f)] private float signSizeMeters = 0.25f;

    private Transform source;
    private Camera head;
    private SourceMarker owner;
    private Renderer signRenderer;
    private float strength;

    public ScreenFillConfig Config => config;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;
        owner = GetComponentInParent<SourceMarker>();
        signRenderer = GetComponentInChildren<Renderer>(true);

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        if (config == null)
            Debug.LogWarning($"[RadiationSignPresentation] {name} has no config; nothing is drawn.");
    }

    private void LateUpdate()
    {
        if (config == null || source == null || head == null || signRenderer == null || visual == null)
            return;

        float distance = Vector3.Distance(head.transform.position, source.position);
        bool markerVisible = owner == null || owner.isVisible;
        float target = markerVisible && distance <= config.TriggerRadiusMeters ? 1f : 0f;

        strength = config.FadeSeconds > 0f
            ? Mathf.MoveTowards(strength, target, Time.deltaTime / config.FadeSeconds)
            : target;

        ApplySize();

        // MarkerVisibilityPolicy owns Renderer.enabled on everything under the marker root,
        // so the sign hides by going transparent instead of fighting it for that flag.
        Color signColor = GetSignColor();
        signColor.a = config.EvaluateFade(strength);
        signRenderer.material.color = signColor;
    }

    private void ApplySize()
    {
        float rootScale = Mathf.Max(0.0001f, transform.lossyScale.x);
        visual.localScale = Vector3.one * (signSizeMeters / rootScale);
    }

    private Color GetSignColor()
    {
        if (config.FollowStatusBand &&
            owner != null &&
            MarkVisualConfig.TryLoad(out MarkVisualConfig visualConfig) &&
            visualConfig.TryGetStatusColor(owner.lastRadiationValue, out Color statusColor))
        {
            return statusColor;
        }

        return config.SolidColor;
    }
}
