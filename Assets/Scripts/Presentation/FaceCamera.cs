using UnityEngine;

/// <summary>
/// Turns this object to the head camera every frame so a flat marker keeps one face.
/// </summary>
[DisallowMultipleComponent]
public class FaceCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Camera to face. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    private void LateUpdate()
    {
        if (head == null)
            head = Camera.main;

        if (head == null)
            return;

        transform.rotation = head.transform.rotation;
    }
}
