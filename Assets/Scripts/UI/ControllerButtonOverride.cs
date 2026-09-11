using UnityEngine;

/// <summary>
/// Picks which Beam Pro placement controls this scene shows: the single Source button or a list per detector.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class ControllerButtonOverride : MonoBehaviour
{
    private static ControllerButtonOverride active;

    [Header("Beam Pro Panel")]
    [Tooltip("ON = Add Source, Place Source and Cancel Place are hidden and the placement guide text is suppressed.")]
    [SerializeField] private bool hidePlacementButtons = true;

    [Tooltip("ON = one hold button per reporting Detector ID is listed instead, so each detector is placed by its own row.")]
    [SerializeField] private bool showDetectorPlaceButtons = false;

    public static bool HidePlacementButtons =>
        active != null && active.hidePlacementButtons;

    public static bool ShowDetectorPlaceButtons =>
        active != null && active.showDetectorPlaceButtons;

    private void Awake()
    {
        active = this;
    }

    private void OnDestroy()
    {
        if (active == this)
            active = null;
    }
}
