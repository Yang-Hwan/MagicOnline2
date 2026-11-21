using UnityEditor;
using UnityEngine;
using Assets.Scripts.Sight.Surface.Pavilion;

public static class ElasticControlAppearanceMenu
{
    [MenuItem("Tools/Gameplay/Elastic Controls/Use New Appearance")]
    public static void Enable() => SetAppearance(true);
    [MenuItem("Tools/Gameplay/Elastic Controls/Restore Original Appearance")]
    public static void Restore() => SetAppearance(false);

    static void SetAppearance(bool enabled)
    {
        var settings = Resources.Load<ElasticControlSettings>("Gameplay/ElasticControls/Settings");
        if (!settings) { Debug.LogError("Elastic control settings asset is missing."); return; }
        Undo.RecordObject(settings, "Change shot control appearance");
        settings.useNewAppearance = enabled;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log(enabled ? "New elastic controls enabled." : "Original shot controls restored.");
    }
}
