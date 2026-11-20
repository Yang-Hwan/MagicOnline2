using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Sight.Surface.Consist;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[InitializeOnLoad]
public static class CoinShopValidation
{
    static CoinShopValidation(){EditorApplication.update+=()=>{if(!File.Exists("Logs/CoinShop/run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Logs/CoinShop/run.request");Run();};}
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Run()
    {
        var scene=default(UnityEngine.SceneManagement.Scene);RenderTexture target=null;checks=0;Directory.CreateDirectory("Logs/CoinShop");
        try{
            scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");var roots=scene.GetRootGameObjects();
            var menu=BottomMenuView.Install(roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single());menu.transform.parent.gameObject.SetActive(true);
            var canvas=menu.GetComponentInParent<Canvas>();var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            var events=roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).First();var raycaster=canvas.GetComponent<GraphicRaycaster>();
            void Invoke(string name,params object[] args)=>typeof(BottomMenuView).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,args);
            Transform Find(string name)=>menu.DesignRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
            void Prepare(){Canvas.ForceUpdateCanvases();menu.Resize();Canvas.ForceUpdateCanvases();foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}Canvas.ForceUpdateCanvases();camera.Render();}
            void Tap(string name){Prepare();var rect=(RectTransform)Find(name);Check(rect,"Missing "+name);var data=new PointerEventData(events){position=camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);Check(hits.Count>0,"No UI hit "+name);ExecuteEvents.Execute(ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject),data,ExecuteEvents.pointerClickHandler);}
            int Filter()=>(int)typeof(BottomMenuView).GetField("shopFilter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(menu);
            Tap("Nav4");Check(menu.CurrentPage==4 && Filter()==0,"Shop navigation defaults to gem tab");
            var tabs=(string[])typeof(BottomMenuView).GetField("ShopTabs",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Check(tabs.SequenceEqual(new[]{"보석","코인","큐대","캐릭터","메시지"}),"Shop uses five tabs with no All tab");
            Tap("코인");Check(Filter()==1,"Coin tab selects coin category");
            var fixture=new[]{new ShopProductData{Id=9001,Kind="CoinPack",Name="코인 10,000 팩",CoinAmount=10000,CoinBonus=1000,PriceGem=100,Revision=1,Purchasable=true},new ShopProductData{Id=9002,Kind="Cue",Name="Not a currency",Owned=true},new ShopProductData{Id=9003,Kind="GemPack",Name="보석 팩",GemAmount=100,GemBonus=10}};
            typeof(BottomMenuView).GetField("shopProducts",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,fixture);Invoke("DrawShopCards");Prepare();
            Check(Find("ProductCard9001") && !Find("ProductCard9002") && !Find("ProductCard9003"),"Coin tab filters equipment and gem packs");
            Check(Find("ProductPrice").GetComponent<TMP_Text>().text.Contains("11,000") && Find("ProductPrice").GetComponent<TMP_Text>().text.Contains("100"),"Coin card shows total grant and gem price");
            Check(Find("ProductImage").GetComponent<RawImage>().texture,"Coin fallback icon loads without custom thumbnail");
            var old=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1672,941,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1672,941),0,0);tex.Apply();File.WriteAllBytes("Logs/CoinShop/ui-coins.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=old;
            Tap("ProductDetails9001");Check(Find("ProductDescription").GetComponent<TMP_Text>().text.Contains("반복 구매") && Find("보석으로 구매"),"Coin product details explain repeat purchase and expose purchase action");
            Invoke("CloseModal");Tap("GemStore");Check(Filter()==0,"Gem balance shortcut selects gem tab");
            Tap("CoinStore");Check(Filter()==1,"Coin balance shortcut selects coin tab");
            menu.SelectPage(0);Tap("Nav4");Check(Filter()==0,"Reopening shop from navigation resets to gems");
            File.WriteAllText("Logs/CoinShop/ui-result.json","{\"passed\":true,\"checks\":"+checks+",\"realUiRaycasts\":true}");
        }catch(Exception e){File.WriteAllText("Logs/CoinShop/ui-failure.txt",e.ToString());Debug.LogException(e);}
        finally{if(target)UnityEngine.Object.DestroyImmediate(target);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
    }
}
