#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor-only: bakes a uGUI Graphic's shape into a Mesh asset for the Overview catalog.
/// </summary>
public static class GraphicMeshBaker
{
    private const string MenuPath = "Tools/RadVis/Bake Graphic To Mesh";

    public static bool TryBake(Graphic graphic, string assetPath, out string message)
    {
        if (graphic == null)
        {
            message = "No Graphic given.";
            return false;
        }

        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".asset"))
        {
            message = "Asset path must end with .asset.";
            return false;
        }

        if (!TryFindPopulateMethod(graphic.GetType(), out MethodInfo populate))
        {
            message = $"{graphic.GetType().Name} has no OnPopulateMesh(VertexHelper).";
            return false;
        }

        VertexHelper helper = new VertexHelper();
        populate.Invoke(graphic, new object[] { helper });

        Mesh baked = new Mesh();
        baked.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        helper.FillMesh(baked);
        helper.Dispose();
        baked.RecalculateBounds();

        if (baked.vertexCount == 0)
        {
            UnityEngine.Object.DestroyImmediate(baked);
            message = $"{graphic.GetType().Name} produced no vertices. Check its rect size and shape fields.";
            return false;
        }

        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            existing.Clear();
            existing.vertices = baked.vertices;
            existing.triangles = baked.triangles;
            existing.colors = baked.colors;
            existing.uv = baked.uv;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(baked);
        }
        else
        {
            AssetDatabase.CreateAsset(baked, assetPath);
        }

        AssetDatabase.SaveAssets();
        message = $"Baked {graphic.GetType().Name} to {assetPath}.";
        return true;
    }

    // OnPopulateMesh is protected, so the shape cannot be requested through a public call.
    // Reflection keeps the baking out of the representation classes themselves.
    private static bool TryFindPopulateMethod(Type graphicType, out MethodInfo populate)
    {
        populate = null;
        Type current = graphicType;

        while (current != null && populate == null)
        {
            populate = current.GetMethod(
                "OnPopulateMesh",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new Type[] { typeof(VertexHelper) },
                null);

            current = current.BaseType;
        }

        return populate != null;
    }

    [MenuItem(MenuPath, true)]
    private static bool CanBakeSelection()
    {
        return Selection.activeGameObject != null &&
               Selection.activeGameObject.GetComponent<Graphic>() != null;
    }

    [MenuItem(MenuPath)]
    private static void BakeSelection()
    {
        Graphic graphic = Selection.activeGameObject.GetComponent<Graphic>();

        string assetPath = EditorUtility.SaveFilePanelInProject(
            "Bake Graphic To Mesh",
            graphic.GetType().Name + "_Baked",
            "asset",
            "Where should the baked mesh go?");

        if (string.IsNullOrEmpty(assetPath))
            return;

        if (TryBake(graphic, assetPath, out string message))
            UnityEngine.Debug.Log($"[GraphicMeshBaker] {message}");
        else
            UnityEngine.Debug.LogWarning($"[GraphicMeshBaker] {message}");
    }
}
#endif
