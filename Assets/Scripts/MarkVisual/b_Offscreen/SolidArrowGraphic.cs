using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws one filled flat-backed triangle pointing along +X, with no outline and no arc.
/// </summary>
[DisallowMultipleComponent]
public class SolidArrowGraphic : MaskableGraphic
{
    [Header("Triangle")]
    [Tooltip("Width of the flat back edge, across the direction the arrow points.")]
    [SerializeField, Min(1f)] private float baseWidth = 80f;

    [Tooltip("Distance from the back edge to the tip, along the direction the arrow points. 80 by 38 gives the reference arrow's 93 degree tip.")]
    [SerializeField, Min(1f)] private float length = 38f;

    public void SetSize(float newLength, float newBaseWidth)
    {
        length = Mathf.Max(1f, newLength);
        baseWidth = Mathf.Max(1f, newBaseWidth);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = new Vector3(length * 0.5f, 0f, 0f);
        vertexHelper.AddVert(vertex);

        vertex.position = new Vector3(-length * 0.5f, baseWidth * 0.5f, 0f);
        vertexHelper.AddVert(vertex);

        vertex.position = new Vector3(-length * 0.5f, -baseWidth * 0.5f, 0f);
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(0, 1, 2);
    }
}
