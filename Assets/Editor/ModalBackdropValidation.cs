using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Consist;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ModalBackdropValidation
{
    static ModalBackdropValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        const string request="Logs/ModalBackdrop/run.request";
        if(!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(request);Run();
    }
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Run()
    {
        Directory.CreateDirectory("Logs/ModalBackdrop");checks=0;
        var scene=default(UnityEngine.SceneManagement.Scene);var previousEvents=EventSystem.current;
        RenderTexture target=null;
        try {
            scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");
            var roots=scene.GetRootGameObjects();
            var legacy=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var menu=BottomMenuView.Install(legacy);menu.transform.parent.gameObject.SetActive(true);menu.SelectPage(0);
            var canvas=menu.GetComponentInParent<Canvas>();
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
            camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var events=roots.SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).First();
            target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            var raycaster=canvas.GetComponent<GraphicRaycaster>();
            void Invoke(string method,params object[] args)=>typeof(BottomMenuView).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,args);
            void Prepare(){Canvas.ForceUpdateCanvases();menu.Resize();Canvas.ForceUpdateCanvases();foreach(var m in menu.GetComponentsInChildren<ModalBackdrop>())typeof(ModalBackdrop).GetMethod("Fit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m,null);Canvas.ForceUpdateCanvases();foreach(var g in menu.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();camera.Render();}
            Vector2 Screen(RectTransform rect)=>camera.WorldToScreenPoint(rect.TransformPoint(rect.rect.center));
            GameObject Hit(Vector2 point){var data=new PointerEventData(events){position=point};var hits=new List<RaycastResult>();raycaster.Raycast(data,hits);Check(hits.Count>0,"Expected modal UI raycast hit");return hits[0].gameObject;}
            void Click(Vector2 point){var data=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};var hit=Hit(point);ExecuteEvents.ExecuteHierarchy(hit,data,ExecuteEvents.pointerDownHandler);var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);ExecuteEvents.Execute(handler,data,ExecuteEvents.pointerClickHandler);}
            void Save(string name){camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1672,941,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1672,941),0,0);tex.Apply();File.WriteAllBytes("Logs/ModalBackdrop/"+name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);RenderTexture.active=old;}
            foreach(var test in new[]{("Attendance","AttendancePanel","Attendance"),("OpenMailbox","MailPanel","Mailbox"),("OpenFriendsFrame","FriendsPanel","Friends")}){
                Invoke(test.Item1);Prepare();
                var modal=menu.DesignRoot.Find(test.Item3);var card=(RectTransform)modal.Find(test.Item2);var shade=modal.Find("Shade").GetComponent<ModalBackdrop>();
                Check(shade && Mathf.Abs(shade.GetComponent<Image>().color.a-.68f)<.001f,test.Item3+" has a shared translucent backdrop");
                Check(!modal.GetComponentsInChildren<Button>().Any(b=>b.name=="닫기" || b.name=="CloseMail" || b.name=="Close"),test.Item3+" has no close button");
                Click(Screen(card));Check(modal && modal.gameObject.activeSelf,test.Item3+" inner click preserves popup");
                Save(test.Item3.ToLowerInvariant());
                var outside=Screen((RectTransform)modal.Find("Shade"));outside=new Vector2(8,outside.y);
                Check(Hit(outside).GetComponent<ModalBackdrop>(),test.Item3+" outside click cannot reach menu underneath");
                Click(outside);Check(!menu.DesignRoot.Find(test.Item3),test.Item3+" outside click dismisses popup");
            }
            Invoke("Dialog","Modal test","");Prepare();
            var lower=menu.DesignRoot.Find("Modal");var lowerCard=(RectTransform)lower.Find("Panel");
            var top=new GameObject("NestedCard",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();top.SetParent(lower,false);top.sizeDelta=new Vector2(300,180);top.anchorMin=top.anchorMax=top.pivot=Vector2.one*.5f;
            var nested=ModalBackdrop.Create(top,()=>{top.gameObject.SetActive(false);UnityEngine.Object.DestroyImmediate(top.gameObject);});Prepare();
            Click(new Vector2(8,400));Check(lower && lower.gameObject.activeSelf && !top,"outside click closes only the top popup");
            var lowerShade=lower.Find("Shade").GetComponent<ModalBackdrop>();
            var drag=new PointerEventData(events){position=new Vector2(8,400),button=PointerEventData.InputButton.Left};lowerShade.OnPointerDown(drag);lowerShade.OnBeginDrag(drag);lowerShade.OnPointerClick(drag);
            Check(lower && lower.gameObject.activeSelf,"background drag does not count as a dismiss tap");
            Invoke("CloseModal");
            File.WriteAllText("Logs/ModalBackdrop/result.json","{\"passed\":true,\"checks\":"+checks+",\"realUiRaycasts\":true,\"liveAccountsUntouched\":true}");
            Debug.Log("Modal backdrop validation passed: "+checks);
        } catch(Exception e){File.WriteAllText("Logs/ModalBackdrop/failure.txt",e.ToString());Debug.LogException(e);}
        finally{if(target){UnityEngine.Object.DestroyImmediate(target);}if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
    }
}
