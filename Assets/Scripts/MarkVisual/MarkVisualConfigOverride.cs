using UnityEngine;

/// <summary>
/// Makes this scene use its own MarkVisualConfig instead of the one in Resources.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class MarkVisualConfigOverride : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Config this scene uses. Empty falls back to the Resources asset every other scene loads.")]
    [SerializeField] private MarkVisualConfig config;

    public MarkVisualConfig Config => config;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogWarning($"[MarkVisualConfigOverride] {name} has no config; the Resources asset stays in use.");
            return;
        }

        MarkVisualConfig.SetSceneOverride(config);
    }

    private void OnDestroy()
    {
        MarkVisualConfig.ClearSceneOverride(config);
    }
}
