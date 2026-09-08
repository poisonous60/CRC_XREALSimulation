using UnityEngine;

/// <summary>
/// Trigger radius, color and opacity of one screen fill look, kept beside its prefab instead of in the project-wide config.
/// </summary>
[CreateAssetMenu(fileName = "c_00_screen_fill_config", menuName = "RadVis/Screen Fill Config")]
public class ScreenFillConfig : ScriptableObject
{
    [Header("Range")]
    [Tooltip("Head-to-source distance at which the fill turns on. Independent of the marker's own size.")]
    [SerializeField, Min(0.05f)] private float triggerRadiusMeters = 1.5f;

    [Header("Look")]
    [Tooltip("Opacity of the fill at full strength. The glasses add this on top of the room, so keep it low.")]
    [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.25f;

    [Tooltip("Seconds the fill takes to reach full strength. 0 switches it instantly.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0f;

    [Tooltip("Shape of the fade. X is elapsed fade progress 0 to 1, Y is the share of Fill Alpha shown. Straight line fades evenly.")]
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Color")]
    [Tooltip("On: the fill takes the marker's status band color. Off: it uses Solid Color.")]
    [SerializeField] private bool followStatusBand = true;

    [Tooltip("Fill color used when Follow Status Band is off. Its alpha is ignored; Fill Alpha sets the opacity.")]
    [SerializeField] private Color solidColor = Color.red;

    public float TriggerRadiusMeters => Mathf.Max(0.05f, triggerRadiusMeters);
    public float FillAlpha => Mathf.Clamp01(fillAlpha);
    public float FadeSeconds => Mathf.Max(0f, fadeSeconds);
    public bool FollowStatusBand => followStatusBand;
    public Color SolidColor => solidColor;

    public float EvaluateFade(float progress)
    {
        float clamped = Mathf.Clamp01(progress);

        if (fadeCurve == null || fadeCurve.length == 0)
            return clamped;

        return Mathf.Clamp01(fadeCurve.Evaluate(clamped));
    }
}
