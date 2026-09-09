using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Head-locked AR glasses HUD for server status, detector readings, gaze highlighting,
/// and four-direction off-screen indicators. It builds its own world-space canvas so no
/// Inspector UI references are required.
/// </summary>
[DisallowMultipleComponent]
public partial class ARDetectorHud : MonoBehaviour
{
    private const float ReferenceWidth = 1920f;
    private const float ReferenceHeight = 1080f;

    [Header("References")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;
    [SerializeField] private RadiationReceiver radiationReceiver;
    [SerializeField] private Camera targetCamera;

    [Header("XREAL Viewport Layout")]
    [Tooltip("Distance of the head-locked canvas from the center eye in meters.")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 1.5f;

    [Tooltip("Normalized bottom-right anchor. 0.94/0.06 keeps text inside the 46-52 degree XREAL display edge.")]
    [SerializeField] private Vector2 hudViewportAnchor = new Vector2(0.94f, 0.06f);

    [SerializeField] private Vector2 hudPixelSize = new Vector2(720f, 430f);
    [SerializeField, Min(10f)] private float hudFontSize = 27f;
    [SerializeField] private string radiationUnit = "CPS";

    [Tooltip("Maximum detector rows kept inside the fixed XREAL HUD. Extra rows are summarized.")]
    [SerializeField, Min(1)] private int maxVisibleDetectorRows = 9;

    [Header("Gaze Selection")]
    [Tooltip("A placed sphere is selected when it is this close to the view-center ray.")]
    [SerializeField, Range(1f, 20f)] private float gazeSelectionAngleDegrees = 8f;

    [Header("Off-screen Indicators")]
    [SerializeField, Range(0f, 0.15f)] private float screenEdgeMargin = 0.05f;

    private readonly Dictionary<string, float> latestDeviceData =
        new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> sortedDisplayDetectorIds = new List<string>();
    private readonly HashSet<string> displayDetectorIdSet =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> markerIndexByDetectorId =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private readonly List<DetectorWorldMarkerManager.DetectorHudMarkerState> markerStates =
        new List<DetectorWorldMarkerManager.DetectorHudMarkerState>();
    private readonly StringBuilder textBuilder = new StringBuilder(512);

    private GameObject canvasObject;
    private RectTransform canvasRect;
    private Canvas canvas;
    private TMP_Text hudText;
    private RectTransform indicatorLayer;
    private bool eventsSubscribed;
    private string serverStatus = "Disconnected";
    private Color serverStatusColor = Color.red;

    public void Initialize(DetectorWorldMarkerManager manager, Camera camera)
    {
        if (manager != null)
            markerManager = manager;

        if (camera != null)
            targetCamera = camera;

        EnsureReferences();
        PullCurrentReceiverState();
        EnsureCanvas();
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        PullCurrentReceiverState();
    }

    private void Start()
    {
        EnsureReferences();
        PullCurrentReceiverState();
        EnsureCanvas();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();

        if (canvasObject != null)
            Destroy(canvasObject);
    }

    private void LateUpdate()
    {
        EnsureReferences();
        EnsureCanvas();

        if (targetCamera == null || canvasRect == null || markerManager == null)
            return;

        UpdateCanvasPoseFromRuntimeProjection();

        markerManager.FillHudMarkerStates(markerStates);
        int selectedMarkerIndex = FindGazeSelectedMarker();

        UpdateHudText(selectedMarkerIndex);
        UpdateOffscreenIndicators();
    }
}
