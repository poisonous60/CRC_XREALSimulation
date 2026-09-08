using UnityEngine;

/// <summary>
/// Hides the Beam Pro placement buttons in this scene.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class ControllerButtonOverride : MonoBehaviour
{
    private static ControllerButtonOverride active;

    [Header("Beam Pro Panel")]
    [Tooltip("ON = Add Source, Place Source and Cancel Place are hidden and the placement guide text is suppressed.")]
    [SerializeField] private bool hidePlacementButtons = true;

    public static bool HidePlacementButtons =>
        active != null && active.hidePlacementButtons;

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
