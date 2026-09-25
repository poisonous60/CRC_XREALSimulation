using UnityEngine;

public enum LookRoleKind
{
    SourceMarker,
    DetectorMarker,
    PlacementPreview,
    Standalone
}

/// <summary>
/// Says what one look prefab is for, so the single look list in MarkVisualConfig can hold every look.
/// </summary>
[DisallowMultipleComponent]
public sealed class LookRole : MonoBehaviour
{
    [Header("Role")]
    [Tooltip("SourceMarker = body of the Source marker. DetectorMarker = body of every other marker. PlacementPreview = drawn instead of the body while Add Source / Place is in progress. Standalone = spawned once when the scene starts, then runs on its own.")]
    [SerializeField] private LookRoleKind role;

    public LookRoleKind Role => role;
}
