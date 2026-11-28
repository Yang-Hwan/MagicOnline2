using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class CueAimPopupValidation
{
    static CueAimPopupValidation(){EditorApplication.update+=()=>{if(!File.Exists("Logs/cue-aim.request")||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete("Logs/cue-aim.request");Run();};}
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    [MenuItem("Tools/Magic Online/Validate Cue Aim Popup")]
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();RenderTexture target=null;
        try{
            var cameraGo=new GameObject("Preview camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.22f,.13f);
            var go=new GameObject("Preview canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var root=new GameObject("PointBig",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(go.transform,false);
            var ball=new GameObject("BallFrm",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();ball.SetParent(root,false);ball.sizeDelta=Vector2.one*700;
            CuePointGuide.Install(ball);
            int closed=0;float angle=0;Vector2 selected=Vector2.zero;
            var popup=root.gameObject.AddComponent<CueAimPopup>();popup.Configure(ball,p=>selected=p,()=>angle,v=>angle=v,()=>closed++);
            var backdrop=ModalBackdrop.Create(root,()=>closed++);
            Require(backdrop.GetComponent<Image>().color.a>=.65f,"Screen dimming missing");
            var buttons=root.GetComponentsInChildren<Button>(true);Require(buttons.Length==0,"Separated popups must not retain tab buttons");
            var slider=root.GetComponentInChildren<Slider>(true);
            target=new RenderTexture(1600,1000,24);camera.targetTexture=target;
            void Render(string name){Canvas.ForceUpdateCanvases();popup.SendMessage("LateUpdate");foreach(var graphic in root.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);}Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes("Logs/"+name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);RenderTexture.active=previous;}
            Render("cue-aim-contact");
            popup.ShowMode(true);Require(closed==0&&!ball.gameObject.activeSelf,"Tab switching closed popup or retained contact view");
            slider.value=45;Require(angle==45,"Slider did not apply angle");
            Require(root.localRotation==Quaternion.identity,"Angle view rotated with cue heading");Render("cue-aim-angle");
            var pointer=new PointerEventData(null){button=PointerEventData.InputButton.Left};
            var gesture=slider.GetComponent<CueAimGesture>();gesture.OnPointerDown(pointer);Require(closed==0,"Slider closed before release");gesture.OnPointerUp(pointer);Require(closed==1,"Slider release did not close");
            popup.ShowMode(false);Require(ball.gameObject.activeSelf,"Contact tab did not restore ball");
            ball.localRotation=Quaternion.Euler(0,0,55);
            var local=new Vector2(60,-70);pointer.position=camera.WorldToScreenPoint(ball.TransformPoint(local));
            // Screen-space camera is supplied by a real UI raycast in play; use it here through the raycast module.
            var events=new GameObject("Events",typeof(EventSystem));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events,scene);
            var data=new PointerEventData(events.GetComponent<EventSystem>()){position=pointer.position,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();go.GetComponent<GraphicRaycaster>().Raycast(data,hits);Require(hits.Count>0,"Contact has no UI hit");data.pointerPressRaycast=hits[0];
            var contact=ball.GetComponent<CueAimGesture>();contact.OnPointerDown(data);contact.OnPointerUp(data);Require(closed==2&&Vector2.Distance(selected,local)<1,"Rotated contact selection or release failed");
            File.WriteAllText("Logs/cue-aim-result.txt","PASS: dimming, separate popup modes, angle update, release dismissal, rotated contact input, rendered both popup modes.");
        }catch(Exception e){File.WriteAllText("Logs/cue-aim-result.txt",e.ToString());Debug.LogException(e);}
        finally{if(target)UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
