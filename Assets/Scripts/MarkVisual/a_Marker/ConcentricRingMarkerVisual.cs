using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the circles the ring rule gives for the marker's reading on one quad, crossfades between bands,
/// and fills the view with a circle's inside color while the head stands in it.
/// </summary>
[DisallowMultipleComponent]
public class ConcentricRingMarkerVisual : MonoBehaviour
{
    private const int MaxRings = ConcentricRingConfig.MaxRingsPerBand;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    private static readonly int RingColorId = Shader.PropertyToID("_RingColor");
    private static readonly int RingKeepId = Shader.PropertyToID("_RingKeep");
    private static readonly int RingCountsId = Shader.PropertyToID("_RingCounts");
    private static readonly int SetWeightsId = Shader.PropertyToID("_SetWeights");

    [Header("Look")]
    [Tooltip("Ring rule, transition and inside fill for this look. Empty draws nothing.")]
    [SerializeField] private ConcentricRingConfig config;

    [Tooltip("Child carrying the quad. Empty means the first child.")]
    [SerializeField] private Transform visual;

    [Tooltip("Camera the inside test and the fill use. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    [Header("Inside Fill")]
    [Tooltip("Distance from the head at which the fill canvas is parked.")]
    [SerializeField, Min(0.2f)] private float canvasDistanceMeters = 0.5f;

    [Tooltip("Draw order of the view fill canvas, not of the circles. The circles' renderer sits at 0, so below 0 the circles draw over the fill. ARDetectorHud sits at 100.")]
    [SerializeField] private int fillSortingOrder = -1;

    private readonly float[] ringRadii = new float[MaxRings * 2];
    private readonly Vector4[] ringColors = new Vector4[MaxRings * 2];
    private readonly float[] ringKeeps = new float[MaxRings * 2];
    private readonly List<ConcentricRing> sortedRings = new List<ConcentricRing>(MaxRings);
    private readonly float[] insideBlends = new float[MaxRings];

    private SourceMarker owner;
    private Renderer ringRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool hasBand;
    private int incomingBand = -1;
    private int outgoingBand = -1;
    private float incomingProgress = 1f;
    private float outgoingProgress;
    private Canvas fillCanvas;
    private RectTransform canvasRect;
    private Image fill;

    private void LateUpdate()
    {
        if (config == null || !TryResolveParts())
            return;

        // The manager adds SourceMarker after Awake, and a catalog copy never gets one.
        if (owner == null)
            owner = GetComponent<SourceMarker>();

        UpdateBand();

        float headDistance = head != null
            ? Vector3.Distance(head.transform.position, transform.position)
            : float.PositiveInfinity;

        // The root carries Marker Size Meters, so one diameter unit is this many meters.
        float metersPerUnit = Mathf.Abs(transform.lossyScale.x);

        float incomingWeight = config.EvaluateTransition(incomingProgress);
        float outgoingWeight = config.EvaluateTransition(outgoingProgress);

        int incomingCount = WriteSet(incomingBand, 0, headDistance, metersPerUnit,
            out float incomingLargest, out Vector4 incomingFill);
        int outgoingCount = WriteSet(outgoingBand, MaxRings, headDistance, metersPerUnit,
            out float outgoingLargest, out Vector4 outgoingFill);
        float largest = Mathf.Max(incomingLargest, outgoingLargest);

        if (largest > 0f)
        {
            for (int i = 0; i < ringRadii.Length; i++)
                ringRadii[i] /= largest;

            visual.localScale = Vector3.one * largest;
        }

        ringRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloatArray(RingRadiusId, ringRadii);
        propertyBlock.SetVectorArray(RingColorId, ringColors);
        propertyBlock.SetFloatArray(RingKeepId, ringKeeps);
        propertyBlock.SetVector(RingCountsId, new Vector4(incomingCount, outgoingCount, 0f, 0f));
        propertyBlock.SetVector(SetWeightsId, new Vector4(incomingWeight, outgoingWeight, 0f, 0f));
        ringRenderer.SetPropertyBlock(propertyBlock);

        UpdateFill(incomingFill * incomingWeight + outgoingFill * outgoingWeight);
    }

    private bool TryResolveParts()
    {
        if (visual == null)
        {
            if (transform.childCount == 0)
                return false;

            visual = transform.GetChild(0);
        }

        if (ringRenderer == null && !visual.TryGetComponent(out ringRenderer))
            return false;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (head == null)
            head = Camera.main;

        return true;
    }

    private void UpdateBand()
    {
        // A catalog copy has no reading, so it shows the band with the most to look at.
        float reading = owner != null ? owner.lastRadiationValue : float.MaxValue;
        int band = config.TryGetBandIndex(reading, out int index) ? index : -1;

        if (!hasBand)
        {
            hasBand = true;
            incomingBand = band;
            incomingProgress = 1f;
            outgoingBand = -1;
            outgoingProgress = 0f;
            return;
        }

        if (band != incomingBand)
        {
            // Turning back to the band still fading out picks it up where it is, so a reading
            // hovering on a boundary never pops.
            if (band == outgoingBand)
            {
                outgoingBand = incomingBand;
                incomingBand = band;
                float swap = incomingProgress;
                incomingProgress = outgoingProgress;
                outgoingProgress = swap;
            }
            else
            {
                // Only two bands fit on screen, so the fainter of the current two gives way.
                if (incomingProgress >= outgoingProgress)
                {
                    outgoingBand = incomingBand;
                    outgoingProgress = incomingProgress;
                }

                incomingBand = band;
                incomingProgress = 0f;
            }
        }

        float seconds = config.TransitionSeconds;
        float step = seconds > 0f ? Time.deltaTime / seconds : 1f;
        // A band with no circles must not outrank a real band still fading out.
        incomingProgress = incomingBand >= 0 ? Mathf.MoveTowards(incomingProgress, 1f, step) : 0f;
        outgoingProgress = Mathf.MoveTowards(outgoingProgress, 0f, step);

        if (outgoingProgress <= 0f)
            outgoingBand = -1;
    }

    private int WriteSet(int bandIndex, int offset, float headDistance, float metersPerUnit,
        out float largestDiameter, out Vector4 insideFill)
    {
        largestDiameter = 0f;
        insideFill = Vector4.zero;
        sortedRings.Clear();

        if (config.TryGetRings(bandIndex, out IReadOnlyList<ConcentricRing> rings))
        {
            for (int i = 0; i < rings.Count && sortedRings.Count < MaxRings; i++)
            {
                if (rings[i] != null)
                    sortedRings.Add(rings[i]);
            }
        }

        sortedRings.Sort((a, b) => a.Diameter.CompareTo(b.Diameter));

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        for (int i = 0; i < MaxRings; i++)
        {
            if (i >= sortedRings.Count)
            {
                ringRadii[offset + i] = 0f;
                ringColors[offset + i] = Vector4.zero;
                ringKeeps[offset + i] = 0f;
                continue;
            }

            ConcentricRing ring = sortedRings[i];
            float blend = config.EvaluateInsideBlend(headDistance, ring.Diameter * 0.5f * metersPerUnit);
            insideBlends[i] = blend;

            // Arrays skip the sRGB to linear step SetColor does, so it is done here.
            Color color = linear ? ring.Color.linear : ring.Color;
            color.a = ring.Color.a * (1f - blend);

            ringRadii[offset + i] = ring.Diameter;
            ringColors[offset + i] = color;
            ringKeeps[offset + i] = 1f - blend;
            largestDiameter = Mathf.Max(largestDiameter, ring.Diameter);
        }

        // Largest first, so each smaller circle the head walks into takes the fill over from the one around it.
        for (int i = sortedRings.Count - 1; i >= 0; i--)
        {
            if (insideBlends[i] <= 0f)
                continue;

            Color inside = sortedRings[i].InsideColor;
            Vector4 premultiplied = new Vector4(inside.r * inside.a, inside.g * inside.a, inside.b * inside.a, inside.a);
            insideFill = Vector4.Lerp(insideFill, premultiplied, insideBlends[i]);
        }

        return sortedRings.Count;
    }

    private void UpdateFill(Vector4 premultiplied)
    {
        if (owner == null || head == null)
            return;

        EnsureCanvas();

        if (fill == null)
            return;

        // The manager fades the marker in and out through _Color's alpha, so the fill follows the circles.
        float markerFade = owner.isPlaced && ringRenderer.enabled && ringRenderer.sharedMaterial != null
            ? ringRenderer.sharedMaterial.GetColor(ColorId).a
            : 0f;
        float alpha = premultiplied.w * markerFade;
        bool showing = alpha > 0.001f;

        if (fill.enabled != showing)
            fill.enabled = showing;

        if (!showing)
            return;

        FollowHead();
        fill.color = new Color(
            premultiplied.x / premultiplied.w,
            premultiplied.y / premultiplied.w,
            premultiplied.z / premultiplied.w,
            alpha);
    }

    private void EnsureCanvas()
    {
        if (fillCanvas != null)
            return;

        // Built outside the marker root, where the manager would otherwise color and hide it.
        GameObject canvasObject = new GameObject("ConcentricRingFillCanvas", typeof(Canvas));
        canvasObject.layer = 5;
        fillCanvas = canvasObject.GetComponent<Canvas>();
        fillCanvas.renderMode = RenderMode.WorldSpace;
        fillCanvas.worldCamera = head;
        fillCanvas.overrideSorting = true;
        fillCanvas.sortingOrder = fillSortingOrder;

        canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.localScale = Vector3.one;

        // RequireComponent is editor-only, so a script-built Graphic gets no CanvasRenderer.
        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer));
        fillObject.layer = 5;
        fillObject.transform.SetParent(canvasObject.transform, false);

        RectTransform fillRect = (RectTransform)fillObject.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillObject.AddComponent<Image>();
        fill.raycastTarget = false;
        fill.enabled = false;
    }

    private void FollowHead()
    {
        float distance = Mathf.Max(0.2f, canvasDistanceMeters);
        Vector3 center = head.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
        float visibleHeight = HeadViewport.VisibleHeight(head, distance);

        canvasRect.position = center;
        canvasRect.rotation = head.transform.rotation;
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta = new Vector2(visibleHeight * HeadViewport.Aspect(head), visibleHeight);
    }

    private void OnDestroy()
    {
        if (fillCanvas != null)
            Destroy(fillCanvas.gameObject);
    }
}
