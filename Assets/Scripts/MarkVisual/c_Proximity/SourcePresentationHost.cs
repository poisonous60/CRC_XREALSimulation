using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns every proximity presentation named by MarkVisualConfig.
/// </summary>
[DisallowMultipleComponent]
public class SourcePresentationHost : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Transform the presentations point at. Empty means this object.")]
    [SerializeField] private Transform source;

    [Tooltip("Head camera the presentations project against. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    public IReadOnlyList<SourcePresentation> Proximity => spawnedProximity;

    private readonly List<SourcePresentation> spawnedProximity = new List<SourcePresentation>();

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

        if (!MarkVisualConfig.TryLoad(out MarkVisualConfig config))
        {
            Debug.LogWarning(
                $"[SourcePresentationHost] {name} found no MarkVisualConfig under a Resources folder.");
            return;
        }

        IReadOnlyList<SourcePresentation> prefabs = config.ProximityPrefabs;
        if (prefabs == null)
            return;

        for (int index = 0; index < prefabs.Count; index++)
        {
            SourcePresentation spawned = Spawn(prefabs[index], $"Proximity{index}");
            if (spawned != null)
                spawnedProximity.Add(spawned);
        }
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
