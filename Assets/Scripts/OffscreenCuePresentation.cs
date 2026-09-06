using UnityEngine;

/// <summary>
/// Holds a child visual at the view edge, pointed at the source, while the source is off screen.
/// </summary>
[DisallowMultipleComponent]
public class OffscreenCuePresentation : SourcePresentation
{
    [Header("Visual")]
    [Tooltip("Object moved to the view edge. Empty means the first child.")]
    [SerializeField] private Transform cue;

    [Tooltip("Local axis of the cue that should point at the source.")]
    [SerializeField] private Vector3 cueForwardAxis = Vector3.up;

    [Header("Placement")]
    [Tooltip("Distance of the cue from the head, in meters.")]
    [SerializeField, Min(0.1f)] private float cueDistanceMeters = 1.5f;

    [Tooltip("Inset from the view edge, as a fraction of the view. 0.06 keeps the cue off the display border.")]
    [SerializeField, Range(0f, 0.4f)] private float edgeInset = 0.06f;

    [Tooltip("Extra margin the source must re-enter before the cue hides, which stops edge flicker.")]
    [SerializeField, Range(0f, 0.2f)] private float hideHysteresis = 0.022f;

    private Transform sourceTransform;
    private Camera headCamera;
    private bool cueShown;

    public override void Bind(Transform source, Camera head)
    {
        sourceTransform = source;
        headCamera = head;

        if (cue == null && transform.childCount > 0)
            cue = transform.GetChild(0);

        if (cue == null)
            Debug.LogWarning($"[OffscreenCuePresentation] {name} has no cue child to place.");

        SetCueVisible(false);
    }

    private void LateUpdate()
    {
        if (sourceTransform == null || headCamera == null || cue == null)
            return;

        Vector3 viewportPoint = headCamera.WorldToViewportPoint(sourceTransform.position);
        Vector2 offsetFromCenter = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);

        if (viewportPoint.z <= 0f)
            offsetFromCenter = -offsetFromCenter;

        float visibleHalfExtent = 0.5f - edgeInset;
        float hideHalfExtent = visibleHalfExtent - (cueShown ? 0f : hideHysteresis);

        bool sourceIsVisible =
            viewportPoint.z > 0f &&
            Mathf.Abs(offsetFromCenter.x) <= hideHalfExtent &&
            Mathf.Abs(offsetFromCenter.y) <= hideHalfExtent;

        if (sourceIsVisible)
        {
            SetCueVisible(false);
            return;
        }

        if (offsetFromCenter.sqrMagnitude < 0.000001f)
            offsetFromCenter = Vector2.up;

        Vector2 clamped = ClampToRect(offsetFromCenter, visibleHalfExtent);

        cue.position = headCamera.ViewportToWorldPoint(
            new Vector3(clamped.x + 0.5f, clamped.y + 0.5f, cueDistanceMeters));

        float bearingDegrees = Mathf.Atan2(offsetFromCenter.y, offsetFromCenter.x) * Mathf.Rad2Deg;
        float axisDegrees = Mathf.Atan2(cueForwardAxis.y, cueForwardAxis.x) * Mathf.Rad2Deg;

        cue.rotation =
            headCamera.transform.rotation * Quaternion.Euler(0f, 0f, bearingDegrees - axisDegrees);

        SetCueVisible(true);
    }

    private static Vector2 ClampToRect(Vector2 direction, float halfExtent)
    {
        float scaleX = Mathf.Abs(direction.x) > Mathf.Epsilon
            ? halfExtent / Mathf.Abs(direction.x)
            : float.PositiveInfinity;

        float scaleY = Mathf.Abs(direction.y) > Mathf.Epsilon
            ? halfExtent / Mathf.Abs(direction.y)
            : float.PositiveInfinity;

        return direction * Mathf.Min(scaleX, scaleY);
    }

    private void SetCueVisible(bool visible)
    {
        if (cueShown == visible)
            return;

        cueShown = visible;

        if (cue != null)
            cue.gameObject.SetActive(visible);
    }
}
