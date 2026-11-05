using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Draws the spheres the sphere rule gives for the marker's reading on one sphere mesh, crossfades between bands,
/// and lines the inside of a sphere with a grid while the head stands in it.
/// </summary>
[DisallowMultipleComponent]
public class ConcentricSphereMarkerVisual : MonoBehaviour
{
    private const int MaxSpheres = ConcentricSphereConfig.MaxSpheresPerBand;

    // The mesh is a polygon whose faces sit slightly inside the true sphere, so it is drawn this
    // much larger to keep the largest sphere's edge on it.
    private const float MeshMargin = 1.05f;

    private static readonly int SphereRadiusId = Shader.PropertyToID("_SphereRadius");
    private static readonly int SphereColorId = Shader.PropertyToID("_SphereColor");
    private static readonly int SphereInsideColorId = Shader.PropertyToID("_SphereInsideColor");
    private static readonly int SphereInsideId = Shader.PropertyToID("_SphereInside");
    private static readonly int SphereGridWeightId = Shader.PropertyToID("_SphereGridWeight");
    private static readonly int SphereCountsId = Shader.PropertyToID("_SphereCounts");
    private static readonly int SetWeightsId = Shader.PropertyToID("_SetWeights");
    private static readonly int GridId = Shader.PropertyToID("_Grid");

    [Header("Look")]
    [Tooltip("Sphere rule, transition and inside grid for this look. Empty draws nothing.")]
    [SerializeField] private ConcentricSphereConfig config;

    [Tooltip("Child carrying the sphere mesh. Empty means the first child.")]
    [SerializeField] private Transform visual;

    [Tooltip("Camera the inside test uses. Empty means Camera.main.")]
    [SerializeField] private Camera head;

    private readonly float[] sphereRadii = new float[MaxSpheres * 2];
    private readonly Vector4[] sphereColors = new Vector4[MaxSpheres * 2];
    private readonly Vector4[] insideColors = new Vector4[MaxSpheres * 2];
    private readonly float[] insideFlags = new float[MaxSpheres * 2];
    private readonly float[] gridWeights = new float[MaxSpheres * 2];
    private readonly List<ConcentricSphere> sortedSpheres = new List<ConcentricSphere>(MaxSpheres);

    private SourceMarker owner;
    private Renderer sphereRenderer;
    private MaterialPropertyBlock propertyBlock;
    private bool hasBand;
    private int incomingBand = -1;
    private int outgoingBand = -1;
    private float incomingProgress = 1f;
    private float outgoingProgress;

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

        int incomingCount = WriteSet(incomingBand, 0, headDistance, metersPerUnit, out float incomingLargest);
        int outgoingCount = WriteSet(outgoingBand, MaxSpheres, headDistance, metersPerUnit, out float outgoingLargest);
        float largest = Mathf.Max(incomingLargest, outgoingLargest);

        if (largest > 0f)
            visual.localScale = Vector3.one * (largest * MeshMargin);

        sphereRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloatArray(SphereRadiusId, sphereRadii);
        propertyBlock.SetVectorArray(SphereColorId, sphereColors);
        propertyBlock.SetVectorArray(SphereInsideColorId, insideColors);
        propertyBlock.SetFloatArray(SphereInsideId, insideFlags);
        propertyBlock.SetFloatArray(SphereGridWeightId, gridWeights);
        propertyBlock.SetVector(SphereCountsId, new Vector4(incomingCount, outgoingCount, 0f, 0f));
        propertyBlock.SetVector(SetWeightsId, new Vector4(
            config.EvaluateTransition(incomingProgress),
            config.EvaluateTransition(outgoingProgress),
            0f,
            0f));
        propertyBlock.SetVector(GridId, new Vector4(
            config.LatitudeSteps,
            config.LongitudeSteps,
            config.LineWidthPixels,
            config.NearFadeMeters));
        sphereRenderer.SetPropertyBlock(propertyBlock);
    }

    private bool TryResolveParts()
    {
        if (visual == null)
        {
            if (transform.childCount == 0)
                return false;

            visual = transform.GetChild(0);
        }

        if (sphereRenderer == null && !visual.TryGetComponent(out sphereRenderer))
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
        // A band with no spheres must not outrank a real band still fading out.
        incomingProgress = incomingBand >= 0 ? Mathf.MoveTowards(incomingProgress, 1f, step) : 0f;
        outgoingProgress = Mathf.MoveTowards(outgoingProgress, 0f, step);

        if (outgoingProgress <= 0f)
            outgoingBand = -1;
    }

    private int WriteSet(int bandIndex, int offset, float headDistance, float metersPerUnit, out float largestDiameter)
    {
        largestDiameter = 0f;
        sortedSpheres.Clear();

        if (config.TryGetSpheres(bandIndex, out IReadOnlyList<ConcentricSphere> spheres))
        {
            for (int i = 0; i < spheres.Count && sortedSpheres.Count < MaxSpheres; i++)
            {
                if (spheres[i] != null)
                    sortedSpheres.Add(spheres[i]);
            }
        }

        sortedSpheres.Sort((a, b) => a.Diameter.CompareTo(b.Diameter));

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        int innermost = -1;

        for (int i = 0; i < MaxSpheres; i++)
        {
            gridWeights[offset + i] = 0f;

            if (i >= sortedSpheres.Count)
            {
                sphereRadii[offset + i] = 0f;
                sphereColors[offset + i] = Vector4.zero;
                insideColors[offset + i] = Vector4.zero;
                insideFlags[offset + i] = 0f;
                continue;
            }

            ConcentricSphere sphere = sortedSpheres[i];
            float radius = sphere.Diameter * 0.5f * metersPerUnit;
            bool inside = headDistance <= radius;

            if (inside && innermost < 0)
                innermost = i;

            // Arrays skip the sRGB to linear step SetColor does, so it is done here.
            sphereRadii[offset + i] = radius;
            sphereColors[offset + i] = linear ? sphere.Color.linear : sphere.Color;
            insideColors[offset + i] = linear ? sphere.InsideColor.linear : sphere.InsideColor;
            insideFlags[offset + i] = inside ? 1f : 0f;
            largestDiameter = Mathf.Max(largestDiameter, sphere.Diameter);
        }

        // Only the smallest sphere the head stands in shows its grid. Nearing a smaller one fades that grid
        // out before the smaller one's comes up, so two grids never overlap.
        if (innermost >= 0)
        {
            float weight = config.EvaluateInsideBlend(headDistance, sphereRadii[offset + innermost]);

            if (innermost > 0 && config.NearFadeMeters > 0f)
                weight *= Mathf.Clamp01((headDistance - sphereRadii[offset + innermost - 1]) / config.NearFadeMeters);

            gridWeights[offset + innermost] = weight;
        }

        return sortedSpheres.Count;
    }
}
