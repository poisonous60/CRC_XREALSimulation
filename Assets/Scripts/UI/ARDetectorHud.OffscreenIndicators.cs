using System.Collections.Generic;
using UnityEngine;

public partial class ARDetectorHud
{
    private readonly OffscreenIndicatorPlacer offscreenIndicators = new OffscreenIndicatorPlacer();
    private readonly List<DetectorWorldMarkerManager.DetectorHudMarkerState> cueStates =
        new List<DetectorWorldMarkerManager.DetectorHudMarkerState>();

    private void UpdateOffscreenIndicators()
    {
        offscreenIndicators.Configure(targetCamera, indicatorLayer, screenEdgeMargin);
        offscreenIndicators.Refresh(GetCueStates());
    }

    private List<DetectorWorldMarkerManager.DetectorHudMarkerState> GetCueStates()
    {
        if (markerManager == null || !markerManager.OffscreenCueForSourceOnly)
            return markerStates;

        cueStates.Clear();

        for (int index = 0; index < markerStates.Count; index++)
        {
            if (markerManager.IsSourceMarkerKey(markerStates[index].detectorId))
                cueStates.Add(markerStates[index]);
        }

        return cueStates;
    }
}
