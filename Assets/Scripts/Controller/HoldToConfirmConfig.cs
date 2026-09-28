using UnityEngine;

/// <summary>
/// Timing, colors and haptic of one hold-to-confirm button. Read every frame, so edits during Play show at once.
/// </summary>
[CreateAssetMenu(fileName = "HoldToConfirmConfig", menuName = "RadVis/Hold To Confirm Config")]
public class HoldToConfirmConfig : ScriptableObject
{
    [Header("Timing")]
    [Tooltip("Seconds the button must be held before the action runs.")]
    [SerializeField, Min(0.1f)] private float holdSeconds = 1.5f;

    [Tooltip("Shape of the fill. X is held time as a share of Hold Seconds, Y is the filled share of the button width. Straight line fills evenly.")]
    [SerializeField] private AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Tooltip("Seconds a full fill takes to drain after an early release. 0 empties instantly.")]
    [SerializeField, Min(0f)] private float rewindSeconds = 0.2f;

    [Tooltip("Seconds the full fill takes to drain right after the action runs, even while the finger stays down. 0 empties instantly.")]
    [SerializeField, Min(0f)] private float confirmedRewindSeconds = 0.3f;

    [Header("Color")]
    [Tooltip("Button color before the fill reaches it. Written to the button's own Image.")]
    [SerializeField] private Color backgroundColor = Color.white;

    [Tooltip("Color that sweeps in from the left while the button is held.")]
    [SerializeField] private Color fillColor = new Color(1f, 0.35f, 0.35f, 1f);

    [Header("Haptic")]
    [Tooltip("Strength of the vibration sent to the Beam Pro when the fill completes. 0 sends none.")]
    [SerializeField, Range(0f, 1f)] private float hapticAmplitude = 0.25f;

    [Tooltip("Length of that vibration in seconds.")]
    [SerializeField, Min(0f)] private float hapticSeconds = 0.15f;

    public float HoldSeconds => Mathf.Max(0.1f, holdSeconds);
    public float RewindSeconds => Mathf.Max(0f, rewindSeconds);
    public float ConfirmedRewindSeconds => Mathf.Max(0f, confirmedRewindSeconds);
    public Color BackgroundColor => backgroundColor;
    public Color FillColor => fillColor;
    public float HapticAmplitude => Mathf.Clamp01(hapticAmplitude);
    public float HapticSeconds => Mathf.Max(0f, hapticSeconds);

    public float EvaluateFill(float progress)
    {
        float clamped = Mathf.Clamp01(progress);

        if (fillCurve == null || fillCurve.length == 0)
            return clamped;

        return Mathf.Clamp01(fillCurve.Evaluate(clamped));
    }
}
