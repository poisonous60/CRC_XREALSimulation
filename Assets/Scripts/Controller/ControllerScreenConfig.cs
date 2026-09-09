using UnityEngine;

/// <summary>
/// Numbers the Beam Pro lock screen needs. Its text and colors live on the prefab.
/// </summary>
[CreateAssetMenu(fileName = "ControllerScreenConfig", menuName = "RadVis/Controller Screen Config")]
public class ControllerScreenConfig : ScriptableObject
{
    [Header("Lock Screen")]
    [Tooltip("Touches needed inside Unlock Window Seconds to leave the lock screen.")]
    [SerializeField, Min(2)] private int unlockTapCount = 5;

    [Tooltip("Window the touches must fall inside. The newest and the oldest of the last Unlock Tap Count touches are compared.")]
    [SerializeField, Min(0.1f)] private float unlockWindowSeconds = 1f;

    [Tooltip("Touches landing this close together count as one. Grabbing the phone puts several fingers down at once, while deliberate tapping is far slower.")]
    [SerializeField, Min(0f)] private float tapGroupingSeconds = 0.05f;

    public int UnlockTapCount => Mathf.Max(2, unlockTapCount);
    public float UnlockWindowSeconds => Mathf.Max(0.1f, unlockWindowSeconds);
    public float TapGroupingSeconds => Mathf.Max(0f, tapGroupingSeconds);
}
