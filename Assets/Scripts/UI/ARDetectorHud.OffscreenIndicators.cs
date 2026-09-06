using UnityEngine;

public partial class ARDetectorHud
{
    private readonly OffscreenIndicatorPlacer offscreenIndicators = new OffscreenIndicatorPlacer();

    private void UpdateOffscreenIndicators()
    {
        offscreenIndicators.Configure(
            targetCamera,
            indicatorLayer,
            screenEdgeMargin,
            GetHudAvoidanceTop(),
            GetHudAvoidanceLeft());

        offscreenIndicators.Refresh(markerStates);
    }

    private float GetHudAvoidanceTop()
    {
        float hudTop = hudViewportAnchor.y + hudPixelSize.y / ReferenceHeight;
        return Mathf.Clamp(hudTop + 0.02f, 0.15f, 0.85f);
    }

    private float GetHudAvoidanceLeft()
    {
        float hudLeft = hudViewportAnchor.x - hudPixelSize.x / ReferenceWidth;
        return Mathf.Clamp(hudLeft - 0.02f, 0.15f, 0.85f);
    }
}
