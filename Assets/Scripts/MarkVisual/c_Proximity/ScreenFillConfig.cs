using UnityEngine;

/// <summary>
/// Trigger radius and opacity of one screen fill look, kept beside its prefab instead of in the project-wide config.
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

    public float TriggerRadiusMeters => Mathf.Max(0.05f, triggerRadiusMeters);
    public float FillAlpha => Mathf.Clamp01(fillAlpha);
    public float FadeSeconds => Mathf.Max(0f, fadeSeconds);
}
