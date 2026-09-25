using System;
using System.Collections.Generic;
using Unity.XR.XREAL;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Bare spatial-anchor scene: aims source discs with the gaze, anchors and saves them, and restores them at start.
/// Every anchor step goes through SpatialAnchorStore.
/// </summary>
[DisallowMultipleComponent]
public sealed class AnchorTestScene : MonoBehaviour
{
    // Named when only one anchor was kept; unchanged so an anchor saved then still restores.
    private const string SavedGuidsKey = "RadVis.AnchorTest.SavedGuid";
    private const char SavedGuidSeparator = ',';

    [Header("References")]
    [Tooltip("XR Origin's anchor manager.")]
    [SerializeField] private ARAnchorManager anchorManager;

    [Tooltip("XR Origin's plane manager. The disc lands where the gaze meets one of its planes.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("Head camera. Its forward ray aims the disc and its pose is read for the anchor quality.")]
    [SerializeField] private Transform head;

    [Tooltip("Scene config whose Marker Prefab and Marker Size Meters draw the source disc.")]
    [SerializeField] private MarkVisualConfig markVisualConfig;

    [Tooltip("Panel laid over the Beam Pro's stock panel while this scene runs.")]
    [SerializeField] private AnchorTestPanel panelPrefab;

    [Tooltip("Takes the Photo button's JPEG.")]
    [SerializeField] private XREALCaptureManager captureManager;

    [Header("Aim")]
    [Tooltip("Plane hits nearer than this are ignored. Same meaning as on the demo's marker manager.")]
    [SerializeField, Min(0f)] private float minPlaneHitDistanceMeters = 0.15f;

    [Tooltip("Plane hits farther than this are ignored.")]
    [SerializeField, Min(0.1f)] private float maxPlaneHitDistanceMeters = 10f;

    [Tooltip("Distance straight ahead when no plane is under the gaze.")]
    [SerializeField, Min(0.1f)] private float defaultPlacementDistanceMeters = 2f;

    [Header("Anchor")]
    [Tooltip("Seconds between two quality readings of the newest unsaved anchor.")]
    [SerializeField, Min(0.1f)] private float qualityIntervalSeconds = 0.5f;

    [Tooltip("A save that has not answered after this many seconds is reported as failed.")]
    [SerializeField, Min(1f)] private float saveTimeoutSeconds = 10f;

    [Tooltip("Seconds the start-up restore waits for the anchor subsystem.")]
    [SerializeField, Min(0f)] private float subsystemReadyTimeoutSeconds = 8f;

    [Tooltip("A load that XREAL has not answered after this many seconds is reported as failed. Recognizing the room has no limit.")]
    [SerializeField, Min(1f)] private float restoreTimeoutSeconds = 30f;

    private readonly GazePlaneProbe gazeProbe = new GazePlaneProbe();
    private readonly List<ARAnchor> unsavedAnchors = new List<ARAnchor>();
    private readonly List<ARAnchor> savedAnchors = new List<ARAnchor>();
    private SpatialAnchorStore store;
    private AnchorTestPanel panel;
    private GameObject aimDisc;
    private bool saving;
    // Clear and scene end bump this, so a save or restore that finishes afterwards is dropped.
    private int clearVersion;
    private float nextQualityTime;
    private string anchorStatus = "";
    private string captureStatus = "";

    private void OnEnable()
    {
        XREALCaptureManager.OnCaptureStateChanged += HandleCaptureStateChanged;
    }

    private void OnDisable()
    {
        XREALCaptureManager.OnCaptureStateChanged -= HandleCaptureStateChanged;
    }

    private void Start()
    {
        store = new SpatialAnchorStore(anchorManager);
        gazeProbe.Configure(head, planeManager, minPlaneHitDistanceMeters, maxPlaneHitDistanceMeters);
        MountPanel();
        RestoreSavedAnchors();
    }

    private void OnDestroy()
    {
        clearVersion++;

        // The controller prefab outlives the scene, so the panel laid on it is taken down here.
        if (panel != null)
            Destroy(panel.gameObject);
    }

    private void Update()
    {
        if (aimDisc != null)
            MoveDiscToGaze();
        else if (!saving && unsavedAnchors.Count > 0 && Time.unscaledTime >= nextQualityTime)
            ShowQuality();
    }

    private void MountPanel()
    {
        ControllerScreenSwitcher stockPanel =
            FindFirstObjectByType<ControllerScreenSwitcher>(FindObjectsInactive.Include);

        if (stockPanel == null)
        {
            Debug.LogWarning("[AnchorTestScene] Beam Pro controller panel not found. The anchor panel is not shown.");
            return;
        }

        panel = Instantiate(panelPrefab, stockPanel.transform);
        panel.PlacePressed += HandlePlacePressed;
        panel.PlaceReleased += HandlePlaceReleased;
        panel.SaveClicked += HandleSaveClicked;
        panel.ClearClicked += HandleClearClicked;
        panel.PhotoClicked += HandlePhotoClicked;
        RefreshStatus();
    }

    private async void RestoreSavedAnchors()
    {
        string[] guids = ReadSavedGuids();

        if (guids.Length == 0)
        {
            SetAnchorStatus("No saved source. Hold Place to set one.");
            return;
        }

        int version = clearVersion;
        SetAnchorStatus("Restoring...");

        float readyDeadline = Time.realtimeSinceStartup + subsystemReadyTimeoutSeconds;
        while (!store.IsReady && Time.realtimeSinceStartup < readyDeadline)
            await Awaitable.NextFrameAsync();

        int loaded = 0;
        string failure = "";

        // One at a time, the way XREAL's own sample loads saved anchors.
        for (int i = 0; i < guids.Length; i++)
        {
            AnchorLoadResult result = await store.LoadAsync(guids[i], restoreTimeoutSeconds);

            if (version != clearVersion)
            {
                if (result.Succeeded)
                    Destroy(result.Anchor.gameObject);

                return;
            }

            if (!result.Succeeded)
            {
                failure = result.FailureReason;
                continue;
            }

            savedAnchors.Add(result.Anchor);
            if (TryCreateDisc(out GameObject disc))
                AttachDisc(disc, result.Anchor);

            loaded++;
        }

        SetAnchorStatus(loaded == guids.Length
            ? "Loaded. Look around. Each disc appears once its spot is recognized."
            : $"Loaded {loaded} of {guids.Length}. Last failure: {failure}");
    }

    private void HandlePlacePressed()
    {
        if (aimDisc != null || saving)
            return;

        if (!TryCreateDisc(out aimDisc))
            return;

        MoveDiscToGaze();
        SetAnchorStatus("Aiming. Release to place.");
    }

    private void HandlePlaceReleased()
    {
        if (aimDisc == null)
            return;

        GameObject disc = aimDisc;
        aimDisc = null;

        if (!store.TryCreate(new Pose(disc.transform.position, disc.transform.rotation), out ARAnchor created))
        {
            Destroy(disc);
            SetAnchorStatus("Place failed: the anchor was not created.");
            return;
        }

        AttachDisc(disc, created);
        unsavedAnchors.Add(created);
        nextQualityTime = 0f;
        SetAnchorStatus("Placed. Look around, then Save.");
    }

    private async void HandleSaveClicked()
    {
        if (aimDisc != null || saving || unsavedAnchors.Count == 0)
            return;

        int version = clearVersion;
        List<ARAnchor> toSave = new List<ARAnchor>(unsavedAnchors);
        string failure = "";
        saving = true;

        for (int i = 0; i < toSave.Count; i++)
        {
            SetAnchorStatus($"Saving {i + 1} of {toSave.Count}...");
            AnchorSaveResult result = await store.SaveAsync(toSave[i], saveTimeoutSeconds);

            if (version != clearVersion)
            {
                if (result.Succeeded)
                    _ = store.EraseAsync(result.PersistentGuid);

                return;
            }

            if (!result.Succeeded)
            {
                failure = result.FailureReason;
                continue;
            }

            AppendSavedGuid(result.PersistentGuid);
            unsavedAnchors.Remove(toSave[i]);
            savedAnchors.Add(toSave[i]);
        }

        saving = false;
        SetAnchorStatus(string.IsNullOrEmpty(failure)
            ? "Saved. They come back at the next start."
            : $"Save failed: {failure}");
    }

    private void HandleClearClicked()
    {
        // XREAL holds anchor removal until a running save ends, which froze the app for seconds.
        if (saving)
            return;

        clearVersion++;

        if (aimDisc != null)
            Destroy(aimDisc);

        aimDisc = null;
        DestroyAnchors(unsavedAnchors);
        DestroyAnchors(savedAnchors);

        foreach (string guid in ReadSavedGuids())
            _ = store.EraseAsync(guid);

        PlayerPrefs.DeleteKey(SavedGuidsKey);
        PlayerPrefs.Save();
        SetAnchorStatus("Cleared. Hold Place to set a source.");
    }

    private void HandlePhotoClicked()
    {
        if (captureManager == null)
        {
            Debug.LogWarning("[AnchorTestScene] XREALCaptureManager is not assigned. Photo does nothing.");
            return;
        }

        captureManager.TakeJpegPhoto();
    }

    private void HandleCaptureStateChanged(string message, bool recording)
    {
        captureStatus = message;
        RefreshStatus();
    }

    private void MoveDiscToGaze()
    {
        if (!gazeProbe.TryGetIntersection(out Vector3 position, out Quaternion rotation, out float _, false, 1f))
        {
            position = head.position + head.forward * defaultPlacementDistanceMeters;
            rotation = Quaternion.LookRotation(head.forward, head.up);
        }

        aimDisc.transform.SetPositionAndRotation(position, rotation);
    }

    private void ShowQuality()
    {
        nextQualityTime = Time.unscaledTime + qualityIntervalSeconds;
        ARAnchor newest = unsavedAnchors[unsavedAnchors.Count - 1];

        if (store.TryGetQuality(newest, new Pose(head.position, head.rotation), out XREALAnchorEstimateQuality quality))
            SetAnchorStatus($"Placed. Quality {QualityWord(quality)}. Look around, then Save.");
    }

    private bool TryCreateDisc(out GameObject disc)
    {
        disc = null;

        if (markVisualConfig == null || !markVisualConfig.TryGetLook(LookRoleKind.SourceMarker, out GameObject sourceLook))
        {
            Debug.LogWarning("[AnchorTestScene] Mark Visual Config or its SourceMarker look is missing. No disc to place.");
            return false;
        }

        disc = Instantiate(sourceLook);
        disc.transform.localScale = Vector3.one * markVisualConfig.MarkerSizeMeters;
        return true;
    }

    private static void AttachDisc(GameObject disc, ARAnchor anchor)
    {
        disc.transform.SetParent(anchor.transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localRotation = Quaternion.identity;
    }

    // The disc is a child of its anchor, so destroying the anchor takes the disc with it.
    private static void DestroyAnchors(List<ARAnchor> anchors)
    {
        foreach (ARAnchor anchor in anchors)
        {
            if (anchor != null)
                Destroy(anchor.gameObject);
        }

        anchors.Clear();
    }

    private static string[] ReadSavedGuids()
    {
        return PlayerPrefs.GetString(SavedGuidsKey, "").Split(SavedGuidSeparator, StringSplitOptions.RemoveEmptyEntries);
    }

    private static void AppendSavedGuid(string guid)
    {
        string saved = PlayerPrefs.GetString(SavedGuidsKey, "");
        PlayerPrefs.SetString(SavedGuidsKey, string.IsNullOrEmpty(saved) ? guid : saved + SavedGuidSeparator + guid);
        PlayerPrefs.Save();
    }

    private void SetAnchorStatus(string status)
    {
        anchorStatus = status;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (panel == null)
            return;

        panel.SetStatus(string.IsNullOrEmpty(captureStatus)
            ? anchorStatus
            : anchorStatus + "\n" + captureStatus);
    }

    private static string QualityWord(XREALAnchorEstimateQuality quality)
    {
        switch (quality)
        {
            case XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_GOOD:
                return "GOOD";
            case XREALAnchorEstimateQuality.XREAL_ANCHOR_ESTIMATE_QUALITY_SUFFICIENT:
                return "SUFFICIENT";
            default:
                return "INSUFFICIENT";
        }
    }
}
