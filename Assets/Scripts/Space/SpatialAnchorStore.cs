using System;
using System.Threading.Tasks;
using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Every spatial-anchor operation and nothing else: create, quality, save, load, erase.
/// Callers decide when to save, where the GUID is kept and what the anchor carries.
/// </summary>
public sealed class SpatialAnchorStore
{
    private static readonly Vector3 LoadedAnchorWaitingPosition = new Vector3(9999f, 9999f, 9999f);
    private const float EraseTimeoutSeconds = 10f;

    private readonly ARAnchorManager anchorManager;

    public SpatialAnchorStore(ARAnchorManager manager)
    {
        anchorManager = manager;
    }

    public bool IsReady =>
        anchorManager != null &&
        anchorManager.isActiveAndEnabled &&
        anchorManager.subsystem != null;

    public bool TryCreate(Pose pose, out ARAnchor anchor)
    {
        anchor = null;

        if (!IsReady)
        {
            Debug.LogWarning("[SpatialAnchorStore] Cannot create an anchor. The anchor subsystem is not ready.");
            return false;
        }

        // XREAL's Unity 6 TryAddAnchorAsync completes from Task.Run without returning to the
        // main thread, so the anchor is added synchronously the way XREAL's own sample does.
        GameObject anchorObject = new GameObject("SpatialAnchor");
        anchorObject.transform.SetPositionAndRotation(pose.position, pose.rotation);
        ARAnchor created = anchorObject.AddComponent<ARAnchor>();

        if (created == null || !created.enabled || created.trackableId == TrackableId.invalidId)
        {
            Debug.LogWarning("[SpatialAnchorStore] XREAL failed to add the runtime anchor.");
            UnityEngine.Object.Destroy(anchorObject);
            return false;
        }

        anchor = created;
        return true;
    }

    public bool TryGetQuality(ARAnchor anchor, Pose headPose, out XREALAnchorEstimateQuality quality)
    {
        quality = XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_INSUFFICIENT;

        if (!IsReady || anchor == null)
            return false;

        quality = anchorManager.GetAnchorQuality(anchor.trackableId, headPose);
        return true;
    }

    public async Awaitable<AnchorSaveResult> SaveAsync(ARAnchor anchor, float timeoutSeconds)
    {
        if (!IsReady || anchor == null)
            return AnchorSaveResult.Failed(Warn("Cannot save. The anchor subsystem is not ready."));

        Task<Result<SerializableGuid>> save = AsTask(anchorManager.TrySaveAnchorAsync(anchor));

        if (!await WaitAsync(save, timeoutSeconds))
        {
            _ = EraseLateSaveAsync(save);
            return AnchorSaveResult.Failed(Warn($"Save did not answer within {timeoutSeconds:F0} s."));
        }

        if (save.IsFaulted)
            return AnchorSaveResult.Failed(Warn($"Save threw: {save.Exception.GetBaseException().Message}"));

        Result<SerializableGuid> result = save.Result;
        if (!result.status.IsSuccess())
            return AnchorSaveResult.Failed(Warn($"Save failed: {result.status.statusCode}"));

        // SerializableGuid.ToString() is two 16-digit hex numbers, not a System.Guid string.
        return AnchorSaveResult.Saved(result.value.guid.ToString("D"));
    }

    public async Awaitable<AnchorLoadResult> LoadAsync(string guid, float timeoutSeconds)
    {
        if (!IsReady)
            return AnchorLoadResult.Failed(Warn("Cannot load. The anchor subsystem is not ready."));

        if (!TryParseMapFileGuid(guid, out SerializableGuid mapFileGuid))
            return AnchorLoadResult.Failed(Warn($"Invalid anchor GUID: {guid}"));

        float deadline = Time.realtimeSinceStartup + timeoutSeconds;
        Task<Result<ARAnchor>> load = AsTask(anchorManager.TryLoadAnchorAsync(mapFileGuid));

        if (!await WaitAsync(load, timeoutSeconds))
        {
            _ = DestroyLateLoadAsync(load);
            return AnchorLoadResult.Failed(Warn($"Load did not answer within {timeoutSeconds:F0} s."));
        }

        if (load.IsFaulted)
            return AnchorLoadResult.Failed(Warn($"Load threw: {load.Exception.GetBaseException().Message}"));

        Result<ARAnchor> result = load.Result;
        if (!result.status.IsSuccess() || result.value == null)
            return AnchorLoadResult.Failed(Warn($"Load failed: {result.status.statusCode}"));

        ARAnchor anchor = result.value;

        // A loaded anchor comes back before the room is recognized. Parking it far away tells the
        // pose the provider sets once it relocalizes apart from the provisional one.
        anchor.transform.position = LoadedAnchorWaitingPosition;

        while (anchor != null && !IsLocated(anchor))
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                UnityEngine.Object.Destroy(anchor.gameObject);
                return AnchorLoadResult.Failed(Warn($"The room was not recognized within {timeoutSeconds:F0} s."));
            }

            await Awaitable.NextFrameAsync();
        }

        if (anchor == null)
            return AnchorLoadResult.Failed(Warn("The loaded anchor was destroyed while waiting."));

        return AnchorLoadResult.Loaded(anchor);
    }

    public async Awaitable<bool> EraseAsync(string guid)
    {
        if (!IsReady || !TryParseMapFileGuid(guid, out SerializableGuid mapFileGuid))
        {
            Warn($"Cannot erase anchor GUID: {guid}");
            return false;
        }

        Task<XRResultStatus> erase = AsTask(anchorManager.TryEraseAnchorAsync(mapFileGuid));

        if (!await WaitAsync(erase, EraseTimeoutSeconds))
        {
            Warn($"Erase did not answer within {EraseTimeoutSeconds:F0} s for {guid}.");
            return false;
        }

        if (erase.IsFaulted)
        {
            Warn($"Erase threw for {guid}: {erase.Exception.GetBaseException().Message}");
            return false;
        }

        if (!erase.Result.IsSuccess())
        {
            Warn($"Erase failed for {guid}: {erase.Result.statusCode}");
            return false;
        }

        return true;
    }

    // XREAL 3.1.0 names the map file after the saved GUID's bytes in memory order, but load and erase
    // look it up by Guid.ToString(), which prints the first three fields byte-reversed.
    private static bool TryParseMapFileGuid(string text, out SerializableGuid mapFileGuid)
    {
        mapFileGuid = default;

        if (!Guid.TryParse(text, out Guid savedGuid))
            return false;

        string fileName = BitConverter.ToString(savedGuid.ToByteArray()).Replace("-", "");
        mapFileGuid = new SerializableGuid(Guid.ParseExact(fileName, "N"));
        return true;
    }

    private static bool IsLocated(ARAnchor anchor)
    {
        return anchor.trackingState == TrackingState.Tracking &&
               (anchor.transform.position - LoadedAnchorWaitingPosition).sqrMagnitude > 1f;
    }

    // A save that answers after its timeout still wrote a map file that no caller knows about.
    private async Awaitable EraseLateSaveAsync(Task<Result<SerializableGuid>> save)
    {
        await Task.WhenAny(save);

        if (save.IsCompletedSuccessfully && save.Result.status.IsSuccess())
            await EraseAsync(save.Result.value.guid.ToString("D"));
    }

    private static async Awaitable DestroyLateLoadAsync(Task<Result<ARAnchor>> load)
    {
        await Task.WhenAny(load);

        if (load.IsCompletedSuccessfully && load.Result.value != null)
            UnityEngine.Object.Destroy(load.Result.value.gameObject);
    }

    // XREAL finishes save, load and erase inside Task.Run and never completes them when that task
    // throws. Awaiting the Task from the main thread resumes there through Unity's synchronization
    // context, so no Unity call runs on XREAL's worker thread.
    private static async Awaitable<bool> WaitAsync(Task task, float timeoutSeconds)
    {
        await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
        return task.IsCompleted;
    }

    private static async Task<T> AsTask<T>(Awaitable<T> awaitable)
    {
        return await awaitable;
    }

    private static string Warn(string message)
    {
        Debug.LogWarning($"[SpatialAnchorStore] {message}");
        return message;
    }
}

public readonly struct AnchorSaveResult
{
    private AnchorSaveResult(bool succeeded, string persistentGuid, string failureReason)
    {
        Succeeded = succeeded;
        PersistentGuid = persistentGuid;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }
    public string PersistentGuid { get; }
    public string FailureReason { get; }

    public static AnchorSaveResult Saved(string persistentGuid) => new AnchorSaveResult(true, persistentGuid, "");
    public static AnchorSaveResult Failed(string reason) => new AnchorSaveResult(false, "", reason);
}

public readonly struct AnchorLoadResult
{
    private AnchorLoadResult(bool succeeded, ARAnchor anchor, string failureReason)
    {
        Succeeded = succeeded;
        Anchor = anchor;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }
    public ARAnchor Anchor { get; }
    public string FailureReason { get; }

    public static AnchorLoadResult Loaded(ARAnchor anchor) => new AnchorLoadResult(true, anchor, "");
    public static AnchorLoadResult Failed(string reason) => new AnchorLoadResult(false, null, reason);
}
