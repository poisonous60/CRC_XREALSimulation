using UnityEngine;

/// <summary>
/// Editor test aid: puts the Source marker at a fixed spot and feeds it the CPS set on this component, so a look can be checked across readings without a server.
/// </summary>
[DisallowMultipleComponent]
public class CpsSliderProbe : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Marker manager that owns the Source marker. Empty finds the one in the scene.")]
    [SerializeField] private DetectorWorldMarkerManager markerManager;

    [Tooltip("World position the Source marker is shown at.")]
    [SerializeField] private Vector3 sourcePosition = new Vector3(0f, 1.6f, 3f);

    [Header("Reading")]
    [Tooltip("CPS handed to the Source marker. Drag it during Play.")]
    [SerializeField, Range(0f, 1000f)] private float cps = 5f;

    private bool applied;
    private float appliedCps;
    private Vector3 appliedPosition;
    private bool reportedFailure;

    private void Update()
    {
        if (markerManager == null)
        {
            markerManager = FindFirstObjectByType<DetectorWorldMarkerManager>();

            if (markerManager == null)
                return;
        }

        if (applied && Mathf.Approximately(appliedCps, cps) && appliedPosition == sourcePosition)
            return;

        if (!markerManager.TryShowComputedSourceAt("", sourcePosition, cps, out string resultMessage))
        {
            if (!reportedFailure)
                Debug.LogWarning($"[CpsSliderProbe] {resultMessage}");

            reportedFailure = true;
            return;
        }

        applied = true;
        appliedCps = cps;
        appliedPosition = sourcePosition;
    }
}
