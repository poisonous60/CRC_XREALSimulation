using UnityEngine;

/// <summary>
/// Per-prefab opacity for the marker's center material, in place of the manager's Marker Alpha.
/// </summary>
[DisallowMultipleComponent]
public class MarkerAlphaOverride : MonoBehaviour
{
    [Header("(a) Marker")]
    [Tooltip("Opacity this look draws at. Replaces Marker Alpha on the manager. The controller hover and move boosts still add on top. A look that carries its own config overwrites this at Awake.")]
    [SerializeField, Range(0f, 1f)] private float markerAlpha = 0.18f;

    public float MarkerAlpha => markerAlpha;

    public void SetMarkerAlpha(float alpha)
    {
        markerAlpha = Mathf.Clamp01(alpha);
    }
}
