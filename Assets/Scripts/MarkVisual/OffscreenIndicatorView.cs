using UnityEngine;

public enum OffscreenEdge
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Look of one off-screen detector indicator. ARDetectorHud places it and calls Show.
/// </summary>
public abstract class OffscreenIndicatorView : MonoBehaviour
{
    public abstract void Show(string label, OffscreenEdge edge, Color color);
}
