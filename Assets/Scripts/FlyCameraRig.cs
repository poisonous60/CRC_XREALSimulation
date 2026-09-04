using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Editor free camera: right mouse looks, WASD moves, Q/E vertical, Shift sprints.
/// </summary>
public class FlyCameraRig : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base travel speed in meters per second.")]
    [SerializeField, Min(0f)] private float moveSpeedMetersPerSecond = 3f;

    [Tooltip("Speed multiplier while either Shift key is held.")]
    [SerializeField, Min(1f)] private float sprintMultiplier = 3f;

    [Header("Look")]
    [Tooltip("Degrees of rotation per pixel of mouse movement.")]
    [SerializeField, Min(0f)] private float lookSensitivityDegreesPerPixel = 0.1f;

    [Tooltip("Vertical look limit measured from the horizon.")]
    [SerializeField, Range(1f, 89f)] private float maximumPitchDegrees = 85f;

    private float yawDegrees;
    private float pitchDegrees;

    private void OnEnable()
    {
        Vector3 eulerAngles = transform.eulerAngles;
        yawDegrees = eulerAngles.y;
        pitchDegrees = eulerAngles.x > 180f ? eulerAngles.x - 360f : eulerAngles.x;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        UpdateLook();
        UpdateMove(keyboard);
    }

    private void UpdateLook()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null || !mouse.rightButton.isPressed)
            return;

        Vector2 mouseDelta = mouse.delta.ReadValue();
        yawDegrees += mouseDelta.x * lookSensitivityDegreesPerPixel;
        pitchDegrees = Mathf.Clamp(
            pitchDegrees - mouseDelta.y * lookSensitivityDegreesPerPixel,
            -maximumPitchDegrees,
            maximumPitchDegrees);

        transform.rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
    }

    private void UpdateMove(Keyboard keyboard)
    {
        float forwardInput = 0f;
        float rightInput = 0f;
        float upInput = 0f;

        if (keyboard.wKey.isPressed)
            forwardInput += 1f;

        if (keyboard.sKey.isPressed)
            forwardInput -= 1f;

        if (keyboard.dKey.isPressed)
            rightInput += 1f;

        if (keyboard.aKey.isPressed)
            rightInput -= 1f;

        if (keyboard.eKey.isPressed)
            upInput += 1f;

        if (keyboard.qKey.isPressed)
            upInput -= 1f;

        Vector3 direction =
            transform.forward * forwardInput +
            transform.right * rightInput +
            Vector3.up * upInput;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        float speed = moveSpeedMetersPerSecond;

        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            speed *= sprintMultiplier;

        transform.position += direction.normalized * (speed * Time.deltaTime);
    }
}
