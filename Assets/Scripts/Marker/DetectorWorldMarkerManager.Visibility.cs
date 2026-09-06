using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public partial class DetectorWorldMarkerManager
{

    private bool IsPreviewMarker(SourceMarker marker)
    {
        return marker != null &&
               marker.isFollowingPlacementOrigin &&
               !marker.isPlaced;
    }

    private void ForceMarkerVisible(SourceMarker marker)
    {
        if (marker == null)
            return;

        SetMarkerRequestedVisibility(marker, true);
    }

    private void SetMarkerRequestedVisibility(SourceMarker marker, bool visible)
    {
        if (marker == null)
            return;

        marker.visibilityRequested = visible;
        ApplyMarkerVisibility(marker);
    }

    private void ApplyMarkerVisibility(SourceMarker marker)
    {
        if (marker == null)
            return;

        bool isPreview = IsPreviewMarker(marker);

        // The preview is a placement aid, not a measurement, so it does not have to
        // wait for the radiation server. Otherwise the sphere the user is aiming with
        // is invisible during an offline or not-yet-connected placement.
        bool previewIgnoresServerGate =
            showPreviewBeforeServerConnected && isPreview;

        bool serverReady = !hideMarkersUntilServerConnected || radiationSnapshot.ServerConnected;
        bool roomReady = isPreview ||
                         !enableRoomCoordinateSystem ||
                         !requireRoomCalibrationBeforeDetectorPlacement ||
                         (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated);
        bool radiationReady = isPreview ||
                              !hideMarkersWithoutFreshRadiationData ||
                              (IsRadiationSnapshotFresh() &&
                               radiationSnapshot.LiveDetectorCount > 0);

        bool visible = marker.visibilityRequested &&
                       roomReady &&
                       radiationReady &&
                       (previewIgnoresServerGate || serverReady);

        marker.isVisible = visible;

        bool centerVisible = marker.centerVisualRequested ||
                             (showGrayPreviewSphere && IsPreviewMarker(marker));

        // The object stays active while hidden so its own components keep running.
        if (marker.bodyRenderers != null && marker.bodyRenderers.Length > 0)
        {
            for (int i = 0; i < marker.bodyRenderers.Length; i++)
            {
                Renderer bodyRenderer = marker.bodyRenderers[i];

                if (bodyRenderer == null)
                    continue;

                bool isCenter = bodyRenderer == marker.renderer;
                bodyRenderer.enabled = visible && (!isCenter || centerVisible);
            }
        }
        else if (marker.renderer != null)
        {
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
        return radiationSnapshot.IsFresh(maximumRadiationSnapshotAgeSeconds);
    }

    private void RefreshRadiationSnapshotVisibilityIfExpired()
    {
        bool snapshotIsFresh = IsRadiationSnapshotFresh();
        if (snapshotIsFresh == radiationSnapshot.LastFreshnessState)
            return;

        radiationSnapshot.RememberFreshness(snapshotIsFresh);
        foreach (var pair in markers)
            ApplyMarkerVisibility(pair.Value);
    }
}
