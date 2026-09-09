using UnityEditor;
using UnityEngine;

/// <summary>Draws an "Edit Script" button under every project ScriptableObject asset.</summary>
[CustomEditor(typeof(ScriptableObject), true)]
public sealed class ConfigScriptShortcut : Editor
{
    private const string ProjectScriptRoot = "Assets/Scripts/";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!TryGetScript(target as ScriptableObject, out MonoScript script))
            return;

        EditorGUILayout.Space();

        if (GUILayout.Button("Edit Script  (" + script.name + ".cs)"))
            AssetDatabase.OpenAsset(script);
    }

    // Unity's own ScriptableObject assets live outside Assets/Scripts, so the button stays off
    // for them and their inspectors keep the stock layout.
    private static bool TryGetScript(ScriptableObject asset, out MonoScript script)
    {
        script = null;

        if (asset == null)
            return false;

        script = MonoScript.FromScriptableObject(asset);

        if (script == null)
            return false;

        if (!AssetDatabase.GetAssetPath(script).StartsWith(ProjectScriptRoot))
            script = null;

        return script != null;
    }
}
