using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Consist;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class DailyCueShopValidation
{
    [Serializable] class Row {public string key,name;public int priceGem;}
    [Serializable] class Rows {public Row[] items;}
    static DailyCueShopValidation(){EditorApplication.update+=Tick;}
    static void Tick(){if(!File.Exists("Logs/DailyCues/run.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Logs/DailyCues/run.request");Run();}
    [MenuItem("Tools/Magic Online/Validate Daily Cue Shop")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs/DailyCues");int checks=0;
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");RenderTexture target=null;
        try{
            var rows=JsonUtility.FromJson<Rows>("{\"items\":"+File.ReadAllText("Docs/DailyCueSkins20261126.json")+"}").items;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            Check(rows.Length==39&&rows.Select(r=>r.key).Distinct().Count()==39,"39 unique daily designs");
            var products=rows.Select((r,i)=>new ShopProductData{Id=20000+i,Kind="Cue",Name=r.name+" 큐",Visual=DailyCueArt.Prefix+r.key,Thumbnail=DailyCueArt.Prefix+r.key,PriceGem=r.priceGem,Purchasable=true,CueAbility=new CueAbilitySettings{Initial=new[]{50,50,50,50,50},Maximum=new[]{100,100,100,100,100}}}).ToArray();
            foreach(var p in products){var image=DailyCueArt.Load(p.Visual);Check(image&&image.width>900&&image.height<image.width*.4f,"Tightly framed artwork: "+p.Name);Check(DailyCueArt.Load(p.Visual)==image,"Texture cache: "+p.Name);Check(PremiumCueDesign.Style(p.Visual)==PremiumCueStyle.Illustrated,"Match visual route: "+p.Name);}
            var roots=scene.GetRootGameObjects();var legacy=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var menu=BottomMenuView.Install(legacy);menu.transform.parent.gameObject.SetActive(true);
            void Set(string name,object value)=>typeof(BottomMenuView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,value);
            Set("shopFilter",2);menu.SelectPage(4);Set("shopProducts",products);
            var canvas=menu.GetComponentInParent<Canvas>();var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            for(int page=0;page<13;page++){
                Set("shopPage",page);typeof(BottomMenuView).GetMethod("DrawShopCards",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
                Canvas.ForceUpdateCanvases();menu.Resize();foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}Canvas.ForceUpdateCanvases();camera.Render();
                Check(menu.DesignRoot.GetComponentsInChildren<RawImage>().Count(i=>i.name=="ProductImage"&&i.texture)==3,"Three visible product artworks on page "+page);
                var old=RenderTexture.active;RenderTexture.active=target;var snapshot=new Texture2D(1672,941,TextureFormat.RGB24,false);snapshot.ReadPixels(new Rect(0,0,1672,941),0,0);snapshot.Apply();File.WriteAllBytes("Logs/DailyCues/shop-"+(page+1).ToString("00")+".png",snapshot.EncodeToPNG());UnityEngine.Object.DestroyImmediate(snapshot);RenderTexture.active=old;
            }
            File.WriteAllText("Logs/DailyCues/ui-result.json","{\"passed\":true,\"checks\":"+checks+",\"products\":39,\"pages\":13}");
        }catch(Exception e){File.WriteAllText("Logs/DailyCues/ui-failure.txt",e.ToString());Debug.LogException(e);}
        finally{if(target)UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
