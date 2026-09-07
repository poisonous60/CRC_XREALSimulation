using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws one filled arrowhead pointing along +X: a triangle whose back edge can bow inward as an arc.
/// </summary>
[DisallowMultipleComponent]
public class SolidArrowGraphic : MaskableGraphic
{
    [Header("Triangle")]
    [Tooltip("Width of the back edge, across the direction the arrow points.")]
    [SerializeField, Min(1f)] private float baseWidth = 80f;

    [Tooltip("Distance from the back corners to the tip, along the direction the arrow points. 80 by 38 gives the reference arrow's 93 degree tip.")]
    [SerializeField, Min(1f)] private float length = 38f;

    [Header("Back arc")]
    [Tooltip("Angle the back arc spans. 0 keeps the back edge straight, 180 bows it into a half circle toward the tip.")]
    [SerializeField, Range(0f, 180f)] private float backArcDegrees = 0f;

    [Tooltip("Segments used to approximate the back arc.")]
    [SerializeField, Range(1, 32)] private int backArcSegments = 12;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(length * 0.5f, 0f, 0f);
        vertexHelper.AddVert(vertex);

        float halfWidth = baseWidth * 0.5f;
        float backX = -length * 0.5f;
        // Sagitta of the arc, capped below the length so its middle never reaches the tip.
        float depth = Mathf.Min(halfWidth * Mathf.Tan(backArcDegrees * 0.25f * Mathf.Deg2Rad), length * 0.9f);

        if (depth <= 0f)
        {
            vertex.position = new Vector3(backX, halfWidth, 0f);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(backX, -halfWidth, 0f);
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(0, 1, 2);
            return;
        }

        float radius = (depth * depth + halfWidth * halfWidth) / (2f * depth);
        float centerX = backX + depth - radius;
        float halfAngle = Mathf.Asin(Mathf.Clamp01(halfWidth / radius));

        for (int index = 0; index <= backArcSegments; index++)
        {
            float angle = halfAngle - halfAngle * 2f * index / backArcSegments;
            vertex.position = new Vector3(centerX + Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            vertexHelper.AddVert(vertex);
        }

        for (int index = 1; index <= backArcSegments; index++)
            vertexHelper.AddTriangle(0, index, index + 1);
    }
}
