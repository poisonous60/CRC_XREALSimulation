using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hands this scene its MarkVisualConfig. A scene without one draws the built-in sphere.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class MarkVisualConfigOverride : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Config this scene uses. Empty leaves the scene with no config at all, so the marker falls back to the built-in sphere.")]
    [SerializeField] private MarkVisualConfig config;

    public MarkVisualConfig Config => config;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogWarning($"[MarkVisualConfigOverride] {name} has no config; this scene draws the built-in sphere.");
            return;
        }

        MarkVisualConfig.SetSceneOverride(config);

        // Saved slider values have to land before the marker manager copies Marker Size Meters
        // in its own OnEnable, so this runs here rather than when the settings screen opens.
        RuntimeTunable.Restore(config);

        IReadOnlyList<GameObject> looks = config.Looks;

        for (int index = 0; index < looks.Count; index++)
        {
            GameObject look = looks[index];

            if (look != null && look.TryGetComponent(out LookRole role) && role.Role == LookRoleKind.Standalone)
                Instantiate(look);
        }
    }

    private void OnDestroy()
    {
        MarkVisualConfig.ClearSceneOverride(config);
    }
}
