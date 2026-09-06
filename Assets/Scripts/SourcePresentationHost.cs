using UnityEngine;

/// <summary>
/// Spawns the off-screen and proximity presentations named by SourcePresentationConfig.
/// </summary>
[DisallowMultipleComponent]
public class SourcePresentationHost : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Transform the presentations point at. Empty means this object.")]
    [SerializeField] private Transform source;

    [Tooltip("Head camera the presentations project against. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    public SourcePresentation Offscreen => spawnedOffscreen;
    public SourcePresentation Proximity => spawnedProximity;

    private SourcePresentation spawnedOffscreen;
    private SourcePresentation spawnedProximity;

    private void Start()
    {
        if (source == null)
            source = transform;

        if (head == null)
            head = Camera.main;

        if (head == null)
        {
            Debug.LogWarning($"[SourcePresentationHost] {name} found no head camera.");
            return;
        }

        if (!SourcePresentationConfig.TryLoad(out SourcePresentationConfig config))
        {
            Debug.LogWarning(
                $"[SourcePresentationHost] {name} found no SourcePresentationConfig under a Resources folder.");
            return;
        }

        spawnedOffscreen = Spawn(config.OffscreenPrefab, "Offscreen");
        spawnedProximity = Spawn(config.ProximityPrefab, "Proximity");
    }

    private SourcePresentation Spawn(SourcePresentation prefab, string role)
    {
        if (prefab == null)
            return null;

        SourcePresentation instance = Instantiate(prefab, transform);
        instance.name = role + "_" + prefab.name;
        instance.Bind(source, head);
        return instance;
    }
}
