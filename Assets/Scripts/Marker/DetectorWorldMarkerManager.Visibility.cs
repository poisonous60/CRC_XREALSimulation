using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private bool IsPreviewMarker(MarkerInfo marker)
    {
        return marker != null &&
               marker.isFollowingPlacementOrigin &&
               !marker.isPlaced;
    }

    private void ForceMarkerVisible(MarkerInfo marker)
    {
        if (marker == null)
            return;

        SetMarkerRequestedVisibility(marker, true);
    }

    private void SetMarkerRequestedVisibility(MarkerInfo marker, bool visible)
    {
        if (marker == null)
            return;

        marker.visibilityRequested = visible;
        ApplyMarkerVisibility(marker);
    }

    private void ApplyMarkerVisibility(MarkerInfo marker)
    {
        if (marker == null)
            return;

        bool isPreview = IsPreviewMarker(marker);

        // The preview is a placement aid, not a measurement, so it does not have to
        // wait for the radiation server. Otherwise the sphere the user is aiming with
        // is invisible during an offline or not-yet-connected placement.
        bool previewIgnoresServerGate =
            showPreviewBeforeServerConnected && isPreview;

        bool serverReady = !hideMarkersUntilServerConnected || serverConnected;
        bool roomReady = isPreview ||
                         !enableRoomCoordinateSystem ||
                         !requireRoomCalibrationBeforeDetectorPlacement ||
                         (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated);
        bool radiationReady = isPreview ||
                              !hideMarkersWithoutFreshRadiationData ||
                              (IsRadiationSnapshotFresh() &&
                               liveRadiationDetectorIds.Count > 0);

        bool visible = marker.visibilityRequested &&
                       roomReady &&
                       radiationReady &&
                       (previewIgnoresServerGate || serverReady);

        if (marker.root != null)
            marker.root.SetActive(visible);

        if (marker.renderer != null)
        {
            bool centerVisible = marker.centerVisualRequested ||
                                 (showGrayPreviewSphere && IsPreviewMarker(marker));
            marker.renderer.enabled = visible && centerVisible;
        }

        if (marker.falloffShells != null)
        {
            for (int i = 0; i < marker.falloffShells.Count; i++)
            {
                FalloffShellInfo shell = marker.falloffShells[i];
                if (shell != null && shell.renderer != null)
                    shell.renderer.enabled = visible && shell.visualRequested;
            }
        }

        if (showLabel && marker.label != null)
            marker.label.gameObject.SetActive(visible);
    }

    private bool IsRadiationSnapshotFresh()
    {
        return serverConnected &&
               hasReceivedRadiationSnapshot &&
               Time.unscaledTime - lastRadiationSnapshotTime <=
               Mathf.Max(0.5f, maximumRadiationSnapshotAgeSeconds);
    }

    private void RefreshRadiationSnapshotVisibilityIfExpired()
    {
        bool snapshotIsFresh = IsRadiationSnapshotFresh();
        if (snapshotIsFresh == lastSnapshotFreshnessState)
            return;

        lastSnapshotFreshnessState = snapshotIsFresh;
        foreach (var pair in markers)
            ApplyMarkerVisibility(pair.Value);
    }
}
