using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Beam Pro panel of the anchor test scene: four buttons and one status line, with no anchor logic.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnchorTestPanel : MonoBehaviour
{
    [Header("Buttons")]
    [Tooltip("Hold to aim a new source disc with the gaze, release to anchor it. Earlier discs stay.")]
    [SerializeField] private EventTrigger placeButton;

    [Tooltip("Saves every placed anchor not yet saved so they come back at the next start.")]
    [SerializeField] private Button saveButton;

    [Tooltip("Erases every saved anchor and removes all discs.")]
    [SerializeField] private Button clearButton;

    [Tooltip("Takes a JPEG through the scene's XREALCaptureManager.")]
    [SerializeField] private Button photoButton;

    [Header("Status")]
    [Tooltip("Anchor state on the first line, the last capture message below it.")]
    [SerializeField] private TMP_Text statusText;

    public event Action PlacePressed;
    public event Action PlaceReleased;
    public event Action SaveClicked;
    public event Action ClearClicked;
    public event Action PhotoClicked;

    private void Awake()
    {
        AddPlaceTrigger(EventTriggerType.PointerDown, () => PlacePressed?.Invoke());
        AddPlaceTrigger(EventTriggerType.PointerUp, () => PlaceReleased?.Invoke());
        saveButton.onClick.AddListener(() => SaveClicked?.Invoke());
        clearButton.onClick.AddListener(() => ClearClicked?.Invoke());
        photoButton.onClick.AddListener(() => PhotoClicked?.Invoke());
    }

    public void SetStatus(string text)
    {
        statusText.text = text;
    }

    private void AddPlaceTrigger(EventTriggerType type, Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        placeButton.triggers.Add(entry);
    }
}
