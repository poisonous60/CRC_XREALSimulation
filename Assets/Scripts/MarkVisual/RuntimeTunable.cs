using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>Marks one config float as a slider, or one config bool as a checkbox, on the Beam Pro settings screen.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class RuntimeTunableAttribute : Attribute
{
    public RuntimeTunableAttribute(float minimum, float maximum, string label)
    {
        Minimum = minimum;
        Maximum = maximum;
        Label = label;
    }

    public RuntimeTunableAttribute(string label) : this(0f, 1f, label)
    {
    }

    public float Minimum { get; }
    public float Maximum { get; }
    public string Label { get; }
}

/// <summary>One tunable field of one config asset or scene component, read and written through reflection.</summary>
public sealed class RuntimeTunableField
{
    private const string SaveKeyPrefix = "RadVis.Tunable.";

    private readonly UnityEngine.Object owner;
    private readonly object target;
    private readonly FieldInfo field;
    private readonly RuntimeTunableAttribute attribute;
    private readonly string fieldPath;
    private readonly string label;

    public RuntimeTunableField(
        UnityEngine.Object owningObject,
        object fieldTarget,
        FieldInfo tunableField,
        RuntimeTunableAttribute tunableAttribute,
        string path,
        string rowLabel)
    {
        owner = owningObject;
        target = fieldTarget;
        field = tunableField;
        attribute = tunableAttribute;
        fieldPath = path;
        label = rowLabel;
    }

    public string Label => label;
    public string Category => owner is ScriptableObject ? owner.name : owner.GetType().Name;
    public float Minimum => attribute.Minimum;
    public float Maximum => attribute.Maximum;
    public string SaveKey => SaveKeyPrefix + owner.name + "." + fieldPath;
    public bool IsToggle => field.FieldType == typeof(bool);

    public float Value => IsToggle
        ? ((bool)field.GetValue(target) ? 1f : 0f)
        : (float)field.GetValue(target);

    public void SetValue(float value)
    {
        if (IsToggle)
            field.SetValue(target, value >= 0.5f);
        else
            field.SetValue(target, Mathf.Clamp(value, attribute.Minimum, attribute.Maximum));
    }
}

/// <summary>
/// Collects the tunable fields the active look exposes and keeps them in PlayerPrefs.
/// </summary>
public static class RuntimeTunable
{
    private const BindingFlags FieldFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static readonly Dictionary<string, float> defaults =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    public static void Collect(MarkVisualConfig config, List<RuntimeTunableField> results)
    {
        results.Clear();

        if (config == null)
            return;

        HashSet<ScriptableObject> visited = new HashSet<ScriptableObject>();
        CollectAsset(config, visited, results);
        CollectPrefab(config.MarkerPrefab, visited, results);
        CollectPrefab(config.OffscreenPrefab != null ? config.OffscreenPrefab.gameObject : null, visited, results);

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

        if (uncertainty != null)
        {
            for (int index = 0; index < uncertainty.Count; index++)
            {
                UncertaintyPresentation entry = uncertainty[index];
                CollectPrefab(entry != null ? entry.gameObject : null, visited, results);
            }
        }

        CollectPrefab(config.DetectorPrefab, visited, results);
        CollectSceneComponents(visited, results);
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

    // The first Collect of a session runs before Restore or any slider writes, so the value it
    // saw is the asset's own and is what Reset returns to.
    public static void ResetToDefaults(List<RuntimeTunableField> tunables)
    {
        for (int index = 0; index < tunables.Count; index++)
        {
            RuntimeTunableField tunable = tunables[index];

            if (defaults.TryGetValue(tunable.SaveKey, out float defaultValue))
                tunable.SetValue(defaultValue);

            PlayerPrefs.DeleteKey(tunable.SaveKey);
        }
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
            CollectReferencedAssets(components[index], visited, results);
    }

    private static void CollectSceneComponents(
        HashSet<ScriptableObject> visited,
        List<RuntimeTunableField> results)
    {
        MonoBehaviour[] components =
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        for (int index = 0; index < components.Length; index++)
        {
            CollectFields(components[index], results);
            CollectReferencedAssets(components[index], visited, results);
        }
    }

    private static void CollectReferencedAssets(
        MonoBehaviour component,
        HashSet<ScriptableObject> visited,
        List<RuntimeTunableField> results)
    {
        if (component == null)
            return;

        FieldInfo[] fields = component.GetType().GetFields(FieldFlags);

        for (int index = 0; index < fields.Length; index++)
        {
            ScriptableObject asset = fields[index].GetValue(component) as ScriptableObject;

            if (asset != null)
                CollectAsset(asset, visited, results);
        }
    }

    private static void CollectAsset(
        ScriptableObject asset,
        HashSet<ScriptableObject> visited,
        List<RuntimeTunableField> results)
    {
        if (asset == null || !visited.Add(asset))
            return;

        CollectFields(asset, results);
    }

    private static void CollectFields(UnityEngine.Object owner, List<RuntimeTunableField> results)
    {
        if (owner == null)
            return;

        FieldInfo[] fields = owner.GetType().GetFields(FieldFlags);

        for (int index = 0; index < fields.Length; index++)
        {
            FieldInfo field = fields[index];

            if (TryGetTunable(field, out RuntimeTunableAttribute attribute))
            {
                Add(owner, owner, field, attribute, field.Name, attribute.Label, results);
                continue;
            }

            if (!typeof(IList).IsAssignableFrom(field.FieldType))
                continue;

            IList list = field.GetValue(owner) as IList;

            if (list == null)
                continue;

            for (int element = 0; element < list.Count; element++)
            {
                object item = list[element];

                // A struct element would come back boxed, so a write to it would not reach the list.
                if (item == null || item is UnityEngine.Object || item.GetType().IsValueType)
                    continue;

                FieldInfo[] itemFields = item.GetType().GetFields(FieldFlags);

                for (int itemIndex = 0; itemIndex < itemFields.Length; itemIndex++)
                {
                    FieldInfo itemField = itemFields[itemIndex];

                    if (!TryGetTunable(itemField, out RuntimeTunableAttribute itemAttribute))
                        continue;

                    Add(
                        owner,
                        item,
                        itemField,
                        itemAttribute,
                        field.Name + "[" + element + "]." + itemField.Name,
                        string.Format(itemAttribute.Label, element + 1),
                        results);
                }
            }
        }
    }

    private static bool TryGetTunable(FieldInfo field, out RuntimeTunableAttribute attribute)
    {
        attribute = field.FieldType == typeof(float) || field.FieldType == typeof(bool)
            ? field.GetCustomAttribute<RuntimeTunableAttribute>()
            : null;

        return attribute != null;
    }

    private static void Add(
        UnityEngine.Object owner,
        object target,
        FieldInfo field,
        RuntimeTunableAttribute attribute,
        string fieldPath,
        string label,
        List<RuntimeTunableField> results)
    {
        RuntimeTunableField tunable = new RuntimeTunableField(owner, target, field, attribute, fieldPath, label);
        results.Add(tunable);

        if (!defaults.ContainsKey(tunable.SaveKey))
            defaults.Add(tunable.SaveKey, tunable.Value);
    }
}
