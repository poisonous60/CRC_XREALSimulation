using UnityEngine;

/// <summary>
/// Base for one swappable source representation: the marker, the off-screen cue, or the proximity info.
/// </summary>
public abstract class SourcePresentation : MonoBehaviour
{
    public abstract void Bind(Transform source, Camera head);
}
