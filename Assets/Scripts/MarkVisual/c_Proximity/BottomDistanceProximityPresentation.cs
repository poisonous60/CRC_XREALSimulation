using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (c) proximity information: one shared, head-locked horizontal-distance badge for the source
/// nearest the view-center ray. Each marker receives this presentation from SourcePresentationHost,
/// but the shared renderer ensures that several markers never overlap at the bottom of the display.
/// </summary>
[DisallowMultipleComponent]
public sealed class BottomDistanceProximityPresentation : SourcePresentation
{
    private static SharedBottomDistanceHud sharedHud;

    [Header("Selection")]
    [Tooltip("A source becomes the badge target when it is within this angle of the view-center ray.")]
    [SerializeField, Range(1f, 20f)] private float selectionAngleDegrees = 8f;

    [Header("Placement")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 1.5f;
    [SerializeField] private Vector2 viewportAnchor = new Vector2(0.5f, 0.10f);

    [Header("Badge")]
    [SerializeField, Min(120f)] private float badgeWidth = 360f;
    [SerializeField, Min(36f)] private float badgeHeight = 76f;
    [SerializeField, Min(10f)] private float fontSize = 34f;
    [SerializeField, Min(0.01f)] private float distanceSmoothingSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeSeconds = 0.18f;
    [SerializeField] private string distanceUnit = "m";

    private Transform source;
    private Camera head;
    private SourceMarker marker;

    public override void Bind(Transform sourceTransform, Camera headCamera)
    {
        source = sourceTransform;
        head = headCamera;
        marker = source != null ? source.GetComponent<SourceMarker>() : null;

        if (head == null)
            return;

        if (sharedHud == null || !sharedHud.UsesCamera(head))
            sharedHud = new SharedBottomDistanceHud(head);

        sharedHud.Register(this);
    }

    private void LateUpdate()
    {
        if (sharedHud != null)
            sharedHud.Refresh();
    }

    private void OnDestroy()
    {
        if (sharedHud == null)
            return;

        sharedHud.Unregister(this);
        if (sharedHud.IsEmpty)
        {
            sharedHud.Dispose();
            sharedHud = null;
        }
    }

    private bool IsEligible(Camera camera)
    {
        return source != null && head == camera && (marker == null || marker.isVisible);
    }

    private sealed class SharedBottomDistanceHud
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private static readonly Color BackgroundColor = new Color(0.16f, 0.15f, 0.045f, 0.9f);
        private static readonly Color BorderColor = new Color(0.72f, 0.61f, 0.10f, 1f);

        private readonly Camera head;
        private readonly List<BottomDistanceProximityPresentation> presentations =
            new List<BottomDistanceProximityPresentation>();

        private RectTransform canvasRect;
        private Canvas canvas;
        private CanvasGroup badgeCanvasGroup;
        private TMP_Text distanceText;
        private float displayedDistance;
        private float distanceVelocity;
        private bool hasDisplayedDistance;
        private int lastRefreshFrame = -1;

        public SharedBottomDistanceHud(Camera headCamera)
        {
            head = headCamera;
        }

        public bool IsEmpty => presentations.Count == 0;

        public bool UsesCamera(Camera candidate) => head == candidate;

        public void Register(BottomDistanceProximityPresentation presentation)
        {
            if (presentation != null && !presentations.Contains(presentation))
                presentations.Add(presentation);
        }

        public void Unregister(BottomDistanceProximityPresentation presentation)
        {
            presentations.Remove(presentation);
        }

        public void Refresh()
        {
            if (lastRefreshFrame == Time.frameCount)
                return;

            lastRefreshFrame = Time.frameCount;
            if (head == null)
                return;

            BottomDistanceProximityPresentation selected = FindSelectedPresentation();
            EnsureCanvas(selected);
            if (canvasRect == null || badgeCanvasGroup == null)
                return;

            PositionCanvas(selected);
            UpdateBadge(selected);
        }

        public void Dispose()
        {
            if (canvasRect != null)
                Object.Destroy(canvasRect.gameObject);
        }

        private BottomDistanceProximityPresentation FindSelectedPresentation()
        {
            BottomDistanceProximityPresentation selected = null;
            float bestAngle = float.PositiveInfinity;
            Vector3 cameraPosition = head.transform.position;
            Vector3 cameraForward = head.transform.forward;

            for (int index = presentations.Count - 1; index >= 0; index--)
            {
                BottomDistanceProximityPresentation candidate = presentations[index];
                if (candidate == null)
                {
                    presentations.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsEligible(head))
                    continue;

                Vector3 toSource = candidate.source.position - cameraPosition;
                if (Vector3.Dot(cameraForward, toSource) <= 0f)
                    continue;

                Vector3 viewport = head.WorldToViewportPoint(candidate.source.position);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
                    viewport.y < 0f || viewport.y > 1f)
                {
                    continue;
                }

                float angle = Vector3.Angle(cameraForward, toSource);
                if (angle <= candidate.selectionAngleDegrees && angle < bestAngle)
                {
                    bestAngle = angle;
                    selected = candidate;
                }
            }

            return selected;
        }

        private void EnsureCanvas(BottomDistanceProximityPresentation style)
        {
            if (canvasRect != null || style == null)
                return;

            GameObject canvasObject = new GameObject(
                "BottomDistanceProximityCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.layer = 5;

            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 101;
            canvas.worldCamera = head;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject badgeObject = new GameObject(
                "HorizontalDistanceBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup));
            badgeObject.layer = 5;

            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.SetParent(canvasRect, false);
            badgeRect.anchorMin = style.viewportAnchor;
            badgeRect.anchorMax = style.viewportAnchor;
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(style.badgeWidth, style.badgeHeight);

            Image background = badgeObject.GetComponent<Image>();
            background.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            background.type = Image.Type.Sliced;
            background.color = BackgroundColor;
            background.raycastTarget = false;

            Outline border = badgeObject.GetComponent<Outline>();
            border.effectColor = BorderColor;
            border.effectDistance = new Vector2(2f, -2f);
            border.useGraphicAlpha = true;

            badgeCanvasGroup = badgeObject.GetComponent<CanvasGroup>();
            badgeCanvasGroup.alpha = 0f;
            badgeCanvasGroup.blocksRaycasts = false;
            badgeCanvasGroup.interactable = false;

            GameObject textObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.layer = 5;

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(badgeRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 4f);
            textRect.offsetMax = new Vector2(-18f, -4f);

            distanceText = textObject.GetComponent<TextMeshProUGUI>();
            distanceText.alignment = TextAlignmentOptions.Center;
            distanceText.fontSize = style.fontSize;
            distanceText.fontStyle = FontStyles.Bold;
            distanceText.color = Color.white;
            distanceText.enableWordWrapping = false;
            distanceText.raycastTarget = false;
            distanceText.text = "SOURCE  ·  --.- m";
        }

        private void PositionCanvas(BottomDistanceProximityPresentation style)
        {
            float distance = Mathf.Max(0.2f, style.canvasDistanceMeters);
            Vector3 bottomLeft = head.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            Vector3 topLeft = head.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
            Vector3 center = head.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));

            canvasRect.position = center;
            canvasRect.rotation = head.transform.rotation;
            canvasRect.localScale = Vector3.one *
                                    (Vector3.Distance(bottomLeft, topLeft) / ReferenceHeight);

            if (canvas.worldCamera != head)
                canvas.worldCamera = head;
        }

        private void UpdateBadge(BottomDistanceProximityPresentation selected)
        {
            float fadeStep = Time.unscaledDeltaTime /
                             Mathf.Max(0.01f, selected == null ? 0.18f : selected.fadeSeconds);
            badgeCanvasGroup.alpha = Mathf.MoveTowards(
                badgeCanvasGroup.alpha,
                selected == null ? 0f : 1f,
                fadeStep);

            if (selected == null || distanceText == null)
            {
                hasDisplayedDistance = false;
                return;
            }

            Vector3 horizontalOffset = selected.source.position - head.transform.position;
            horizontalOffset.y = 0f;
            float targetDistance = horizontalOffset.magnitude;

            if (!hasDisplayedDistance)
            {
                displayedDistance = targetDistance;
                distanceVelocity = 0f;
                hasDisplayedDistance = true;
            }
            else
            {
                displayedDistance = Mathf.SmoothDamp(
                    displayedDistance,
                    targetDistance,
                    ref distanceVelocity,
                    Mathf.Max(0.01f, selected.distanceSmoothingSeconds),
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);
            }

            string unit = string.IsNullOrWhiteSpace(selected.distanceUnit) ? "m" : selected.distanceUnit;
            distanceText.text = $"SOURCE  ·  {displayedDistance:0.0} {unit}";
        }
    }
}
