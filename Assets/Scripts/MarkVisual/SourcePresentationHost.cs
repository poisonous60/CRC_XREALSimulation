using UnityEngine;

/// <summary>
/// Spawns the proximity presentation named by MarkVisualSetting.
/// </summary>
[DisallowMultipleComponent]
public class SourcePresentationHost : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Transform the presentations point at. Empty means this object.")]
    [SerializeField] private Transform source;

    [Tooltip("Head camera the presentations project against. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    public SourcePresentation Proximity => spawnedProximity;

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

        if (!MarkVisualSetting.TryLoad(out MarkVisualSetting config))
        {
            Debug.LogWarning(
                $"[SourcePresentationHost] {name} found no MarkVisualSetting under a Resources folder.");
            return;
        }

        spawnedProximity = Spawn(config.ProximityPrefab, "Proximity");
    }

    private SourcePresentation Spawn(SourcePresentation prefab, string role)
    {
        if (prefab == null)
            return null;

        SourcePresentation instance = Instantiate(prefab, transform);
        instance.name = role + "_" + prefab.name;
        instance.Bind(source, head);

        SourceMarker marker = GetComponent<SourceMarker>();
        if (marker != null)
            marker.RegisterBodyRenderers(instance.gameObject);

        return instance;
    }
}
