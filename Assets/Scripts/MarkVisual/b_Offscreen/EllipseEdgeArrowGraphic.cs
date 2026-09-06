using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws the (b) ellipse-border cue: an outline chevron pointing along +X with a short arc behind it.
/// </summary>
[DisallowMultipleComponent]
public class EllipseEdgeArrowGraphic : MaskableGraphic
{
    [Header("Chevron")]
    [Tooltip("Distance from the chevron's back edge to its tip, in canvas units.")]
    [SerializeField, Min(1f)] private float chevronLength = 36f;

    [Tooltip("Half height of the chevron's back edge, in canvas units.")]
    [SerializeField, Min(1f)] private float chevronHalfWidth = 36f;

    [Tooltip("Stroke thickness of the chevron. The chevron is an outline, never a filled triangle.")]
    [SerializeField, Min(0.5f)] private float chevronStroke = 4f;

    [Header("Halo arc")]
    [Tooltip("Show the short arc that stands for the ellipse border the chevron sits on.")]
    [SerializeField] private bool showArc = true;

    [Tooltip("Radius of the arc. Larger values flatten it toward a straight border.")]
    [SerializeField, Min(1f)] private float arcRadius = 300f;

    [Tooltip("Half of the arc's angular span in degrees. 12 gives the 24 degree span the reference uses.")]
    [SerializeField, Range(2f, 45f)] private float arcHalfAngleDegrees = 12f;

    [Tooltip("Stroke thickness of the arc.")]
    [SerializeField, Min(0.5f)] private float arcStroke = 4f;

    [Tooltip("Gap between the chevron tip and the arc.")]
    [SerializeField, Min(0f)] private float arcGap = 10f;

    [Tooltip("Opacity of the arc relative to the chevron.")]
    [SerializeField, Range(0f, 1f)] private float arcAlpha = 0.55f;

    [Tooltip("Segments used to approximate the arc.")]
    [SerializeField, Range(2, 48)] private int arcSegments = 12;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Vector2 tip = new Vector2(chevronLength * 0.5f, 0f);
        Vector2 upperBack = new Vector2(-chevronLength * 0.5f, chevronHalfWidth);
        Vector2 lowerBack = new Vector2(-chevronLength * 0.5f, -chevronHalfWidth);

        AddStrokeSegment(vertexHelper, upperBack, tip, chevronStroke, color);
        AddStrokeSegment(vertexHelper, tip, lowerBack, chevronStroke, color);

        if (showArc)
            AddArc(vertexHelper);
    }

    private void AddArc(VertexHelper vertexHelper)
    {
        // A circular arc stands in for the viewport ellipse so this graphic needs no screen data.
        float centerX = chevronLength * 0.5f + arcGap - arcRadius;
        Vector2 arcCenter = new Vector2(centerX, 0f);

        Color arcColor = color;
        arcColor.a *= arcAlpha;

        float halfAngle = arcHalfAngleDegrees * Mathf.Deg2Rad;
        float step = halfAngle * 2f / arcSegments;
        Vector2 previous = arcCenter + PointOnCircle(-halfAngle, arcRadius);

        for (int index = 1; index <= arcSegments; index++)
        {
            Vector2 current = arcCenter + PointOnCircle(-halfAngle + step * index, arcRadius);
            AddStrokeSegment(vertexHelper, previous, current, arcStroke, arcColor);
            previous = current;
        }
    }

    private static Vector2 PointOnCircle(float angleRadians, float radius)
    {
        return new Vector2(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius);
    }

    private static void AddStrokeSegment(
        VertexHelper vertexHelper,
        Vector2 from,
        Vector2 to,
        float thickness,
        Color segmentColor)
    {
        Vector2 along = to - from;
        if (along.sqrMagnitude < 0.000001f)
            return;

        Vector2 normal = new Vector2(-along.y, along.x).normalized * (thickness * 0.5f);
        int firstIndex = vertexHelper.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = segmentColor;

        vertex.position = from - normal;
        vertexHelper.AddVert(vertex);
        vertex.position = from + normal;
        vertexHelper.AddVert(vertex);
        vertex.position = to + normal;
        vertexHelper.AddVert(vertex);
        vertex.position = to - normal;
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
        vertexHelper.AddTriangle(firstIndex + 2, firstIndex + 3, firstIndex);
    }
}
