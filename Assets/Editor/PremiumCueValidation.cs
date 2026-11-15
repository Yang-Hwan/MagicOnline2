using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PremiumCueValidation
{
    [MenuItem("Tools/Magic Online/Validate Premium Cues")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode first");
        var scene=EditorSceneManager.NewPreviewScene();int checks=0;
        try
        {
            Directory.CreateDirectory("Logs/PremiumCues");
            var cameraRoot=new GameObject("Preview camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=450;camera.aspect=1600f/900;
            camera.transform.position=new Vector3(0,0,-10);camera.farClipPlane=5000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.025f,.055f);
            var canvasRoot=new GameObject("Cue previews",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot,scene);
            var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;canvasRoot.GetComponent<RectTransform>().sizeDelta=new Vector2(1600,900);
            string[] keys={"ThunderGod","Nebula","CrimsonFlame"};var previews=new PremiumCuePreview[3];
            for(int i=0;i<3;i++)
            {
                string key="Gameplay/PremiumCues/"+keys[i];var texture=Resources.Load<Texture2D>(key);Check(texture && texture.width>1000,"full quality transparent artwork loads: "+keys[i]);
                var node=new GameObject(keys[i],typeof(RectTransform),typeof(RawImage));node.transform.SetParent(canvasRoot.transform,false);var image=node.GetComponent<RawImage>();image.texture=texture;image.raycastTarget=false;
                image.rectTransform.sizeDelta=new Vector2(1400,1400*texture.height/(float)texture.width);image.rectTransform.anchoredPosition=new Vector2(0,280-i*280);
                previews[i]=PremiumCuePreview.Attach(image,key);previews[i].PreviewTime=0;
                Check(previews[i] && !previews[i].raycastTarget,"shop effect cannot block purchase clicks: "+keys[i]);
            }
            string first=Render("shop-0");foreach(var preview in previews){preview.PreviewTime=.7f;preview.SetVerticesDirty();}
            Check(first!=Render("shop-1"),"shop effects visibly animate between frames");
            var geo=new CueEffectGeometry();var random=UnityEngine.Random.state;
            foreach(var style in new[]{PremiumCueStyle.Thunder,PremiumCueStyle.Nebula,PremiumCueStyle.Flame})
            {
                geo.Build(style,.2f,.6f);var initial=geo.Vertices.ToArray();Check(initial.Length>100 && initial.Length<6000,"effect geometry is bounded: "+style);
                geo.Build(style,.8f,.6f);Check(!initial.SequenceEqual(geo.Vertices),"effect moves: "+style);
            }
            Check(random.Equals(UnityEngine.Random.state),"cosmetic effects leave physics random state untouched");
            canvasRoot.SetActive(false);var worlds=new PremiumCueWorld[3];
            for(int i=0;i<3;i++)
            {
                var slider=new GameObject("Slider"+i);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(slider,scene);
                var cue3d=new GameObject("Cue3D");cue3d.transform.SetParent(slider.transform,false);
                var standard=new GameObject("Standard3DCue",typeof(MeshFilter),typeof(MeshRenderer));standard.transform.SetParent(cue3d.transform,false);
                var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-.01f,-.01f,0),new Vector3(.01f,.01f,1)};mesh.RecalculateBounds();standard.GetComponent<MeshFilter>().sharedMesh=mesh;
                standard.transform.localScale=Vector3.one*1100;standard.transform.localRotation=Quaternion.Euler(0,-90,0);standard.transform.localPosition=new Vector3(550,280-i*280,0);
                worlds[i]=PremiumCueWorld.Equip(slider.transform,"Gameplay/PremiumCues/"+keys[i]);
                Check(worlds[i] && !standard.GetComponent<MeshRenderer>().enabled && !worlds[i].GetComponentInChildren<Collider>(),"game cue replaces visuals without changing physics: "+keys[i]);
                typeof(PremiumCueWorld).GetField("cameraView",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(worlds[i],camera);worlds[i].UpdateGeometry(0);
                Check(worlds[i].GetComponentInChildren<MeshRenderer>().bounds.extents.y>10,"cue billboard faces the rendering camera: "+keys[i]);
            }
            string worldFirst=Render("match-0");foreach(var cue in worlds)cue.UpdateGeometry(.7f);Check(worldFirst!=Render("match-1"),"match effects visibly animate between frames");
            foreach(var cue in worlds)cue.Remove();Check(!worlds.Any(c=>c && c.gameObject.activeSelf),"switching equipment immediately removes old cue effects");
            Check(PremiumCueDesign.Style("Gameplay/ShopCues/Inferno")==PremiumCueStyle.None,"existing cue appearance remains on original path");
            File.WriteAllText("Logs/PremiumCues/validation.json","{\"Ok\":true,\"Checks\":"+checks+",\"Renders\":4}");Debug.Log("[PremiumCueValidation] PASS "+checks+" checks, 4 renders");
            void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
            string Render(string name)
            {
                foreach(var p in previews)if(p && p.gameObject.activeInHierarchy){p.SetAllDirty();p.Rebuild(CanvasUpdate.PreRender);p.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();var target=new RenderTexture(1600,900,24);camera.targetTexture=target;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();var png=image.EncodeToPNG();File.WriteAllBytes("Logs/PremiumCues/"+name+".png",png);
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(png));
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);return hash;
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
