using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bridge between the Beam Pro / XREAL virtual-controller UI and scene managers.
/// Attach this to a GameObject inside the XREALVirtualController_custom prefab or
/// to a scene object referenced by the controller buttons.
///
/// Button OnClick examples:
/// - BeamProControllerBridge.ConnectToServer()
/// - BeamProControllerBridge.StartQrScan()
/// - BeamProControllerBridge.StopQrScan()
/// - BeamProControllerBridge.PlaceDetector()
/// - BeamProControllerBridge.CancelPlace()
/// </summary>
public partial class BeamProControllerBridge : MonoBehaviour
{
    private const string WaitingForDataMessage = "Waiting for radiation data...";
    private const float WorkflowRefreshIntervalSeconds = 0.10f;
    private const float ActionStatusLifetimeSeconds = 4f;

    [Header("Scene Managers")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;
    [SerializeField] private RoomCoordinateSystem roomCoordinateSystem;
    [SerializeField] private QRScanner qrScanner;
    [SerializeField] private RadiationReceiver radiationReceiver;

    [SerializeField] private XREALCaptureManager captureManager;
    [Header("Controller UI")]
    [Tooltip("Optional Beam Pro-side IP input field. If assigned, ConnectToServer() uses this value.")]
    [SerializeField] private TMP_InputField controllerIpInputField;

    [Tooltip("Optional Beam Pro-side status text.")]
    [SerializeField] private TMP_Text controllerStatusText;

    [Tooltip("Beam Pro-side radiation data text.")]
    [SerializeField] private TMP_Text controllerRadiationDisplayText;
    [Tooltip("Beam Pro-side capture status text.")]
    [SerializeField] private TMP_Text controllerCaptureStatusText;

    [Tooltip("Text child of the Start/Stop Record button.")]
    [SerializeField] private TMP_Text controllerRecordButtonText;

    [Header("Workflow Controls")]
    [Tooltip("Optional. Auto-found by the SourceActionButton name when empty.")]
    [SerializeField] private Button controllerSourceActionButton;

    [SerializeField] private TMP_Text controllerSourceActionButtonText;

    [Header("Detector List Text")]
    [SerializeField, Min(6f)] private float detectorListMinFontSize = 10f;
    [SerializeField, Min(10f)] private float detectorListMaxFontSize = 40f;

    [Header("Workflow Guide Text")]
    [SerializeField, Min(8f)] private float workflowGuideMinFontSize = 24f;
    [SerializeField, Min(12f)] private float workflowGuideMaxFontSize = 44f;
    [SerializeField, Min(6f)] private float workflowButtonMinFontSize = 10f;
    [SerializeField, Min(10f)] private float workflowButtonMaxFontSize = 20f;
    [Tooltip("Inset of the button label inside its rect: left, top, right, bottom. Detector rows copy it.")]
    [SerializeField] private Vector4 workflowButtonMargin = new Vector4(3f, 1f, 3f, 1f);

    [Header("Behavior")]
    [SerializeField] private bool autoFindReferences = true;
    [SerializeField] private bool logActions = true;

    private Color latestServerStatusColor = Color.white;
    private string transientActionMessage = "";
    private Color transientActionColor = Color.white;
    private float transientActionExpiresAt = float.NegativeInfinity;
    private float nextWorkflowRefreshTime;
    private float nextReferenceResolveTime;
    private bool workflowUiDirty = true;
    private bool receivedRadiationThisConnection;
    private string lastRenderedWorkflowText = "";

    private void Awake()
    {
        ResolveReferences();
        ResolveControllerControls();
        ConfigureControllerDetectorList();
        ConfigureControllerWorkflowGuide();
        ConfigureWorkflowButtonLabels();
        SyncControllerIpField();
        RefreshWorkflowUi(true);
    }

    private void OnEnable()
    {
        ResolveReferences();
        ResolveControllerControls();
        ConfigureControllerDetectorList();
        ConfigureControllerWorkflowGuide();
        ConfigureWorkflowButtonLabels();
        RadiationReceiver.OnServerStatusChanged += HandleServerStatusChanged;
        RadiationReceiver.OnDisplayTextChanged += HandleDisplayTextChanged;
        RadiationReceiver.OnServerConnectionChanged += HandleServerConnectionChanged;
        RadiationReceiver.OnRadiationDataFreshnessChanged += HandleRadiationDataFreshnessChanged;
        RadiationReceiver.OnRadiationDataReceived += HandleRadiationDataReceived;
        RoomCoordinateSystem.RoomStatusChanged += HandleRoomStatusChanged;
        XREALCaptureManager.OnCaptureStateChanged += HandleCaptureStateChanged;
        RegisterControllerIpListener();
        SyncControllerIpField();
        SyncCurrentReceiverText();

        receivedRadiationThisConnection =
            radiationReceiver != null &&
            radiationReceiver.IsConnected &&
            radiationReceiver.HasFreshRadiationData;
        ConfigureControllerDetectorList();
        ConfigureControllerWorkflowGuide();
        ConfigureWorkflowButtonLabels();
        workflowUiDirty = true;
        nextWorkflowRefreshTime = 0f;
        nextReferenceResolveTime = 0f;
        RefreshWorkflowUi(true);
    }

    private void LateUpdate()
    {
        float now = Time.unscaledTime;
        if (now >= nextReferenceResolveTime)
        {
            nextReferenceResolveTime = now + 1f;
            ResolveReferences();
            ResolveControllerControls();
        }

        if (workflowUiDirty || now >= nextWorkflowRefreshTime)
            RefreshWorkflowUi(false);
    }

    private void OnDisable()
    {
        RadiationReceiver.OnServerStatusChanged -= HandleServerStatusChanged;
        RadiationReceiver.OnDisplayTextChanged -= HandleDisplayTextChanged;
        RadiationReceiver.OnServerConnectionChanged -= HandleServerConnectionChanged;
        RadiationReceiver.OnRadiationDataFreshnessChanged -= HandleRadiationDataFreshnessChanged;
        RadiationReceiver.OnRadiationDataReceived -= HandleRadiationDataReceived;
        RoomCoordinateSystem.RoomStatusChanged -= HandleRoomStatusChanged;
        XREALCaptureManager.OnCaptureStateChanged -= HandleCaptureStateChanged;
        if (controllerIpInputField != null)
            controllerIpInputField.onEndEdit.RemoveListener(HandleControllerIpEndEdit);
    }

    /// <summary>
    /// Manually refresh manager references. Useful if objects are created after the controller prefab.
    /// </summary>
    public void RefreshReferences()
    {
        ResolveReferences();
        ResolveControllerControls();
        ConfigureControllerWorkflowGuide();
        ConfigureWorkflowButtonLabels();
        SyncControllerIpField();
        SyncCurrentReceiverText();
        RefreshWorkflowUi(true);
    }

    private void Log(string message)
    {
        if (logActions)
            Debug.Log($"[BeamProControllerBridge] {message}");
    }

    private void Warn(string message)
    {
        ShowControllerActionStatus(message, Color.yellow);
        Debug.LogWarning($"[BeamProControllerBridge] {message}");
    }
}
