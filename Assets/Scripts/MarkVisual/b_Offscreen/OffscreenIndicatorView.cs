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
    public virtual bool UsesEllipseClamp => false;

    public virtual bool UsesFixedEdgeCenter => false;

    public abstract void Show(string label, OffscreenEdge edge, Color color);

    // Called every frame the detector is back on screen. A look that fades out returns false
    // until it is done, and the placer leaves it active and calls again the next frame.
    public virtual bool Hide()
    {
        return true;
    }

    public virtual void SetDirection(float degrees)
    {
    }
}
