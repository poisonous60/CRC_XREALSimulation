using UnityEngine;

/// <summary>
/// Shows one of the three Beam Pro screens. Screen 1 stays active under the two overlays.
/// </summary>
[DisallowMultipleComponent]
public class ControllerScreenSwitcher : MonoBehaviour
{
    [Header("Screens")]
    [Tooltip("Lock overlay covering the whole panel.")]
    [SerializeField] private GameObject lockScreen;

    [Tooltip("Settings overlay covering the whole panel.")]
    [SerializeField] private GameObject settingsScreen;

    [Header("Settings")]
    [Tooltip("Builder filling the settings screen. Empty leaves whatever the prefab holds.")]
    [SerializeField] private SettingsScreenBuilder settingsBuilder;

    public bool IsLocked => lockScreen != null && lockScreen.activeSelf;

    private void Awake()
    {
        ShowMain();
    }

    public void ShowMain()
    {
        LeaveSettings();
        SetScreenActive(lockScreen, false);
        SetScreenActive(settingsScreen, false);
    }

    public void ShowLock()
    {
        LeaveSettings();
        SetScreenActive(settingsScreen, false);
        SetScreenActive(lockScreen, true);
    }

    public void ShowSettings()
    {
        SetScreenActive(lockScreen, false);
        SetScreenActive(settingsScreen, true);

        if (settingsBuilder != null)
            settingsBuilder.Rebuild();
    }

    // Writing every slider move straight to disk would hit PlayerPrefs on every frame of a drag,
    // so the values are only committed once the screen closes.
    private void LeaveSettings()
    {
        if (settingsScreen == null || !settingsScreen.activeSelf || settingsBuilder == null)
            return;

        settingsBuilder.Flush();
    }

    private static void SetScreenActive(GameObject screen, bool visible)
    {
        if (screen == null || screen.activeSelf == visible)
            return;

        screen.SetActive(visible);
    }
}
