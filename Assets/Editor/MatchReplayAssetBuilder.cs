using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class MatchReplayAssetBuilder : IPreprocessBuildWithReport
{
    const string Folder="Assets/Resources/Gameplay/Replay";
    public int callbackOrder=>-1000;
    public void OnPreprocessBuild(BuildReport report)=>Generate();
    [MenuItem("Tools/Magic Online/Generate Replay Asset IDs")]
    public static void Generate()
    {
        Directory.CreateDirectory(Folder);
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        var paths=new HashSet<string>(AssetDatabase.GetDependencies(scenes,true));
        foreach(string path in AssetDatabase.GetAllAssetPaths())if(path.StartsWith("Assets/",StringComparison.Ordinal)&&path.Contains("/Resources/")&&!path.StartsWith(Folder,StringComparison.Ordinal))paths.Add(path);
        Directory.CreateDirectory(Folder+"/Refs");
        var ids=new List<string>();var assets=new List<UnityEngine.Object>();var seen=new HashSet<UnityEngine.Object>();
        var names=new List<string>();var types=new List<string>();var groups=new List<string>();var slots=new List<int>();
        AssetDatabase.StartAssetEditing();
        try{foreach(string path in paths.OrderBy(p=>p)){
            if(path.StartsWith(Folder,StringComparison.Ordinal)||Directory.Exists(path))continue;
            // Scene/prefab objects are not asset references. Loading scene
            // contents here also causes threaded-scene-read errors in builds.
            var mainType=AssetDatabase.GetMainAssetTypeAtPath(path);string extension=Path.GetExtension(path).ToLowerInvariant();
            bool model=extension==".fbx"||extension==".obj"||extension==".dae"||extension==".blend"||extension==".3ds";
            if(!model&&(mainType==null||!(typeof(Texture).IsAssignableFrom(mainType)||typeof(Sprite).IsAssignableFrom(mainType)||typeof(Material).IsAssignableFrom(mainType)||typeof(Mesh).IsAssignableFrom(mainType)||typeof(TMP_FontAsset).IsAssignableFrom(mainType)||typeof(Shader).IsAssignableFrom(mainType)||typeof(Font).IsAssignableFrom(mainType))))continue;
            var references=new List<UnityEngine.Object>();string sourceGuid=AssetDatabase.AssetPathToGUID(path);if(string.IsNullOrEmpty(sourceGuid))continue;
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path)){
                if(!(obj is Texture||obj is Sprite||obj is Mesh||obj is Material||obj is TMP_FontAsset||obj is Shader)||!seen.Add(obj))continue;
                if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj,out string guid,out long local)){ids.Add(guid+":"+local);assets.Add(obj);names.Add(obj.name);types.Add(obj.GetType().FullName);groups.Add("Gameplay/Replay/Refs/"+sourceGuid);slots.Add(references.Count);references.Add(obj);}
            }
            if(references.Count>0){string resourcePath=Folder+"/Refs/"+sourceGuid+".asset";var entry=AssetDatabase.LoadAssetAtPath<ReplayAssetReference>(resourcePath);if(!entry){entry=ScriptableObject.CreateInstance<ReplayAssetReference>();AssetDatabase.CreateAsset(entry,resourcePath);}if(entry.objects==null||!entry.objects.SequenceEqual(references)){entry.objects=references.ToArray();EditorUtility.SetDirty(entry);}}
        }}finally{AssetDatabase.StopAssetEditing();}
        var keys=new List<string>();var templates=new List<GameObject>();var emitterKeys=new List<string[]>();var root=new GameObject("Replay particle templates");root.SetActive(false);
        try{
            void Add(ParticleSystem source){
                string key=MatchReplayAssets.ParticleKey(source);if(keys.Contains(key))return;
                var go=new GameObject(source.name);go.transform.SetParent(root.transform,false);
                var particle=go.AddComponent<ParticleSystem>();EditorUtility.CopySerialized(source,particle);
                var renderer=go.GetComponent<ParticleSystemRenderer>();EditorUtility.CopySerialized(source.GetComponent<ParticleSystemRenderer>(),renderer);
                var main=particle.main;main.playOnAwake=false;particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var copiedSub=particle.subEmitters;for(int i=copiedSub.subEmittersCount-1;i>=0;i--)copiedSub.RemoveSubEmitter(i);copiedSub.enabled=false;
                // Remap authored sub-emitter references after all templates exist.
                var sub=source.subEmitters;var references=new string[sub.subEmittersCount];for(int i=0;i<references.Length;i++){var emitter=sub.GetSubEmitterSystem(i);references[i]=emitter?MatchReplayAssets.ParticleKey(emitter):"";}keys.Add(key);templates.Add(go);emitterKeys.Add(references);
            }
            foreach(string path in scenes){var scene=EditorSceneManager.OpenPreviewScene(path);try{foreach(var go in scene.GetRootGameObjects())foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))Add(ps);}finally{EditorSceneManager.ClosePreviewScene(scene);}}
            foreach(string path in paths)if(path.EndsWith(".prefab",StringComparison.Ordinal)){var sourcePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(sourcePrefab)foreach(var ps in sourcePrefab.GetComponentsInChildren<ParticleSystem>(true))Add(ps);}
            for(int i=0;i<templates.Count;i++){
                var sub=templates[i].GetComponent<ParticleSystem>().subEmitters;
                for(int n=0;n<sub.subEmittersCount;n++){int at=keys.IndexOf(emitterKeys[i][n]);if(at>=0)sub.SetSubEmitterSystem(n,templates[at].GetComponent<ParticleSystem>());}
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Particles.prefab");
            var catalog=AssetDatabase.LoadAssetAtPath<MatchReplayAssets>(Folder+"/Assets.asset");if(!catalog){catalog=ScriptableObject.CreateInstance<MatchReplayAssets>();AssetDatabase.CreateAsset(catalog,Folder+"/Assets.asset");}
            catalog.ids=ids.ToArray();catalog.assetNames=names.ToArray();catalog.assetTypes=types.ToArray();catalog.groups=groups.ToArray();catalog.slots=slots.ToArray();catalog.particleKeys=keys.ToArray();catalog.particleResource="Gameplay/Replay/Particles";EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
            Debug.Log($"Replay asset IDs: {assets.Count}, particle templates: {keys.Count}");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
