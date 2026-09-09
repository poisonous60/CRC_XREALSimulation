using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills the settings screen with one slider per tunable field the active look exposes.
/// </summary>
[DisallowMultipleComponent]
public class SettingsScreenBuilder : MonoBehaviour
{
    private const string ValueFormat = "0.##";
    private const float MinimumLabelFontSize = 16f;

    [Header("Row Template")]
    [Tooltip("Inactive row cloned once per tunable field. It needs a Slider and the two texts named below.")]
    [SerializeField] private GameObject rowTemplate;

    [Tooltip("Parent the cloned rows are added under. Empty means the template's own parent.")]
    [SerializeField] private Transform rowContainer;

    [Header("Row Wiring")]
    [Tooltip("Name of the text inside the row that shows the field label.")]
    [SerializeField] private string labelTextName = "Label";

    [Tooltip("Name of the text inside the row that shows the current number.")]
    [SerializeField] private string valueTextName = "Value";

    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private readonly List<RuntimeTunableField> tunables = new List<RuntimeTunableField>();

    public void Rebuild()
    {
        ClearRows();

        if (rowTemplate == null)
        {
            Debug.LogWarning("[SettingsScreenBuilder] No row template assigned. The settings screen stays empty.");
            return;
        }

        if (!MarkVisualConfig.TryLoad(out MarkVisualConfig config))
        {
            Debug.LogWarning("[SettingsScreenBuilder] No MarkVisualConfig in this scene. The settings screen stays empty.");
            return;
        }

        RuntimeTunable.Collect(config, tunables);

        Transform parent = rowContainer != null ? rowContainer : rowTemplate.transform.parent;

        for (int index = 0; index < tunables.Count; index++)
            SpawnRow(tunables[index], parent);
    }

    public void Flush()
    {
        RuntimeTunable.Flush();
    }

    private void SpawnRow(RuntimeTunableField tunable, Transform parent)
    {
        GameObject row = Instantiate(rowTemplate, parent);
        row.name = "Row_" + tunable.Label;
        row.SetActive(true);
        spawnedRows.Add(row);

        TMP_Text labelText = FindText(row, labelTextName);
        TMP_Text valueText = FindText(row, valueTextName);
        Slider slider = row.GetComponentInChildren<Slider>(true);

        if (labelText != null)
        {
            // Labels come from whichever look is wired, so the row cannot be sized for them.
            float labelMaxFontSize = labelText.fontSize;
            labelText.text = tunable.Label;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = MinimumLabelFontSize;
            labelText.fontSizeMax = labelMaxFontSize;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (valueText != null)
            valueText.text = tunable.Value.ToString(ValueFormat);

        if (slider == null)
        {
            Debug.LogWarning($"[SettingsScreenBuilder] Row template has no Slider, so {tunable.Label} cannot be changed.");
            return;
        }

        slider.wholeNumbers = false;
        slider.minValue = tunable.Minimum;
        slider.maxValue = tunable.Maximum;
        slider.SetValueWithoutNotify(Mathf.Clamp(tunable.Value, tunable.Minimum, tunable.Maximum));

        RuntimeTunableField boundTunable = tunable;
        TMP_Text boundValueText = valueText;

        slider.onValueChanged.AddListener(value =>
        {
            boundTunable.SetValue(value);
            RuntimeTunable.Save(boundTunable);

            if (boundValueText != null)
                boundValueText.text = boundTunable.Value.ToString(ValueFormat);
        });
    }

    private void ClearRows()
    {
        for (int index = 0; index < spawnedRows.Count; index++)
        {
            if (spawnedRows[index] != null)
                Destroy(spawnedRows[index]);
        }

        spawnedRows.Clear();
        tunables.Clear();
    }

    private static TMP_Text FindText(GameObject row, string childName)
    {
        TMP_Text[] texts = row.GetComponentsInChildren<TMP_Text>(true);

        for (int index = 0; index < texts.Length; index++)
        {
            if (texts[index] != null && texts[index].name == childName)
                return texts[index];
        }

        return null;
    }
}
