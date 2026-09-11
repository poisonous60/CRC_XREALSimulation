using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fills the settings screen with one slider per tunable float and one checkbox per tunable bool
/// the active look exposes, grouped under a header row per config asset or scene component.
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

    [Header("Layout")]
    [Tooltip("Settings Screen numbers applied every time the screen opens. Empty leaves the prefab's own rects untouched.")]
    [SerializeField] private ControllerScreenConfig layoutConfig;

    [Tooltip("Title band moved to the config's title fractions. Empty leaves it where the prefab has it.")]
    [SerializeField] private RectTransform titleRect;

    [Tooltip("Back button moved to the config's button height. Empty leaves it where the prefab has it.")]
    [SerializeField] private RectTransform backButtonRect;

    [Tooltip("Reset button moved to the same height as Back. Empty leaves it where the prefab has it.")]
    [SerializeField] private RectTransform resetButtonRect;

    [Tooltip("Scroll view around the rows. Its rect takes the config's rows fractions and the list starts at the top on every open. Empty moves the row container itself.")]
    [SerializeField] private ScrollRect rowScroll;

    private readonly List<GameObject> spawnedGroups = new List<GameObject>();
    private readonly List<RuntimeTunableField> tunables = new List<RuntimeTunableField>();

    private Transform RowParent
    {
        get
        {
            if (rowContainer != null)
                return rowContainer;

            return rowTemplate != null ? rowTemplate.transform.parent : null;
        }
    }

    public void Rebuild()
    {
        ClearRows();
        ApplyLayout();

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

        Transform parent = RowParent;
        List<string> categories = new List<string>();

        for (int index = 0; index < tunables.Count; index++)
        {
            if (!categories.Contains(tunables[index].Category))
                categories.Add(tunables[index].Category);
        }

        for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            string category = categories[categoryIndex];
            RectTransform group = SpawnGroup(category, parent);
            SpawnHeader(category, group);

            for (int index = 0; index < tunables.Count; index++)
            {
                if (tunables[index].Category == category)
                    SpawnRow(tunables[index], group);
            }

            FitGroupHeight(group);
        }

        if (rowScroll != null && rowScroll.content != null)
            rowScroll.content.anchoredPosition = Vector2.zero;
    }

    public void Flush()
    {
        RuntimeTunable.Flush();
    }

    public void ResetToDefaults()
    {
        RuntimeTunable.ResetToDefaults(tunables);
        Rebuild();
    }

    private void ApplyLayout()
    {
        if (layoutConfig == null)
            return;

        SetVerticalAnchors(titleRect, layoutConfig.TitleBottomFraction, layoutConfig.TitleTopFraction);

        Transform parent = RowParent;
        RectTransform rowsArea = rowScroll != null ? rowScroll.transform as RectTransform : parent as RectTransform;
        SetVerticalAnchors(rowsArea, layoutConfig.RowsBottomFraction, layoutConfig.RowsTopFraction);

        VerticalLayoutGroup layout = parent != null ? parent.GetComponent<VerticalLayoutGroup>() : null;

        if (layout != null)
        {
            // LayoutGroup.padding compares by reference, so editing the RectOffset in place would not mark it dirty.
            RectOffset padding = layout.padding;
            layout.padding = new RectOffset(padding.left, padding.right, layoutConfig.RowPaddingTop, layoutConfig.RowPaddingBottom);
            layout.spacing = layoutConfig.CategorySpacing;
        }

        PlaceOnButtonLine(backButtonRect);
        PlaceOnButtonLine(resetButtonRect);
    }

    private void PlaceOnButtonLine(RectTransform rect)
    {
        if (rect == null)
            return;

        SetVerticalAnchors(rect, layoutConfig.BackButtonFraction, layoutConfig.BackButtonFraction);
        Vector2 position = rect.anchoredPosition;
        position.y = 0f;
        rect.anchoredPosition = position;
    }

    private static void SetVerticalAnchors(RectTransform rect, float bottom, float top)
    {
        if (rect == null)
            return;

        Vector2 min = rect.anchorMin;
        Vector2 max = rect.anchorMax;
        min.y = Mathf.Min(bottom, top);
        max.y = Mathf.Max(bottom, top);
        rect.anchorMin = min;
        rect.anchorMax = max;
    }

    private RectTransform SpawnGroup(string category, Transform parent)
    {
        GameObject group = new GameObject("Group_" + category, typeof(RectTransform));
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        spawnedGroups.Add(group);

        VerticalLayoutGroup parentLayout = parent.GetComponent<VerticalLayoutGroup>();
        VerticalLayoutGroup layout = group.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        if (layoutConfig != null)
            layout.spacing = layoutConfig.RowSpacing;
        else if (parentLayout != null)
            layout.spacing = parentLayout.spacing;

        return rect;
    }

    // The outer layout group reads each child's sizeDelta, so a nested ContentSizeFitter would land one rebuild late.
    private void FitGroupHeight(RectTransform group)
    {
        float rowHeight = ((RectTransform)rowTemplate.transform).sizeDelta.y;
        float spacing = group.GetComponent<VerticalLayoutGroup>().spacing;
        int rows = group.childCount;
        group.sizeDelta = new Vector2(0f, rows * rowHeight + Mathf.Max(0, rows - 1) * spacing);
    }

    private void SpawnHeader(string category, Transform parent)
    {
        GameObject row = Instantiate(rowTemplate, parent);
        row.name = "Header_" + category;
        row.SetActive(true);

        TMP_Text labelText = FindText(row, labelTextName);
        TMP_Text valueText = FindText(row, valueTextName);
        Slider slider = row.GetComponentInChildren<Slider>(true);

        if (labelText != null)
        {
            labelText.text = category;
            labelText.fontStyle |= FontStyles.Bold;
            labelText.textWrappingMode = TextWrappingModes.NoWrap;
            labelText.overflowMode = TextOverflowModes.Ellipsis;

            Vector2 anchorMax = labelText.rectTransform.anchorMax;
            anchorMax.x = 1f;
            labelText.rectTransform.anchorMax = anchorMax;
        }

        if (valueText != null)
            valueText.gameObject.SetActive(false);

        if (slider != null)
            slider.gameObject.SetActive(false);
    }

    private void SpawnRow(RuntimeTunableField tunable, Transform parent)
    {
        GameObject row = Instantiate(rowTemplate, parent);
        row.name = "Row_" + tunable.Label;
        row.SetActive(true);

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
            valueText.text = FormatValue(tunable);

        if (tunable.IsToggle)
        {
            SpawnToggle(row, tunable, slider, valueText);
            return;
        }

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
                boundValueText.text = FormatValue(boundTunable);
        });
    }

    private void SpawnToggle(GameObject row, RuntimeTunableField tunable, Slider slider, TMP_Text valueText)
    {
        Toggle toggle = row.GetComponentInChildren<Toggle>(true);

        if (slider != null)
            slider.gameObject.SetActive(false);

        if (toggle == null)
        {
            Debug.LogWarning($"[SettingsScreenBuilder] Row template has no Toggle, so {tunable.Label} cannot be changed.");
            return;
        }

        toggle.gameObject.SetActive(true);
        toggle.SetIsOnWithoutNotify(tunable.Value >= 0.5f);

        RuntimeTunableField boundTunable = tunable;
        TMP_Text boundValueText = valueText;

        toggle.onValueChanged.AddListener(isOn =>
        {
            boundTunable.SetValue(isOn ? 1f : 0f);
            RuntimeTunable.Save(boundTunable);

            if (boundValueText != null)
                boundValueText.text = FormatValue(boundTunable);
        });
    }

    private static string FormatValue(RuntimeTunableField tunable)
    {
        if (tunable.IsToggle)
            return tunable.Value >= 0.5f ? "On" : "Off";

        return tunable.Value.ToString(ValueFormat);
    }

    private void ClearRows()
    {
        for (int index = 0; index < spawnedGroups.Count; index++)
        {
            if (spawnedGroups[index] != null)
                Destroy(spawnedGroups[index]);
        }

        spawnedGroups.Clear();
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
