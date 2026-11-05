using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ConcentricRing
{
    [Tooltip("Circle diameter as a multiple of Config > (a) Marker > Marker Size Meters, so 1 matches the plain circle and the Marker size slider scales every circle.")]
    [SerializeField, Min(0.01f)] private float diameter = 1f;

    [Tooltip("Color of the circle seen from outside. Alpha is its opacity.")]
    [SerializeField] private Color color = new Color(0f, 1f, 0f, 0.3f);

    [Tooltip("Color the whole view takes while the head is inside this circle. Alpha is the fill opacity; 0 fills nothing.")]
    [SerializeField] private Color insideColor = new Color(0f, 1f, 0f, 0.3f);

    public ConcentricRing()
    {
    }

    public ConcentricRing(float diameter, Color color)
    {
        this.diameter = diameter;
        this.color = color;
        insideColor = color;
    }

    public float Diameter => Mathf.Max(0.01f, diameter);
    public Color Color => color;
    public Color InsideColor => insideColor;
}

[Serializable]
public class ConcentricRingBand
{
    [Tooltip("Upper CPS bound of this band. The lowest band the reading fits into wins; a reading past every band uses the highest.")]
    [SerializeField, Min(0f)] private float maxCps = 10f;

    [Tooltip("Circles drawn while the reading sits in this band, in any order. Only the first eight are drawn. Empty draws nothing.")]
    [SerializeField] private List<ConcentricRing> rings = new List<ConcentricRing>();

    public ConcentricRingBand()
    {
    }

    public ConcentricRingBand(float maxCps, params ConcentricRing[] rings)
    {
        this.maxCps = maxCps;
        this.rings = new List<ConcentricRing>(rings);
    }

    public float MaxCps => maxCps;
    public IReadOnlyList<ConcentricRing> Rings => rings;
}

/// <summary>
/// CPS bands of one concentric ring look, each with its own circles, plus how the look moves between bands.
/// </summary>
[CreateAssetMenu(fileName = "a_00_concentric_ring_config", menuName = "RadVis/Concentric Ring Config")]
public class ConcentricRingConfig : ScriptableObject
{
    public const int MaxRingsPerBand = 8;

    [Header("Rule")]
    [Tooltip("Bands from low to high CPS. Press + to add one. Each band lists the circles drawn while the reading sits in it.")]
    [SerializeField] private List<ConcentricRingBand> bands = new List<ConcentricRingBand>
    {
        new ConcentricRingBand(2f),
        new ConcentricRingBand(10f,
            new ConcentricRing(1f, new Color(0f, 1f, 0f, 0.35f))),
        new ConcentricRingBand(20f,
            new ConcentricRing(1f, new Color(1f, 1f, 0f, 0.35f)),
            new ConcentricRing(2f, new Color(0f, 1f, 0f, 0.3f))),
        new ConcentricRingBand(1000f,
            new ConcentricRing(1f, new Color(1f, 0f, 0f, 0.35f)),
            new ConcentricRing(2f, new Color(1f, 1f, 0f, 0.3f)),
            new ConcentricRing(4f, new Color(0f, 1f, 0f, 0.25f)))
    };

    [Header("Transition")]
    [Tooltip("Seconds the old band's circles take to fade out while the new band's fade in. 0 swaps them in one frame.")]
    [SerializeField, Min(0f)] private float transitionSeconds = 0.5f;

    [Tooltip("Shape of that crossfade. X is elapsed progress 0 to 1, Y is the share of a band shown as it fades in; a band fading out runs the same curve backwards. Straight line fades evenly.")]
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Inside")]
    [Tooltip("How a circle turns into the view fill as the head walks in. X is depth into the circle as a share of its radius, 0 at the edge and 1 at the center. Y is the share of Inside Color shown; the circle itself fades by the same share.")]
    [SerializeField] private AnimationCurve insideBlendCurve =
        new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f));

    public float TransitionSeconds => Mathf.Max(0f, transitionSeconds);

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
            ConcentricRingBand band = bands[index];

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

    public bool TryGetRings(int bandIndex, out IReadOnlyList<ConcentricRing> rings)
    {
        rings = null;

        if (bands == null || bandIndex < 0 || bandIndex >= bands.Count || bands[bandIndex] == null)
            return false;

        rings = bands[bandIndex].Rings;
        return rings != null;
    }
}
