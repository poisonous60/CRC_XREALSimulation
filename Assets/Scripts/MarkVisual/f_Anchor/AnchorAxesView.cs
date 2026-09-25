using UnityEngine;

/// <summary>Frame anchor debug look: XREAL's sample XYZ axes on the room frame anchor, drawn only while the Settings checkbox is on.</summary>
[DisallowMultipleComponent]
public class AnchorAxesView : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Holds the Settings checkbox that shows the axes. Empty never shows them.")]
    [SerializeField] private AnchorAxesConfig config;

    private Renderer[] axisRenderers;
    private Transform anchor;
    private bool shown;

    public AnchorAxesConfig Config => config;

    private void Awake()
    {
        axisRenderers = GetComponentsInChildren<Renderer>(true);

        for (int index = 0; index < axisRenderers.Length; index++)
            axisRenderers[index].enabled = false;
    }

    private void OnEnable()
    {
        RoomFrameAnchor.AnchorChanged += HandleAnchorChanged;
    }

    private void OnDisable()
    {
        RoomFrameAnchor.AnchorChanged -= HandleAnchorChanged;
    }

    private void LateUpdate()
    {
        bool visible = anchor != null && config != null && config.ShowAxes;

        if (visible)
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        if (visible == shown)
            return;

        shown = visible;

        for (int index = 0; index < axisRenderers.Length; index++)
            axisRenderers[index].enabled = visible;
    }

    private void HandleAnchorChanged(Transform value)
    {
        anchor = value;
    }
}
