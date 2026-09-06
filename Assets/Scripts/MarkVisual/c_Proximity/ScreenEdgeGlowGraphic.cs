using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Draws a screen border frame that fades from the outer edge inward, with no fill in the middle.
/// </summary>
[DisallowMultipleComponent]
public class ScreenEdgeGlowGraphic : MaskableGraphic
{
    [Header("Frame")]
    [Tooltip("Thickness of the border as a fraction of the shorter screen side.")]
    [SerializeField, Range(0.02f, 0.5f)] private float thicknessFraction = 0.18f;

    [Tooltip("Opacity at the outer screen edge, relative to the graphic color.")]
    [SerializeField, Range(0f, 1f)] private float outerAlpha = 1f;

    [Tooltip("Opacity where the border meets the clear center.")]
    [SerializeField, Range(0f, 1f)] private float innerAlpha = 0f;

    public void SetThicknessFraction(float fraction)
    {
        float clamped = Mathf.Clamp(fraction, 0.02f, 0.5f);
        if (Mathf.Approximately(clamped, thicknessFraction))
            return;

        thicknessFraction = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float thickness = Mathf.Min(rect.width, rect.height) * thicknessFraction;
        thickness = Mathf.Min(thickness, Mathf.Min(rect.width, rect.height) * 0.5f);

        Color outer = color;
        outer.a *= outerAlpha;

        Color inner = color;
        inner.a *= innerAlpha;

        Vector2 outerMin = new Vector2(rect.xMin, rect.yMin);
        Vector2 outerMax = new Vector2(rect.xMax, rect.yMax);
        Vector2 innerMin = outerMin + new Vector2(thickness, thickness);
        Vector2 innerMax = outerMax - new Vector2(thickness, thickness);

        AddQuad(vertexHelper,
            new Vector2(outerMin.x, outerMax.y), new Vector2(outerMax.x, outerMax.y),
            new Vector2(innerMax.x, innerMax.y), new Vector2(innerMin.x, innerMax.y),
            outer, inner);

        AddQuad(vertexHelper,
            new Vector2(outerMax.x, outerMin.y), new Vector2(outerMin.x, outerMin.y),
            new Vector2(innerMin.x, innerMin.y), new Vector2(innerMax.x, innerMin.y),
            outer, inner);

        AddQuad(vertexHelper,
            new Vector2(outerMin.x, outerMin.y), new Vector2(outerMin.x, outerMax.y),
            new Vector2(innerMin.x, innerMax.y), new Vector2(innerMin.x, innerMin.y),
            outer, inner);

        AddQuad(vertexHelper,
            new Vector2(outerMax.x, outerMax.y), new Vector2(outerMax.x, outerMin.y),
            new Vector2(innerMax.x, innerMin.y), new Vector2(innerMax.x, innerMax.y),
            outer, inner);
    }

    private static void AddQuad(
        VertexHelper vertexHelper,
        Vector2 outerFirst,
        Vector2 outerSecond,
        Vector2 innerSecond,
        Vector2 innerFirst,
        Color outerColor,
        Color innerColor)
    {
        int firstIndex = vertexHelper.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;

        vertex.color = outerColor;
        vertex.position = outerFirst;
        vertexHelper.AddVert(vertex);
        vertex.position = outerSecond;
        vertexHelper.AddVert(vertex);

        vertex.color = innerColor;
        vertex.position = innerSecond;
        vertexHelper.AddVert(vertex);
        vertex.position = innerFirst;
        vertexHelper.AddVert(vertex);

        vertexHelper.AddTriangle(firstIndex, firstIndex + 1, firstIndex + 2);
        vertexHelper.AddTriangle(firstIndex + 2, firstIndex + 3, firstIndex);
    }
}
