using UnityEngine;

/// <summary>
/// Base for one swappable uncertainty footprint drawn on the surface the source sits on.
/// </summary>
public abstract class UncertaintyPresentation : MonoBehaviour
{
    public abstract void Show(Pose surfacePose, float radiusMeters, Color color);
}
