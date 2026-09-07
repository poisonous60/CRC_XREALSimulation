using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Preview-center detector placement flow.
/// QR scan selects detectorId, then marker preview follows the glasses/camera center
/// until PlaceDetector() is called. The placed marker keeps the same fixed size and
/// updates the center color by CPS and draws faint inverse-square falloff shells.
/// </summary>
public partial class DetectorWorldMarkerManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Usually empty. Uses Main Camera automatically.")]
    [SerializeField] private Transform placementOrigin;

    [Tooltip("Used only when Placement Origin is empty.")]
    [SerializeField] private Camera fallbackCamera;

    [Tooltip("Optional. If empty, the script creates a sphere automatically.")]
    [SerializeField] private GameObject markerPrefab;

    [Header("Source Marker")]
    [Tooltip("Key of the one Source Marker. A sticker scan or Add Source starts a Placement of this marker; the sticker text is never used as a key.")]
    [SerializeField] private string sourceMarkerKey = "SOURCE";

    [Header("Plane Intersection Placement")]
    [Tooltip("ON = place the preview at the intersection between the glasses' center gaze ray and a detected AR plane.")]
    [SerializeField] private bool usePlaneIntersectionPlacement = true;

    [Tooltip("ARPlaneManager on the XR Origin. It is found and enabled automatically when empty.")]
    [SerializeField] private ARPlaneManager planeManager;

    [Tooltip("Plane types to detect. XREAL supports horizontal and vertical planes.")]
    [SerializeField] private PlaneDetectionMode planeDetectionMode =
        PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

    [Tooltip("Minimum gaze-ray distance accepted as a placement point.")]
    [SerializeField, Min(0f)] private float minPlaneHitDistanceMeters = 0.15f;

    [Tooltip("Maximum gaze-ray distance searched for a placement point.")]
    [SerializeField, Min(0.1f)] private float maxPlaneHitDistanceMeters = 10f;

    [Tooltip("Hide the detector preview until the center gaze ray intersects a detected plane polygon.")]
    [SerializeField] private bool hidePreviewWithoutPlaneHit = true;

    [SerializeField] private string waitingForPlaneStateLabel = "aim at a detected plane";

    [Header("Coordinate Storage")]
    [SerializeField] private bool useCoordinateDatabase = true;
    [SerializeField] private DetectorCoordinateDatabase coordinateDatabase;
    [SerializeField] private bool loadSavedCoordinatesOnStart = true;

    [Tooltip("Create a ROOM_ORIGIN QR reference frame for stable detector coordinates across Unity sessions.")]
    [SerializeField] private bool enableRoomCoordinateSystem = true;

    [Tooltip("Optional. Created on this GameObject automatically when empty.")]
    [SerializeField] private RoomCoordinateSystem roomCoordinateSystem;

    [Tooltip("Require ROOM_ORIGIN calibration before accepting detector QR placement. This prevents session-space coordinates from entering the room database.")]
    [SerializeField] private bool requireRoomCalibrationBeforeDetectorPlacement = true;

    [Header("Spatial Anchor Storage")]
    [SerializeField] private bool useSpatialAnchors = false;
    [SerializeField] private DetectorSpatialAnchorManager spatialAnchorManager;
    [SerializeField] private bool createSpatialAnchorOnQr = false;
    [SerializeField] private bool parentMarkerToAnchor = true;

    [Header("Server Visibility")]
    [Tooltip("Keep every restored and placed detector hidden until the WebSocket server is connected.")]
    [SerializeField] private bool hideMarkersUntilServerConnected = true;

    [Tooltip("Hide a placed detector until its ID exists in a recent complete CPS snapshot. This prevents saved values from looking live after reconnects or per-device dropouts.")]
    [SerializeField] private bool hideMarkersWithoutFreshRadiationData = true;

    [SerializeField, Min(0.5f)] private float maximumRadiationSnapshotAgeSeconds = 5f;

    [Tooltip("Optional. Found automatically when empty.")]
    [SerializeField] private RadiationReceiver radiationReceiver;

    [Tooltip("With multiple placed detectors, Cancel removes only the marker closest to the gaze center within this angle.")]
    [SerializeField, Range(1f, 45f)] private float cancelSelectionMaxAngleDegrees = 12f;

    [Header("Preview-Center Placement")]
    [Tooltip("ON = use camera/glasses center. OFF = use ZXing QR image center.")]
    [SerializeField] private bool usePreviewCenterPlacement = true;

    [Tooltip("Distance from glasses/camera to detector preview and final placed detector. Try 1.5 ~ 2.5m if too close.")]
    [SerializeField] private float defaultPlacementDistanceMeters = 2.0f;

    [Tooltip("ON = after QR scan, the marker preview follows the glasses/camera center until PlaceDetector() is called.")]
    [SerializeField] private bool followPreviewCenterUntilPlaced = true;

    [Tooltip("Label text shown while the detector preview is following the glasses/camera center.")]
    [SerializeField] private string followingStateLabel = "following gaze";

    [Tooltip("Label text shown after PlaceDetector() fixes the detector in world space.")]
    [SerializeField] private string placedStateLabel = "placed";

    [Tooltip("Optional vertical offset while previewing/placing. Usually 0.")]
    [SerializeField] private float markerVerticalOffsetMeters = 0f;

    [Header("Placement Preview Visual")]
    [Tooltip("ON = the detector preview always renders as a neutral gray sphere until PlaceDetector() fixes it, whatever the CPS reading is. Risk colors start only after placement.")]
    [SerializeField] private bool showGrayPreviewSphere = true;

    [Tooltip("Color of the preview sphere shown before placement.")]
    [SerializeField] private Color previewSphereColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Tooltip("Opacity of the preview sphere. Keep it above Marker Alpha so the preview stays easy to see while aiming.")]
    [SerializeField, Range(0.05f, 1f)] private float previewSphereAlpha = 0.35f;

    [Tooltip("ON = the preview sphere stays visible before the WebSocket server is connected, even while placed detectors are still hidden.")]
    [SerializeField] private bool showPreviewBeforeServerConnected = true;

    [Tooltip("Approximate Beam Pro rear camera horizontal FOV. Used only when not using pure center placement.")]
    [SerializeField] private float cameraHorizontalFovDegrees = 70f;

    [Tooltip("Approximate Beam Pro rear camera vertical FOV. Used only when not using pure center placement.")]
    [SerializeField] private float cameraVerticalFovDegrees = 50f;

    [Tooltip("OFF recommended for current preview-center flow. If ON, QR size can override default distance.")]
    [SerializeField] private bool useQrSizeToEstimateDistance = false;

    [Tooltip("Physical side length of printed QR code in meters. Example: 8cm = 0.08")]
    [SerializeField] private float realQrSizeMeters = 0.08f;

    [Tooltip("ZXing result points can be inside the QR, not full printed square.")]
    [SerializeField, Range(0.4f, 1.2f)] private float qrEffectiveSizeRatio = 0.70f;

    [SerializeField, Range(0.3f, 3.0f)] private float distanceCalibrationMultiplier = 1.0f;
    [SerializeField] private float minEstimatedDistanceMeters = 0.3f;
    [SerializeField] private float maxEstimatedDistanceMeters = 5.0f;

    [Header("Rescan Behavior")]
    [SerializeField] private bool updateExistingMarkerOnRescan = true;
    [SerializeField] private bool smoothPositionOnRescan = false;
    [SerializeField, Range(0.05f, 1.0f)] private float rescanPositionBlend = 0.55f;

    [Header("Marker Visual")]
    [Tooltip("Fixed world scale of every detector sphere. Radiation value changes color only, not size.")]
    [SerializeField, Min(0.001f)] private float fixedMarkerSize = 0.20f;

    [Tooltip("Opacity of the line-free center detector sphere.")]
    [SerializeField, Range(0.02f, 0.8f)] private float markerAlpha = 0.18f;

    [Tooltip("Use the included line-free transparent volume shader for consistent XREAL rendering.")]
    [SerializeField] private bool forceDedicatedTransparentShader = true;

    [Header("AR Glasses HUD")]
    [Tooltip("Creates a head-locked server/device HUD and off-screen detector arrows automatically.")]
    [SerializeField] private bool enableArGlassesHud = true;

    [Tooltip("Optional existing HUD. When empty, one is created automatically at runtime.")]
    [SerializeField] private ARDetectorHud arGlassesHud;

    [Header("Marker Label")]
    [SerializeField] private bool showLabel = false;
    [SerializeField] private bool showDistanceInLabel = true;
    [SerializeField] private bool showAnchorStateInLabel = true;

    [Tooltip("Camera-relative world offset from the sphere center. Positive X is screen-right and negative Y is screen-down.")]
    [SerializeField] private Vector3 labelCameraOffsetMeters = new Vector3(0.16f, -0.13f, -0.01f);

    [Tooltip("World-space scale of the label. This is kept independent of Fixed Marker Size.")]
    [SerializeField, Min(0.001f)] private float labelWorldScale = 0.04f;

    [SerializeField, Min(0.1f)] private float labelFontSize = 4.5f;
    [SerializeField, Range(0f, 1f)] private float labelOutlineWidth = 0.2f;
    [SerializeField] private Color labelOutlineColor = Color.black;

    [Header("Radiation Thresholds")]
    [Tooltip("CPS values at or below this value hide the center sphere completely.")]
    [SerializeField, Min(0f)] private float hiddenMaxCps = 2f;

    [Tooltip("CPS values above Hidden Max and at or below this value are green.")]
    [SerializeField, Min(0f)] private float greenMaxCps = 10f;

    [Tooltip("CPS values strictly above this value are red. Exactly 350 CPS remains yellow.")]
    [SerializeField, Min(0f)] private float dangerThresholdCps = 350f;

    [Header("Controller Detector Reposition")]
    [Tooltip("Allow a placed, currently visible detector to be highlighted and repositioned with the Beam Pro controller ray and center pad.")]
    [SerializeField] private bool enableControllerDetectorReposition = true;

    [Tooltip("Optional controller input bridge. It is created on this GameObject automatically when empty.")]
    [SerializeField] private DetectorControllerInteractor detectorControllerInteractor;

    [Tooltip("Maximum controller-ray distance at which a detector can be selected.")]
    [SerializeField, Min(0.5f)] private float controllerSelectionMaxDistanceMeters = 10f;

    [Tooltip("Small angular allowance around the controller ray, useful for distant 20 cm detector spheres.")]
    [SerializeField, Range(0.25f, 8f)] private float controllerSelectionAngleDegrees = 1.5f;

    [Tooltip("Multiplier applied to the visible center-sphere radius for controller hit testing.")]
    [SerializeField, Range(1f, 2f)] private float controllerSelectionRadiusMultiplier = 1.25f;

    [Tooltip("How much a hovered detector center color blends toward white.")]
    [SerializeField, Range(0f, 0.6f)] private float controllerHoverHighlightBlend = 0.20f;

    [Tooltip("Additional center-sphere opacity while a detector is hovered.")]
    [SerializeField, Range(0f, 0.5f)] private float controllerHoverAlphaBoost = 0.08f;

    [Tooltip("How much a detector being moved blends toward white.")]
    [SerializeField, Range(0f, 0.8f)] private float controllerMoveHighlightBlend = 0.32f;

    [Tooltip("Additional center-sphere opacity while a detector is being moved.")]
    [SerializeField, Range(0f, 0.6f)] private float controllerMoveAlphaBoost = 0.14f;

    private readonly Dictionary<string, SourceMarker> markers =
        new Dictionary<string, SourceMarker>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> sortedHudDetectorIds = new List<string>();

    private bool spatialEventsSubscribed = false;
    private bool warnedAboutMissingPlaneManager = false;
    private string currentFollowingDetectorId = "";
    private PlacementSession activePlacementSession;
    // PlaceDetector clears currentFollowingDetectorId after the preview is fixed.
    // Keep the most recently committed detector so Cancel Place can also remove
    // that exact sphere immediately after it has been placed.
    private string lastInteractedDetectorId = "";
    private Shader cachedDetectorTransparentShader;




    private SourceMarker controllerHoveredMarker;
    private DetectorMoveSession activeDetectorMoveSession;
    private void OnEnable()
    {
        radiationSnapshot.Reset();

        if (MarkVisualConfig.TryLoad(out MarkVisualConfig presentationConfig))
        {
            fixedMarkerSize = presentationConfig.MarkerSizeMeters;
            showLabel = presentationConfig.ShowLabel;
        }

        fixedMarkerSize = Mathf.Max(0.001f, fixedMarkerSize);

        QRScanner.OnScanStarted += NotifyQrScanStarted;
        QRScanner.OnQRDetectedDetailed += HandleQrDetected;
        RadiationReceiver.OnRadiationDataReceived += HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged += HandleServerConnectionChanged;
        EnsureSpatialAnchorManager();
        SubscribeSpatialEvents();
    }

    private void OnDisable()
    {
        AbortControllerDetectorInteraction(true);
        QRScanner.OnScanStarted -= NotifyQrScanStarted;
        QRScanner.OnQRDetectedDetailed -= HandleQrDetected;
        RadiationReceiver.OnRadiationDataReceived -= HandleRadiationDataReceived;
        RadiationReceiver.OnServerConnectionChanged -= HandleServerConnectionChanged;
        UnsubscribeSpatialEvents();
    }

    private void OnDestroy()
    {
        AbortControllerDetectorInteraction(false);
        foreach (var pair in markers)
            DestroyMarkerVisualResources(pair.Value);
    }

    public bool HasActivePlacement =>
        activePlacementSession != null || !string.IsNullOrEmpty(currentFollowingDetectorId);

    public bool HasActiveDetectorMove => activeDetectorMoveSession != null;

    public bool RequiresRoomCalibration =>
        enableRoomCoordinateSystem && requireRoomCalibrationBeforeDetectorPlacement;

    // Engine code stripping keeps only referenced component types; GameObject.CreatePrimitive needs these four in the Android build.
    private MeshFilter StrippingGuardMeshFilter { get; set; }
    private MeshRenderer StrippingGuardMeshRenderer { get; set; }
    private SphereCollider StrippingGuardSphereCollider { get; set; }
    private BoxCollider StrippingGuardBoxCollider { get; set; }

    private bool IsRoomReadyForPlacement()
    {
        return !RequiresRoomCalibration ||
               (roomCoordinateSystem != null && roomCoordinateSystem.IsCalibrated);
    }

    public string HoveredDetectorId =>
        controllerHoveredMarker != null ? controllerHoveredMarker.detectorId : "";

    public string ActiveMovedDetectorId =>
        activeDetectorMoveSession != null && activeDetectorMoveSession.marker != null
            ? activeDetectorMoveSession.marker.detectorId
            : "";

    /// <summary>
    /// Detector currently owned by the shared Place/Cancel controls.
    /// Exposed for the Beam Pro workflow guide only; placement remains owned here.
    /// </summary>
    public string ActivePlacementDetectorId =>
        activePlacementSession != null
            ? activePlacementSession.detectorId
            : currentFollowingDetectorId;

    /// <summary>
    /// True when the active detector preview can be committed at its current pose.
    /// In plane-intersection mode this becomes true only while the center gaze ray
    /// hits a tracked plane.
    /// </summary>
    public bool HasValidActivePlacementPose
    {
        get
        {
            string detectorId = NormalizeDetectorId(ActivePlacementDetectorId);
            if (string.IsNullOrEmpty(detectorId) ||
                !markers.TryGetValue(detectorId, out SourceMarker marker) ||
                marker == null || marker.root == null)
            {
                return false;
            }

            return !usePlaneIntersectionPlacement || marker.hasValidPlaneHit;
        }
    }

    /// <summary>
    /// Number of committed detector visuals materialized in the currently
    /// localized room. Hidden-by-CPS/server markers are still counted.
    /// </summary>
    public int CurrentRoomPlacedDetectorCount
    {
        get
        {
            int count = 0;
            foreach (var pair in markers)
            {
                SourceMarker marker = pair.Value;
                if (marker != null && marker.root != null && marker.isPlaced)
                    count++;
            }

            return count;
        }
    }

    public void NotifyQrScanStarted()
    {
        AbortControllerDetectorInteraction(true);
        // Starting a new scan invalidates the old one-step delete token. Cancel
        // during camera startup must stop the scan, not delete a previous detector.
        lastInteractedDetectorId = "";
    }

    private void Start()
    {
        EnsurePlacementOrigin();
        EnsurePlaneDetectionManager();
        EnsureCoordinateDatabase();
        RemoveRecordsOutsideSourceKey();
        EnsureRoomCoordinateSystem();
        EnsureSpatialAnchorManager();
        EnsureRadiationReceiver();
        EnsureDetectorControllerInteractor();
        SubscribeSpatialEvents();
        EnsureArGlassesHud();
        InitializePlacedDetectorOrderFromDatabase();

        SetServerConnectionState(radiationReceiver != null && radiationReceiver.IsConnected);
        if (radiationReceiver != null &&
            radiationReceiver.IsConnected &&
            radiationReceiver.HasFreshRadiationData)
        {
            HandleRadiationDataReceived(radiationReceiver.LatestDeviceData);
        }

        if (loadSavedCoordinatesOnStart)
            LoadSavedCoordinatesWithoutAnchors();
    }

    private void LateUpdate()
    {
        RefreshRadiationSnapshotVisibilityIfExpired();
        EnsurePlacementOrigin();
        UpdateFollowingMarkerPosition();

        if (!showLabel || fallbackCamera == null)
            return;

        foreach (var kvp in markers)
        {
            UpdateLabelTransform(kvp.Value);
        }
    }

    private void OnValidate()
    {
        maximumRadiationSnapshotAgeSeconds =
            Mathf.Max(0.5f, maximumRadiationSnapshotAgeSeconds);
        controllerSelectionMaxDistanceMeters =
            Mathf.Max(0.5f, controllerSelectionMaxDistanceMeters);
        controllerSelectionAngleDegrees =
            Mathf.Clamp(controllerSelectionAngleDegrees, 0.25f, 8f);
        controllerSelectionRadiusMultiplier =
            Mathf.Clamp(controllerSelectionRadiusMultiplier, 1f, 2f);
    }
}
