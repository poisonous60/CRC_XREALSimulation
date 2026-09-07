using UnityEngine;

public partial class ARDetectorHud
{
    private readonly OffscreenIndicatorPlacer offscreenIndicators = new OffscreenIndicatorPlacer();

    private void UpdateOffscreenIndicators()
    {
        offscreenIndicators.Configure(targetCamera, indicatorLayer, screenEdgeMargin);
        offscreenIndicators.Refresh(markerStates);
    }
}
