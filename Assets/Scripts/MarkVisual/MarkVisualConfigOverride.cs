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
    }

    private void OnDestroy()
    {
        MarkVisualConfig.ClearSceneOverride(config);
    }
}
