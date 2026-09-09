using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Counts touches on the lock overlay and returns to the main screen on the unlock burst.
/// </summary>
[DisallowMultipleComponent]
public class LockScreenTapUnlock : MonoBehaviour, IPointerDownHandler
{
    private const int FallbackUnlockTapCount = 5;
    private const float FallbackUnlockWindowSeconds = 1f;
    private const float FallbackTapGroupingSeconds = 0.05f;

    [Header("References")]
    [Tooltip("Screen switcher told to leave the lock screen. Auto-found on the controller prefab root when empty.")]
    [SerializeField] private ControllerScreenSwitcher screenSwitcher;

    [Tooltip("Unlock gesture numbers. Empty falls back to five touches inside one second.")]
    [SerializeField] private ControllerScreenConfig config;

    private readonly List<float> tapTimes = new List<float>();
    private float lastCountedTapTime = float.NegativeInfinity;

    private void OnEnable()
    {
        tapTimes.Clear();
        lastCountedTapTime = float.NegativeInfinity;
    }

    // This handler is what keeps a tap from bubbling to ControllerPanelCancelArea, which would
    // otherwise read the unlock gesture as a background tap and cancel a pending placement.
    public void OnPointerDown(PointerEventData eventData)
    {
        float now = Time.unscaledTime;

        if (now - lastCountedTapTime < GroupingSeconds)
            return;

        lastCountedTapTime = now;
        tapTimes.Add(now);

        int required = RequiredTapCount;

        while (tapTimes.Count > required)
            tapTimes.RemoveAt(0);

        if (tapTimes.Count < required || now - tapTimes[0] > WindowSeconds)
            return;

        tapTimes.Clear();

        if (!ResolveSwitcher())
            return;

        screenSwitcher.ShowMain();
    }

    private int RequiredTapCount => config != null ? config.UnlockTapCount : FallbackUnlockTapCount;

    private float WindowSeconds =>
        config != null ? config.UnlockWindowSeconds : FallbackUnlockWindowSeconds;

    private float GroupingSeconds =>
        config != null ? config.TapGroupingSeconds : FallbackTapGroupingSeconds;

    private bool ResolveSwitcher()
    {
        if (screenSwitcher != null)
            return true;

        Transform searchRoot = transform.root;

        if (searchRoot != null)
            screenSwitcher = searchRoot.GetComponentInChildren<ControllerScreenSwitcher>(true);

        if (screenSwitcher == null)
            Debug.LogWarning("[LockScreenTapUnlock] ControllerScreenSwitcher not found. The lock screen cannot be left.");

        return screenSwitcher != null;
    }
}
