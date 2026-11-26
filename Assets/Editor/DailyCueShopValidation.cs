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
using UnityEngine.EventSystems;
using TMPro;

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
        Directory.CreateDirectory("Logs/DailyCues");File.Delete("Logs/DailyCues/carousel-failure.txt");File.Delete("Logs/DailyCues/carousel-result.json");int checks=0;
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");RenderTexture target=null;
        try{
            var rows=JsonUtility.FromJson<Rows>("{\"items\":"+File.ReadAllText("Docs/DailyCueSkins20261126.json")+"}").items;
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            Check(rows.Length==39&&rows.Select(r=>r.key).Distinct().Count()==39,"39 unique daily designs");
            var products=rows.Select((r,i)=>new ShopProductData{Id=20000+i,Kind="Cue",Name=r.name+" 큐",Visual=DailyCueArt.Prefix+r.key,Thumbnail=DailyCueArt.Prefix+r.key,PriceGem=r.priceGem,Purchasable=true,CueAbility=new CueAbilitySettings{Initial=new[]{50,50,50,50,50},Maximum=new[]{100,100,100,100,100}}}).ToArray();
            void ClearArt()=>typeof(DailyCueArt).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            ClearArt();var timing=System.Diagnostics.Stopwatch.StartNew();
            foreach(var p in products){var image=DailyCueArt.Load(p.Visual);Check(image&&image.width>900&&image.height<image.width*.4f,"Tightly framed artwork: "+p.Name);Check(DailyCueArt.Load(p.Visual)==image,"Texture cache: "+p.Name);Check(PremiumCueDesign.Style(p.Visual)==PremiumCueStyle.Illustrated,"Match visual route: "+p.Name);}
            timing.Stop();long fullCatalogArtMs=timing.ElapsedMilliseconds;ClearArt();
            var roots=scene.GetRootGameObjects();var legacy=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var menu=BottomMenuView.Install(legacy);menu.transform.parent.gameObject.SetActive(true);
            void Set(string name,object value)=>typeof(BottomMenuView).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,value);
            Set("shopFilter",2);menu.SelectPage(4);Set("shopProducts",products);
            var canvas=menu.GetComponentInParent<Canvas>();var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            void Render(string name){
                Canvas.ForceUpdateCanvases();menu.Resize();
                foreach(var scrolling in menu.DesignRoot.GetComponentsInChildren<ScrollRect>())scrolling.SendMessage("LateUpdate");
                foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=target;
                var snapshot=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                snapshot.ReadPixels(new Rect(0,0,target.width,target.height),0,0);snapshot.Apply();
                File.WriteAllBytes("Logs/DailyCues/"+name+".png",snapshot.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(snapshot);RenderTexture.active=old;
            }
            void Draw()=>typeof(BottomMenuView).GetMethod("DrawShopCards",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
            products[1].Owned=true;products[2].Purchasable=false;
            timing.Restart();Draw();timing.Stop();long firstWindowMs=timing.ElapsedMilliseconds;Render("carousel-first");
            var scroll=menu.DesignRoot.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="ShopCueViewport");
            Check(scroll.horizontal&&!scroll.vertical&&scroll.viewport.GetComponent<RectMask2D>(),"Clipped horizontal carousel");
            Check(scroll.viewport.rect.width>1600,"Full-width catalog");
            Check(scroll.content.childCount==3,"Only first three cards loaded initially");
            Check(scroll.content.GetComponentsInChildren<RawImage>().Count(i=>i.name=="ProductImage"&&i.texture)==3,"Only first window artwork loaded");
            Check(Mathf.Abs(scroll.content.rect.width-(1608+38*1032))<1,"Virtual content preserves full catalog extent");
            var first=scroll.content.GetChild(0);
            Check(first.Find("BuyCue").GetComponent<Button>().interactable,"Available purchase action");
            Check(!scroll.content.GetChild(1).Find("BuyCue").GetComponent<Button>().interactable,"Owned purchase disabled");
            Check(!scroll.content.GetChild(2).Find("BuyCue").GetComponent<Button>().interactable,"Unavailable purchase disabled");
            for(int i=0;i<5;i++)Check(first.Find("CueStatValue"+i).GetComponent<TMP_Text>().text==(i==3?"30초":"50%"),"Live stat "+i);
            Check(first.Find("CueCardBackground").GetComponent<Image>().color.a<.8f,"Translucent product card");
            menu.DesignRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="NextCue").onClick.Invoke();
            Check(Mathf.Abs(scroll.horizontalNormalizedPosition-1f/38)<.001f,"Next product navigation");
            menu.DesignRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="PreviousCue").onClick.Invoke();
            Check(scroll.horizontalNormalizedPosition<.001f,"Previous product navigation");
            var events=roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
            scroll.horizontalNormalizedPosition=.5f;
            var wheel=new PointerEventData(events){scrollDelta=new Vector2(0,-5)};
            scroll.OnScroll(wheel);
            Check(Mathf.Abs(scroll.horizontalNormalizedPosition-.5f)>.001f,"Mouse wheel moves horizontally");
            var center=RectTransformUtility.WorldToScreenPoint(camera,scroll.viewport.TransformPoint(new Vector3(804,-100,0)));
            var drag=new PointerEventData(events){button=PointerEventData.InputButton.Left,position=center};
            scroll.OnInitializePotentialDrag(drag);scroll.OnBeginDrag(drag);float before=scroll.content.anchoredPosition.x;
            drag.position=center+Vector2.left*300;scroll.OnDrag(drag);scroll.OnEndDrag(drag);
            Check(scroll.content.anchoredPosition.x<before,"Pointer drag moves carousel");
            scroll.StopMovement();scroll.horizontalNormalizedPosition=.5f;Render("carousel-middle");
            scroll.horizontalNormalizedPosition=1;Render("carousel-last");
            Check(Mathf.Abs(scroll.horizontalNormalizedPosition-1)<.001f,"Last product reachable");
            Draw();
            scroll=menu.DesignRoot.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="ShopCueViewport");
            Check(Mathf.Abs(scroll.horizontalNormalizedPosition-1)<.001f,"Scroll position survives redraw");
            // Settle the newly recreated ScrollRect at its initial position before
            // simulating subsequent frames of user scrolling.
            Canvas.ForceUpdateCanvases();scroll.SendMessage("LateUpdate");
            for(int i=0;i<products.Length;i++){
                scroll.horizontalNormalizedPosition=i/38f;scroll.SendMessage("LateUpdate");
                Check(scroll.content.Find("ShopCue"+products[i].Id),"Product reachable in virtual window: "+products[i].Name);
                Check(scroll.content.childCount<=5,"At most five live cards at index "+i);
            }
            typeof(BottomMenuView).GetMethod("ShowShopProduct",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{products[0]});
            Render("carousel-detail");
            var modal=menu.DesignRoot.Find("Modal");
            Check(modal.Find("Panel/Background").GetComponent<Image>().color.a<1,"Translucent detail panel");
            Check(modal.Find("Shade").GetComponent<Image>().color.a<=.5f,"Background remains visible");
            modal.GetComponentsInChildren<Button>().Single(b=>b.name=="CloseCueDetails").onClick.Invoke();
            // Empty catalogs and old products without growth settings must remain usable.
            Set("shopProducts",new ShopProductData[0]);Draw();
            Check(!menu.DesignRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="NextCue"),"Empty catalog navigation");
            Set("shopProducts",new[]{new ShopProductData{Id=30000,Kind="Cue",Name="기본 큐대",Power=10,ShotSeconds=5,Purchasable=true}});Draw();
            Check(menu.DesignRoot.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="CueStatValue0"&&t.text=="+10%"),"Legacy cue values");
            File.WriteAllText("Logs/DailyCues/carousel-result.json","{\"passed\":true,\"checks\":"+checks+",\"products\":39,\"initialCards\":3,\"maxLiveCards\":5,\"fullCatalogArtMs\":"+fullCatalogArtMs+",\"coldFirstWindowMs\":"+firstWindowMs+"}");
        }catch(Exception e){File.WriteAllText("Logs/DailyCues/carousel-failure.txt",e.ToString());Debug.LogException(e);}
        finally{if(target)UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
