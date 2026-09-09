using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>Marks one config float as a slider on the Beam Pro settings screen.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class RuntimeTunableAttribute : Attribute
{
    public RuntimeTunableAttribute(float minimum, float maximum, string label)
    {
        Minimum = minimum;
        Maximum = maximum;
        Label = label;
    }

    public float Minimum { get; }
    public float Maximum { get; }
    public string Label { get; }
}

/// <summary>One tunable field of one config asset, read and written through reflection.</summary>
public sealed class RuntimeTunableField
{
    private const string SaveKeyPrefix = "RadVis.Tunable.";

    private readonly ScriptableObject owner;
    private readonly FieldInfo field;
    private readonly RuntimeTunableAttribute attribute;

    public RuntimeTunableField(
        ScriptableObject owningAsset,
        FieldInfo tunableField,
        RuntimeTunableAttribute tunableAttribute)
    {
        owner = owningAsset;
        field = tunableField;
        attribute = tunableAttribute;
    }

    public string Label => attribute.Label;
    public float Minimum => attribute.Minimum;
    public float Maximum => attribute.Maximum;
    public string SaveKey => SaveKeyPrefix + owner.name + "." + field.Name;
    public float Value => (float)field.GetValue(owner);

    public void SetValue(float value)
    {
        field.SetValue(owner, Mathf.Clamp(value, attribute.Minimum, attribute.Maximum));
    }
}

/// <summary>
/// Collects the tunable fields the active look exposes and keeps them in PlayerPrefs.
/// </summary>
public static class RuntimeTunable
{
    private const BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Collect(MarkVisualConfig config, List<RuntimeTunableField> results)
    {
        results.Clear();

        if (config == null)
            return;

        HashSet<ScriptableObject> visited = new HashSet<ScriptableObject>();
        CollectAsset(config, visited, results);
        CollectPrefab(config.MarkerPrefab, visited, results);
        CollectPrefab(
            config.OffscreenPrefab != null ? config.OffscreenPrefab.gameObject : null,
            visited,
            results);

        IReadOnlyList<SourcePresentation> proximity = config.ProximityPrefabs;
        if (proximity != null)
        {
            for (int index = 0; index < proximity.Count; index++)
            {
                SourcePresentation entry = proximity[index];
                CollectPrefab(entry != null ? entry.gameObject : null, visited, results);
            }
        }

        IReadOnlyList<UncertaintyPresentation> uncertainty = config.UncertaintyPrefabs;
        if (uncertainty == null)
            return;

        for (int index = 0; index < uncertainty.Count; index++)
        {
            UncertaintyPresentation entry = uncertainty[index];
            CollectPrefab(entry != null ? entry.gameObject : null, visited, results);
        }
    }

    public static void Save(RuntimeTunableField tunable)
    {
        if (tunable == null)
            return;

        PlayerPrefs.SetFloat(tunable.SaveKey, tunable.Value);
    }

    public static void Flush()
    {
        PlayerPrefs.Save();
    }

    // Editor Play mode keeps whatever the sliders wrote into the asset, so restoring there would
    // silently undo an Inspector edit made between two Play sessions. Builds reload the asset
    // from the package every launch, which is the only case a saved value has to survive.
    public static void Restore(MarkVisualConfig config)
    {
#if !UNITY_EDITOR
        List<RuntimeTunableField> tunables = new List<RuntimeTunableField>();
        Collect(config, tunables);

        for (int index = 0; index < tunables.Count; index++)
        {
            RuntimeTunableField tunable = tunables[index];

            if (PlayerPrefs.HasKey(tunable.SaveKey))
                tunable.SetValue(PlayerPrefs.GetFloat(tunable.SaveKey, tunable.Value));
        }
#endif
    }

    private static void CollectPrefab(
        GameObject prefab,
        HashSet<ScriptableObject> visited,
        List<RuntimeTunableField> results)
    {
        if (prefab == null)
            return;

        // HOWTO_marker_look.md section 13 keeps every look config on the prefab root, so the
        // children are not walked.
        MonoBehaviour[] components = prefab.GetComponents<MonoBehaviour>();

        for (int index = 0; index < components.Length; index++)
        {
            MonoBehaviour component = components[index];

            if (component == null)
                continue;

            FieldInfo[] fields = component.GetType().GetFields(FieldFlags);

            for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                ScriptableObject asset = fields[fieldIndex].GetValue(component) as ScriptableObject;

                if (asset != null)
                    CollectAsset(asset, visited, results);
            }
        }
    }

    private static void CollectAsset(
        ScriptableObject asset,
        HashSet<ScriptableObject> visited,
        List<RuntimeTunableField> results)
    {
        if (asset == null || !visited.Add(asset))
            return;

        FieldInfo[] fields = asset.GetType().GetFields(FieldFlags);

        for (int index = 0; index < fields.Length; index++)
        {
            FieldInfo field = fields[index];

            if (field.FieldType != typeof(float))
                continue;

            RuntimeTunableAttribute attribute =
                field.GetCustomAttribute<RuntimeTunableAttribute>();

            if (attribute != null)
                results.Add(new RuntimeTunableField(asset, field, attribute));
        }
    }
}
