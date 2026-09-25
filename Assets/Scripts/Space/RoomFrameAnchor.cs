using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// One unsaved spatial anchor at the room frame, created once and never moved. It gives the frame
/// pose to follow while the picture is out of view, at the offset taken at the last sighting.
/// </summary>
public sealed class RoomFrameAnchor
{
    private const float CreateRetrySeconds = 1f;

    private readonly SpatialAnchorStore store;

    private ARAnchor anchor;
    private Pose offset = Pose.identity;
    private bool offsetTakenWhileTracking;
    private float nextCreateTime;

    public static event System.Action<Transform> AnchorChanged;

    public RoomFrameAnchor(ARAnchorManager manager)
    {
        store = new SpatialAnchorStore(manager);
    }

    public void Commit(Pose framePose)
    {
        if (anchor == null)
        {
            // TryCreate warns on every refusal, and this runs on every sighting.
            if (!store.IsReady || Time.time < nextCreateTime)
                return;

            if (!store.TryCreate(framePose, out anchor))
            {
                nextCreateTime = Time.time + CreateRetrySeconds;
                return;
            }

            AnchorChanged?.Invoke(anchor.transform);
        }

        Transform anchorTransform = anchor.transform;
        offset = new Pose(
            anchorTransform.InverseTransformPoint(framePose.position),
            Quaternion.Inverse(anchorTransform.rotation) * framePose.rotation);

        // An offset measured against a pose the provider has not confirmed would turn its
        // later correction into a step of the frame.
        offsetTakenWhileTracking = anchor.trackingState == TrackingState.Tracking;
    }

    public bool TryGetFollowPose(out Pose framePose)
    {
        framePose = Pose.identity;

        if (anchor == null || !offsetTakenWhileTracking || anchor.trackingState != TrackingState.Tracking)
            return false;

        Transform anchorTransform = anchor.transform;
        Quaternion rotation = anchorTransform.rotation * offset.rotation;

        // The room frame is gravity up and yaw only, whatever tilt the anchor picked up.
        Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            return false;

        framePose = new Pose(
            anchorTransform.TransformPoint(offset.position),
            Quaternion.LookRotation(forward.normalized, Vector3.up));
        return true;
    }

    public void Remove()
    {
        if (anchor != null)
        {
            Object.Destroy(anchor.gameObject);
            AnchorChanged?.Invoke(null);
        }

        anchor = null;
        offsetTakenWhileTracking = false;
    }
}
