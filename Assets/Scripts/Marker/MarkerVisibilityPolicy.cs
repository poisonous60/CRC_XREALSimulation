using UnityEngine;

/// <summary>
/// Decides whether a marker may be seen, and switches its renderers to match.
/// </summary>
public class MarkerVisibilityPolicy
{
    public bool hideUntilServerConnected = true;
    public bool hideWithoutFreshRadiationData = true;
    public bool showPreviewBeforeServerConnected = true;
    public bool showGrayPreviewSphere = true;
    public bool requireRoomCalibration = true;
    public bool roomCoordinateSystemEnabled = true;
    public bool showLabel;
    public float maximumSnapshotAgeSeconds = 5f;

    public bool IsPreview(SourceMarker marker)
    {
        return marker != null &&
               marker.isFollowingPlacementOrigin &&
               !marker.isPlaced;
    }

    public void Apply(
        SourceMarker marker,
        RoomCoordinateSystem roomCoordinateSystem,
        RadiationSnapshot snapshot)
    {
        if (marker == null || snapshot == null)
            return;

        bool isPreview = IsPreview(marker);

        // The preview is a placement aid, not a measurement, so it does not have to
        // wait for the radiation server. Otherwise the sphere the user is aiming with
        // is invisible during an offline or not-yet-connected placement.
        bool previewIgnoresServerGate = showPreviewBeforeServerConnected && isPreview;

        bool serverReady = !hideUntilServerConnected || snapshot.ServerConnected;
        bool roomReady = isPreview ||
                         !roomCoordinateSystemEnabled ||
                         !requireRoomCalibration ||
                         (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated);
        bool radiationReady = isPreview ||
                              !hideWithoutFreshRadiationData ||
                              (snapshot.IsFresh(maximumSnapshotAgeSeconds) &&
                               snapshot.LiveDetectorCount > 0);

        bool visible = marker.visibilityRequested &&
                       roomReady &&
                       radiationReady &&
                       (previewIgnoresServerGate || serverReady);

        marker.isVisible = visible;

        bool centerVisible = marker.centerVisualRequested ||
                             (showGrayPreviewSphere && isPreview);

        // The placement look replaces the marker's own body while it is up, so the
        // two are never drawn on top of each other.
        bool usePlacementVisual = marker.placementVisual != null;

        if (usePlacementVisual)
            marker.placementVisual.SetActive(visible);

        // The object stays active while hidden so its own components keep running.
        if (marker.bodyRenderers != null && marker.bodyRenderers.Length > 0)
        {
            for (int i = 0; i < marker.bodyRenderers.Length; i++)
            {
                Renderer bodyRenderer = marker.bodyRenderers[i];

                if (bodyRenderer == null)
                    continue;

                bool wasEnabledAtCreation =
                    marker.bodyRendererDefaults == null ||
                    i >= marker.bodyRendererDefaults.Length ||
                    marker.bodyRendererDefaults[i];

                bool isCenter = bodyRenderer == marker.renderer;
                bodyRenderer.enabled = !usePlacementVisual &&
                    (isCenter
                        ? visible && centerVisible
                        : visible && wasEnabledAtCreation);
            }
        }
        else if (marker.renderer != null)
        {
            marker.renderer.enabled = !usePlacementVisual && visible && centerVisible;
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
}
