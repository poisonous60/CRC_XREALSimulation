using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ConcentricSphere
{
    [Tooltip("Sphere diameter as a multiple of Config > (a) Marker > Marker Size Meters, so 1 matches the plain marker and the Marker size slider scales every sphere.")]
    [SerializeField, Min(0.01f)] private float diameter = 1f;

    [Tooltip("Color of the sphere seen from outside. Alpha is its opacity.")]
    [SerializeField] private Color color = new Color(0f, 1f, 0f, 0.3f);

    [Tooltip("Color of the grid lines on the inside of this sphere while the head is in it. Alpha is the line opacity; 0 draws no grid.")]
    [SerializeField] private Color insideColor = new Color(0f, 1f, 0f, 0.6f);

    public ConcentricSphere()
    {
    }

    public ConcentricSphere(float diameter, Color color, Color insideColor)
    {
        this.diameter = diameter;
        this.color = color;
        this.insideColor = insideColor;
    }

    public float Diameter => Mathf.Max(0.01f, diameter);
    public Color Color => color;
    public Color InsideColor => insideColor;
}

[Serializable]
public class ConcentricSphereBand
{
    [Tooltip("Upper CPS bound of this band. The lowest band the reading fits into wins; a reading past every band uses the highest.")]
    [SerializeField, Min(0f)] private float maxCps = 10f;

    [Tooltip("Spheres drawn while the reading sits in this band, in any order. Only the first eight are drawn. Empty draws nothing.")]
    [SerializeField] private List<ConcentricSphere> spheres = new List<ConcentricSphere>();

    public ConcentricSphereBand()
    {
    }

    public ConcentricSphereBand(float maxCps, params ConcentricSphere[] spheres)
    {
        this.maxCps = maxCps;
        this.spheres = new List<ConcentricSphere>(spheres);
    }

    public float MaxCps => maxCps;
    public IReadOnlyList<ConcentricSphere> Spheres => spheres;
}

/// <summary>
/// CPS bands of one concentric sphere look, each with its own spheres, plus how the look moves between bands
/// and what the inside of a sphere shows.
/// </summary>
[CreateAssetMenu(fileName = "a_00_concentric_sphere_config", menuName = "RadVis/Concentric Sphere Config")]
public class ConcentricSphereConfig : ScriptableObject
{
    public const int MaxSpheresPerBand = 8;

    [Header("Rule")]
    [Tooltip("Bands from low to high CPS. Press + to add one. Each band lists the spheres drawn while the reading sits in it.")]
    [SerializeField] private List<ConcentricSphereBand> bands = new List<ConcentricSphereBand>
    {
        new ConcentricSphereBand(2f),
        new ConcentricSphereBand(10f,
            new ConcentricSphere(1f, new Color(0f, 1f, 0f, 0.35f), new Color(0f, 1f, 0f, 0.6f))),
        new ConcentricSphereBand(20f,
            new ConcentricSphere(1f, new Color(1f, 1f, 0f, 0.35f), new Color(1f, 1f, 0f, 0.6f)),
            new ConcentricSphere(2f, new Color(0f, 1f, 0f, 0.3f), new Color(0f, 1f, 0f, 0.6f))),
        new ConcentricSphereBand(1000f,
            new ConcentricSphere(1f, new Color(1f, 0f, 0f, 0.35f), new Color(1f, 0f, 0f, 0.6f)),
            new ConcentricSphere(2f, new Color(1f, 1f, 0f, 0.3f), new Color(1f, 1f, 0f, 0.6f)),
            new ConcentricSphere(4f, new Color(0f, 1f, 0f, 0.25f), new Color(0f, 1f, 0f, 0.6f)))
    };

    [Header("Transition")]
    [Tooltip("Seconds the old band's spheres take to fade out while the new band's fade in. 0 swaps them in one frame.")]
    [SerializeField, Min(0f)] private float transitionSeconds = 0.5f;

    [Tooltip("Shape of that crossfade. X is elapsed progress 0 to 1, Y is the share of a band shown as it fades in; a band fading out runs the same curve backwards. Straight line fades evenly.")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Walking In")]
    [Tooltip("The part of a sphere's outside surface closer to the head than this fades out, so walking into the sphere does not cut it off in one frame. Over the same distance the grid around the head fades out before a smaller sphere's grid comes up. 0 turns both fades off.")]
    [SerializeField, Min(0f)] private float nearFadeMeters = 0.5f;

    [Tooltip("How the grid on the inside of a sphere comes up as the head walks in. X is depth into the sphere as a share of its radius, 0 at the surface and 1 at the center. Y is the share of the grid shown. Only the smallest sphere the head stands in shows its grid.")]
    [SerializeField] private AnimationCurve insideBlendCurve =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f));

    [Header("Inside Grid")]
    [Tooltip("Latitude lines split the sphere from pole to pole into this many steps. The poles sit straight above and below the source.")]
    [SerializeField, Min(1)] private int latitudeSteps = 12;

    [Tooltip("Longitude lines split the sphere around its vertical axis into this many steps.")]
    [SerializeField, Min(1)] private int longitudeSteps = 24;

    [Tooltip("Grid line width in screen pixels, the same at any distance.")]
    [SerializeField, Min(0.5f)] private float lineWidthPixels = 1.5f;

    public float TransitionSeconds => Mathf.Max(0f, transitionSeconds);
    public float NearFadeMeters => Mathf.Max(0f, nearFadeMeters);
    public int LatitudeSteps => Mathf.Max(1, latitudeSteps);
    public int LongitudeSteps => Mathf.Max(1, longitudeSteps);
    public float LineWidthPixels => Mathf.Max(0.5f, lineWidthPixels);

    public float EvaluateTransition(float progress)
    {
        float clamped = Mathf.Clamp01(progress);

        if (transitionCurve == null || transitionCurve.length == 0)
            return clamped;

        return Mathf.Clamp01(transitionCurve.Evaluate(clamped));
    }

    public float EvaluateInsideBlend(float headDistance, float radius)
    {
        if (radius <= 0f || headDistance > radius)
            return 0f;

        if (insideBlendCurve == null || insideBlendCurve.length == 0)
            return 1f;

        return Mathf.Clamp01(insideBlendCurve.Evaluate(1f - headDistance / radius));
    }

    public bool TryGetBandIndex(float countsPerSecond, out int bandIndex)
    {
        bandIndex = -1;

        if (bands == null || float.IsNaN(countsPerSecond) || float.IsInfinity(countsPerSecond) || countsPerSecond < 0f)
            return false;

        float bestBound = float.PositiveInfinity;
        float highestBound = float.NegativeInfinity;
        int highestIndex = -1;

        for (int index = 0; index < bands.Count; index++)
        {
            ConcentricSphereBand band = bands[index];

            if (band == null)
                continue;

            if (band.MaxCps > highestBound)
            {
                highestBound = band.MaxCps;
                highestIndex = index;
            }

            if (countsPerSecond <= band.MaxCps && band.MaxCps < bestBound)
            {
                bestBound = band.MaxCps;
                bandIndex = index;
            }
        }

        if (bandIndex < 0)
            bandIndex = highestIndex;

        return bandIndex >= 0;
    }

    public bool TryGetSpheres(int bandIndex, out IReadOnlyList<ConcentricSphere> spheres)
    {
        spheres = null;

        if (bands == null || bandIndex < 0 || bandIndex >= bands.Count || bands[bandIndex] == null)
            return false;

        spheres = bands[bandIndex].Spheres;
        return spheres != null;
    }
}
