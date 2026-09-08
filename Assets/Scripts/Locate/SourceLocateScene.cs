using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Draws the estimated source, its uncertainty disc and the four detector labels.
/// </summary>
[DisallowMultipleComponent]
public sealed class SourceLocateScene : MonoBehaviour
{
    private const float LabelFontSize = 4f;

    [Header("Configuration")]
    [Tooltip("Paper geometry, calibration file and look of this scene.")]
    [SerializeField] private SourceLocateConfig config;

    [Header("References")]
    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private RoomCoordinateSystem roomCoordinateSystem;

    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    [Tooltip("Optional. The first one in the scene is used when empty.")]
    [SerializeField] private RadiationReceiver radiationReceiver;

    private SourceLocator locator;
    private readonly List<UncertaintyPresentation> uncertaintyViews =
        new List<UncertaintyPresentation>();
    private Color uncertaintyColor = Color.white;
    private readonly List<TMP_Text> detectorLabels = new List<TMP_Text>();
    private bool subscribed;
    private bool hasEstimate;
    private Vector3 targetRoomPoint;
    private Vector3 shownRoomPoint;
    private float shownRadiusMeters;
    private float targetRadiusMeters;
    private Vector3 appliedSourceWorldPoint;
    private bool hasAppliedSource;
    private bool estimateVisible = true;
    private bool detectorLabelsVisible = true;

    private void Awake()
    {
        ResolveReferences();

        if (config == null)
        {
            Debug.LogWarning("[SourceLocateScene] No SourceLocateConfig assigned; the scene draws nothing.");
            return;
        }

        if (config.LayoutJson == null)
        {
            Debug.LogWarning("[SourceLocateScene] The config has no layout json; the scene draws nothing.");
            return;
        }

        if (!SourceLocator.TryBuild(config.LayoutJson.text, out locator, out string message))
        {
            Debug.LogWarning($"[SourceLocateScene] The layout was rejected: {message}");
            return;
        }

        BuildUncertaintyViews();
        BuildDetectorLabels();
        SetEstimateVisible(false);
        SetDetectorLabelsVisible(false);
    }

    private void OnEnable()
    {
        if (subscribed)
            return;

        RadiationReceiver.OnRadiationDataReceived += HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged += HandleServerConnectionChanged;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (!subscribed)
            return;

        RadiationReceiver.OnRadiationDataReceived -= HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged -= HandleServerConnectionChanged;
        subscribed = false;

        SetEstimateVisible(false);
        SetDetectorLabelsVisible(false);
    }

    private void OnDestroy()
    {
        SetEstimateVisible(false);
    }

    private void LateUpdate()
    {
        if (locator == null)
            return;

        if (!ResolveRoomFrame())
        {
            SetEstimateVisible(false);
            SetDetectorLabelsVisible(false);
            return;
        }

        DrawDetectorPositions();

        if (!hasEstimate)
            return;

        float lerpSeconds = config.PositionLerpSeconds;
        if (lerpSeconds <= 0f)
        {
            shownRoomPoint = targetRoomPoint;
            shownRadiusMeters = targetRadiusMeters;
        }
        else
        {
            float step = Mathf.Clamp01(Time.deltaTime / lerpSeconds);
            shownRoomPoint = Vector3.Lerp(shownRoomPoint, targetRoomPoint, step);
            shownRadiusMeters = Mathf.Lerp(shownRadiusMeters, targetRadiusMeters, step);
        }

        DrawEstimate();
        SetEstimateVisible(true);
    }

    private void ResolveReferences()
    {
        if (roomCoordinateSystem == null)
            roomCoordinateSystem = FindFirstObjectByType<RoomCoordinateSystem>();

        if (markerManager == null)
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>();

        if (radiationReceiver == null)
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();
    }

    private bool ResolveRoomFrame()
    {
        if (roomCoordinateSystem == null)
            roomCoordinateSystem = FindFirstObjectByType<RoomCoordinateSystem>();

        return roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated;
    }

    private void HandleServerConnectionChanged(bool connected)
    {
        if (locator == null)
            return;

        locator.ResetTracker();
        hasEstimate = false;
        SetEstimateVisible(false);
        UpdateDetectorLabelText();
    }

    private void HandleRadiationDataReceived(Dictionary<string, float> deviceData)
    {
        if (locator == null || deviceData == null)
            return;

        if (radiationReceiver == null)
            radiationReceiver = FindFirstObjectByType<RadiationReceiver>();

        string serverTime = radiationReceiver != null ? radiationReceiver.LatestServerTime : "";

        bool estimated = locator.TryPush(serverTime, deviceData, out SourceLocator.Result result);
        UpdateDetectorLabelText();

        if (!estimated)
            return;

        Vector2 origin = config.SquareOriginFromMarkerMeters;
        targetRoomPoint = new Vector3(
            origin.x + (float)result.X * 0.01f,
            0f,
            origin.y + (float)result.Y * 0.01f);
        targetRadiusMeters = Mathf.Max(
            config.MinimumDiscRadiusMeters,
            (float)result.Radius * 0.01f);

        if (!hasEstimate)
        {
            shownRoomPoint = targetRoomPoint;
            shownRadiusMeters = targetRadiusMeters;
            hasEstimate = true;
        }
    }

    private void DrawEstimate()
    {
        Vector3 sourceRoomPoint = new Vector3(
            shownRoomPoint.x,
            config.SourceHeightMeters,
            shownRoomPoint.z);
        Vector3 discRoomPoint = new Vector3(
            shownRoomPoint.x,
            config.DiscHeightMeters,
            shownRoomPoint.z);

        // Recomputed every frame because a ROOM_ORIGIN re-alignment moves the world point
        // without the room point changing, but only re-applied when it actually moved.
        Vector3 sourceWorldPoint = roomCoordinateSystem.RoomToWorldPoint(sourceRoomPoint);
        bool moved = !hasAppliedSource ||
                     (sourceWorldPoint - appliedSourceWorldPoint).sqrMagnitude > 1e-8f;

        if (markerManager != null && moved)
        {
            if (markerManager.TryShowComputedSource(sourceWorldPoint, out string markerMessage))
            {
                appliedSourceWorldPoint = sourceWorldPoint;
                hasAppliedSource = true;
            }
            else
            {
                Debug.LogWarning($"[SourceLocateScene] {markerMessage}");
            }
        }

        Pose surfacePose = new Pose(
            roomCoordinateSystem.RoomToWorldPoint(discRoomPoint),
            roomCoordinateSystem.RoomToWorldRotation(Quaternion.identity));

        for (int index = 0; index < uncertaintyViews.Count; index++)
        {
            UncertaintyPresentation view = uncertaintyViews[index];
            if (view != null)
                view.Show(surfacePose, shownRadiusMeters, uncertaintyColor);
        }
    }

    private void DrawDetectorPositions()
    {
        Vector2 origin = config.SquareOriginFromMarkerMeters;

        for (int index = 0; index < detectorLabels.Count; index++)
        {
            TMP_Text label = detectorLabels[index];
            if (label == null)
                continue;

            Vector3 roomPoint = new Vector3(
                origin.x + (float)locator.DetectorX(index) * 0.01f,
                config.DetectorHeightMeters,
                origin.y + (float)locator.DetectorY(index) * 0.01f);
            label.transform.position = roomCoordinateSystem.RoomToWorldPoint(roomPoint);
        }

        SetDetectorLabelsVisible(true);
    }

    private void SetDetectorLabelsVisible(bool visible)
    {
        if (detectorLabelsVisible == visible)
            return;

        detectorLabelsVisible = visible;

        for (int index = 0; index < detectorLabels.Count; index++)
        {
            TMP_Text label = detectorLabels[index];
            if (label != null)
                label.gameObject.SetActive(visible);
        }
    }

    private void UpdateDetectorLabelText()
    {
        for (int index = 0; index < detectorLabels.Count; index++)
        {
            TMP_Text label = detectorLabels[index];
            if (label == null)
                continue;

            string name = locator.DetectorNames[index];
            label.text = locator.TryGetLatestRate(name, out double rate)
                ? $"{name}  {rate:0} cps"
                : name;
        }
    }

    private void BuildUncertaintyViews()
    {
        if (!MarkVisualConfig.TryLoad(out MarkVisualConfig look))
        {
            Debug.LogWarning("[SourceLocateScene] No MarkVisualConfig; the uncertainty footprint is not drawn.");
            return;
        }

        uncertaintyColor = look.UncertaintyColor;

        IReadOnlyList<UncertaintyPresentation> prefabs = look.UncertaintyPrefabs;
        if (prefabs == null)
            return;

        for (int index = 0; index < prefabs.Count; index++)
        {
            if (prefabs[index] == null)
                continue;

            UncertaintyPresentation spawned = Instantiate(prefabs[index], transform, false);
            spawned.name = $"Uncertainty{index}_{prefabs[index].name}";
            uncertaintyViews.Add(spawned);
        }

        if (uncertaintyViews.Count == 0)
            Debug.LogWarning("[SourceLocateScene] MarkVisualConfig lists no uncertainty prefab.");
    }

    private void BuildDetectorLabels()
    {
        for (int index = 0; index < locator.DetectorNames.Count; index++)
        {
            GameObject labelObject = new GameObject($"DetectorLabel_{locator.DetectorNames[index]}");
            labelObject.transform.SetParent(transform, false);

            TMP_Text label = labelObject.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = LabelFontSize;
            label.text = locator.DetectorNames[index];
            label.color = config.LabelColor;

            float scale = config.LabelHeightMeters / LabelFontSize * 10f;
            labelObject.transform.localScale = new Vector3(scale, scale, scale);
            labelObject.AddComponent<FaceCamera>();

            detectorLabels.Add(label);
        }
    }

    private void SetEstimateVisible(bool visible)
    {
        if (estimateVisible == visible)
            return;

        estimateVisible = visible;

        for (int index = 0; index < uncertaintyViews.Count; index++)
        {
            UncertaintyPresentation view = uncertaintyViews[index];
            if (view != null)
                view.gameObject.SetActive(visible);
        }

        if (visible)
            return;

        // The marker keeps its world pose while hidden, so the next show has to move it again.
        hasAppliedSource = false;
        if (markerManager != null)
            markerManager.HideComputedSource();
    }
}
