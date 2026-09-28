using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Runs On Confirmed only after the button is held until its fill sweeps from left to right. Attach beside the Button.
/// </summary>
[DisallowMultipleComponent]
public class HoldToConfirmButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("Timing, colors and haptic. One asset can serve several buttons.")]
    [SerializeField] private HoldToConfirmConfig config;

    [Tooltip("The button's own Image. Takes Background Color from the config.")]
    [SerializeField] private Image background;

    [Tooltip("Child Image stretched from the left edge. Its right anchor follows the fill, like a Slider's Fill.")]
    [SerializeField] private Image fill;

    [Header("Events")]
    [Tooltip("Called once when the fill completes. Wire the action here, not in the Button's On Click.")]
    [SerializeField] private UnityEvent onConfirmed = new UnityEvent();

    private Selectable selectable;
    private bool isHeld;
    private int heldPointerId;
    private bool hasConfirmed;
    private bool drainingAfterConfirm;
    private float progress;
    private float appliedShare = -1f;
    private bool warned;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // A second finger landing mid-hold must not restart a hold that already confirmed.
        if (isHeld || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (!IsInteractable())
            return;

        isHeld = true;
        heldPointerId = eventData.pointerId;
        hasConfirmed = false;
        drainingAfterConfirm = false;
        // Starting from empty keeps a release during the rewind from shortening the next hold.
        progress = 0f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            ReleaseIfHolding(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ReleaseIfHolding(eventData);
    }

    private void OnDisable()
    {
        Release();
        drainingAfterConfirm = false;
        progress = 0f;
        ApplyFill(0f);
    }

    private void Update()
    {
        if (!HasReferences())
            return;

        background.color = config.BackgroundColor;
        fill.color = config.FillColor;

        if (isHeld && !IsInteractable())
            Release();

        if (isHeld && !hasConfirmed)
        {
            progress = Mathf.Min(1f, progress + Time.deltaTime / config.HoldSeconds);
            if (progress >= 1f)
                Confirm();
        }
        else if (progress > 0f)
        {
            float rewindSeconds = drainingAfterConfirm ? config.ConfirmedRewindSeconds : config.RewindSeconds;
            progress = rewindSeconds > 0f
                ? Mathf.MoveTowards(progress, 0f, Time.deltaTime / rewindSeconds)
                : 0f;

            if (progress <= 0f)
                drainingAfterConfirm = false;
        }

        ApplyFill(config.EvaluateFill(progress));
    }

    private void ReleaseIfHolding(PointerEventData eventData)
    {
        if (isHeld && eventData.pointerId == heldPointerId)
            Release();
    }

    private void Release()
    {
        isHeld = false;
        hasConfirmed = false;
    }

    private bool IsInteractable()
    {
        return selectable == null || selectable.IsInteractable();
    }

    private void Confirm()
    {
        hasConfirmed = true;
        drainingAfterConfirm = true;
        SendHaptic();
        onConfirmed.Invoke();
    }

    private void SendHaptic()
    {
        if (config.HapticAmplitude <= 0f || XREALVirtualController.Singleton == null)
            return;

        XREALVirtualController.Singleton.Controller.SendHapticImpulse(0, config.HapticAmplitude, config.HapticSeconds);
    }

    private void ApplyFill(float share)
    {
        if (fill == null || Mathf.Approximately(share, appliedShare))
            return;

        appliedShare = share;
        fill.enabled = share > 0f;

        RectTransform fillRect = fill.rectTransform;
        Vector2 anchorMax = fillRect.anchorMax;
        anchorMax.x = share;
        fillRect.anchorMax = anchorMax;
    }

    private bool HasReferences()
    {
        if (config != null && background != null && fill != null)
            return true;

        if (!warned)
        {
            warned = true;
            Debug.LogWarning($"[HoldToConfirmButton] {name} is missing its config, background or fill. Holding does nothing.");
        }

        return false;
    }
}
