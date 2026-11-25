using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class CueLevelUpFontBuilder
{
    const string Folder="Assets/Resources/Gameplay/CueLevelUp";
    public static void Ensure()
    {
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder+"/Font.asset"))return;
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Resources/Gameplay","CueLevelUp");
        var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/Practice/Commons/Fonts/NanumBarunGothicBold.ttf");
        if(!source)throw new FileNotFoundException("Cue level-up bold font source is missing");
        var font=TMP_FontAsset.CreateFontAsset(source,100,12,GlyphRenderMode.SDFAA,512,512,AtlasPopulationMode.Dynamic);
        font.name="Cue level-up bold digits";
        if(!font.TryAddCharacters("CUE LEVEL UP Lv.0123456789",out string missing))throw new System.Exception("Missing cue level-up glyphs: "+missing);
        font.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font,Folder+"/Font.asset");
        font.material.name="Cue level-up font material";AssetDatabase.AddObjectToAsset(font.material,font);
        foreach(var texture in font.atlasTextures)if(texture){texture.name="Cue level-up glyph atlas";AssetDatabase.AddObjectToAsset(texture,font);}
        EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(Folder+"/Font.asset");
    }
}
