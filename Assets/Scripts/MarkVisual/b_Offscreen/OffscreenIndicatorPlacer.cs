using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Marks where an off-screen marker is: which edge it left by, and where on that edge to sit.
/// </summary>
public class OffscreenIndicatorPlacer
{
    private struct OffscreenState
    {
        public DetectorWorldMarkerManager.DetectorHudMarkerState marker;
        public Vector3 viewport;
        public OffscreenEdge direction;
    }

    private readonly List<OffscreenState> offscreenStates = new List<OffscreenState>();
    private readonly List<OffscreenIndicatorView> indicatorPool = new List<OffscreenIndicatorView>();

    private Camera targetCamera;
    private RectTransform indicatorLayer;
    private float screenEdgeMargin = 0.05f;
    private float hudAvoidanceTop = 0.15f;
    private float hudAvoidanceLeft = 0.85f;

    public void Configure(
        Camera camera,
        RectTransform layer,
        float edgeMargin,
        float avoidanceTop,
        float avoidanceLeft)
    {
        targetCamera = camera;
        indicatorLayer = layer;
        screenEdgeMargin = edgeMargin;
        hudAvoidanceTop = avoidanceTop;
        hudAvoidanceLeft = avoidanceLeft;
    }

    public void Refresh(List<DetectorWorldMarkerManager.DetectorHudMarkerState> markerStates)
    {
        offscreenStates.Clear();

        if (targetCamera == null || indicatorLayer == null || markerStates == null)
            return;

        for (int i = 0; i < markerStates.Count; i++)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(markerStates[i].worldPosition);
            bool inside = viewport.z > 0f &&
                          viewport.x >= screenEdgeMargin && viewport.x <= 1f - screenEdgeMargin &&
                          viewport.y >= screenEdgeMargin && viewport.y <= 1f - screenEdgeMargin;

            if (inside)
                continue;

            OffscreenEdge direction = GetCardinalDirection(markerStates[i].worldPosition, viewport);
            offscreenStates.Add(new OffscreenState
            {
                marker = markerStates[i],
                viewport = viewport,
                direction = direction
            });
        }

        EnsurePoolSize(offscreenStates.Count);

        int leftCount = 0;
        int rightCount = 0;
        int upCount = 0;
        int downCount = 0;

        for (int i = 0; i < indicatorPool.Count; i++)
        {
            bool active = i < offscreenStates.Count;
            indicatorPool[i].gameObject.SetActive(active);
            if (!active)
                continue;

            OffscreenState state = offscreenStates[i];
            int stackIndex;

            switch (state.direction)
            {
                case OffscreenEdge.Left:
                    stackIndex = leftCount++;
                    break;
                case OffscreenEdge.Right:
                    stackIndex = rightCount++;
                    break;
                case OffscreenEdge.Up:
                    stackIndex = upCount++;
                    break;
                default:
                    stackIndex = downCount++;
                    break;
            }

            ConfigureIndicator(indicatorPool[i], state, stackIndex);
        }
    }

    private OffscreenEdge GetCardinalDirection(Vector3 worldPosition, Vector3 viewport)
    {
        if (viewport.z > 0f)
        {
            float horizontalOverflow = Mathf.Abs(viewport.x - 0.5f) / 0.5f;
            float verticalOverflow = Mathf.Abs(viewport.y - 0.5f) / 0.5f;

            if (horizontalOverflow >= verticalOverflow)
                return viewport.x < 0.5f ? OffscreenEdge.Left : OffscreenEdge.Right;

            return viewport.y < 0.5f ? OffscreenEdge.Down : OffscreenEdge.Up;
        }

        Vector3 local = targetCamera.transform.InverseTransformPoint(worldPosition);
        float horizontalAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float verticalAngle = Mathf.Atan2(local.y, Mathf.Max(0.0001f, Mathf.Abs(local.z))) * Mathf.Rad2Deg;

        if (Mathf.Abs(horizontalAngle) >= Mathf.Abs(verticalAngle))
            return horizontalAngle < 0f ? OffscreenEdge.Left : OffscreenEdge.Right;

        return verticalAngle < 0f ? OffscreenEdge.Down : OffscreenEdge.Up;
    }

    private void EnsurePoolSize(int count)
    {
        OffscreenIndicatorView prefab = null;

        if (MarkVisualSetting.TryLoad(out MarkVisualSetting visualSetting))
            prefab = visualSetting.OffscreenPrefab;

        while (indicatorPool.Count < count)
        {
            OffscreenIndicatorView view = prefab != null
                ? Object.Instantiate(prefab)
                : CreateDefaultIndicatorView();

            view.name = $"DetectorDirection_{indicatorPool.Count}";
            view.gameObject.layer = 5;
            ((RectTransform)view.transform).SetParent(indicatorLayer, false);
            indicatorPool.Add(view);
        }
    }

    private OffscreenIndicatorView CreateDefaultIndicatorView()
    {
        GameObject indicatorObject = new GameObject(
            "DetectorDirection",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI),
            typeof(TextOffscreenIndicatorView));

        return indicatorObject.GetComponent<TextOffscreenIndicatorView>();
    }

    private void ConfigureIndicator(OffscreenIndicatorView indicator, OffscreenState state, int stackIndex)
    {
        indicator.Show(state.marker.detectorId, state.direction, state.marker.color);

        RectTransform rect = (RectTransform)indicator.transform;

        if (indicator.UsesEllipseClamp)
        {
            PlaceOnEllipse(indicator, rect, state, stackIndex);
            return;
        }

        float stackOffset = AlternatingStackOffset(stackIndex, 0.055f);
        Vector2 anchor;

        switch (state.direction)
        {
            case OffscreenEdge.Left:
                anchor = new Vector2(
                    screenEdgeMargin,
                    Mathf.Clamp(state.viewport.z > 0f ? state.viewport.y + stackOffset : 0.5f + stackOffset, 0.15f, 0.85f));
                break;

            case OffscreenEdge.Right:
                anchor = new Vector2(
                    1f - screenEdgeMargin,
                    Mathf.Clamp(
                        state.viewport.z > 0f ? state.viewport.y + stackOffset : 0.5f + stackOffset,
                        hudAvoidanceTop,
                        0.85f));
                break;

            case OffscreenEdge.Up:
                anchor = new Vector2(
                    Mathf.Clamp(state.viewport.z > 0f ? state.viewport.x + stackOffset : 0.5f + stackOffset, 0.15f, 0.85f),
                    1f - screenEdgeMargin);
                break;

            default:
                anchor = new Vector2(
                    Mathf.Clamp(
                        state.viewport.z > 0f ? state.viewport.x + stackOffset : 0.5f + stackOffset,
                        0.15f,
                        hudAvoidanceLeft),
                    screenEdgeMargin);
                break;
        }

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
    }

    private void PlaceOnEllipse(
        OffscreenIndicatorView indicator,
        RectTransform rect,
        OffscreenState state,
        int stackIndex)
    {
        Vector2 fromCenter = new Vector2(state.viewport.x - 0.5f, state.viewport.y - 0.5f);

        if (state.viewport.z <= 0f)
            fromCenter = -fromCenter;

        if (fromCenter.sqrMagnitude < 0.000001f)
            fromCenter = new Vector2(0f, -1f);

        fromCenter.Normalize();
        fromCenter = RotateDegrees(fromCenter, AlternatingStackOffset(stackIndex, 7f));

        float semiAxis = Mathf.Max(0.01f, 0.5f - screenEdgeMargin);
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 anchor = center + fromCenter * semiAxis;

        if (anchor.x > 0.5f)
            anchor.y = Mathf.Max(anchor.y, hudAvoidanceTop);

        if (anchor.y < 0.5f)
            anchor.x = Mathf.Min(anchor.x, hudAvoidanceLeft);

        Vector2 corrected = anchor - center;
        if (corrected.sqrMagnitude > 0.000001f)
            anchor = center + corrected.normalized * semiAxis;

        anchor.x = Mathf.Clamp(anchor.x, screenEdgeMargin, 1f - screenEdgeMargin);
        anchor.y = Mathf.Clamp(anchor.y, screenEdgeMargin, 1f - screenEdgeMargin);

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;

        float aspect = targetCamera != null && targetCamera.aspect > 0f ? targetCamera.aspect : 1f;
        Vector2 screenDirection = new Vector2(fromCenter.x * aspect, fromCenter.y);
        indicator.SetDirection(Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg);
    }

    private static Vector2 RotateDegrees(Vector2 direction, float degrees)
    {
        if (Mathf.Approximately(degrees, 0f))
            return direction;

        float radians = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cos - direction.y * sin,
            direction.x * sin + direction.y * cos);
    }

    private float AlternatingStackOffset(int index, float step)
    {
        if (index <= 0)
            return 0f;

        int level = (index + 1) / 2;
        return level * step * (index % 2 == 1 ? 1f : -1f);
    }
}
