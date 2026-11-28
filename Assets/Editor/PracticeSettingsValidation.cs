using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Consist;
[InitializeOnLoad]
public static class PracticeSettingsValidation
{
    static PracticeSettingsValidation(){EditorApplication.update+=()=>{if(!File.Exists("Logs/practice-settings.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Logs/practice-settings.request");Run();};}
    [MenuItem("Tools/Gameplay/Validate Practice Settings")]
    public static void Run()
    {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");int count=0;
        void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
        try {
            var roots=scene.GetRootGameObjects();
            var legacy=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).Single();
            var view=BottomMenuView.Install(legacy);view.transform.parent.gameObject.SetActive(true);view.SelectPage(1);
            Check(view.GetComponentsInChildren<Button>().Count(b=>b.name.Length==7&&b.name.StartsWith("Single"))==3,"Exactly three single-player menu buttons");
            view.OpenPracticeSettings();
            var modal=view.DesignRoot.Find("Practice Settings Modal");
            Check(Mathf.Abs(modal.Find("Shade").GetComponent<Image>().color.a-.9f)<.001f,"90 percent backdrop");
            Check(view.PracticeSelectionReady&&view.PracticeBallSelection==4&&view.PracticeComputerSelection==false,"Default four-ball self selections");
            var start=modal.GetComponentsInChildren<Button>().Single(b=>b.name=="Start Practice");Check(start.interactable,"Start enabled for default selections");
            view.SelectPracticeOption(0);Check(start.interactable&&view.PracticeComputerSelection==false,"Changing ball count retains default opponent");
            view.SelectPracticeOption(3);Check(start.interactable&&view.PracticeBallSelection==3&&view.PracticeComputerSelection==true,"Three-ball computer selection");
            view.SelectPracticeOption(1);view.SelectPracticeOption(2);Check(view.PracticeBallSelection==4&&view.PracticeComputerSelection==false,"Four-ball self selection");
            foreach(int balls in new[]{3,4})foreach(bool computer in new[]{false,true}) {
                var config=PracticeSceneFlow.PracticeConfiguration(balls,computer);
                Check(config.BallCount==balls&&config.HasComputerOpponent==computer,"Configuration combination");
            }
            var configure=typeof(PracticeSceneFlow).GetMethod("ConfigureWaitingPractice",BindingFlags.NonPublic|BindingFlags.Static);
            var oldConfig=PracticeSceneFlow.Configuration;
            foreach(int balls in new[]{3,4}) {
                configure.Invoke(null,new object[]{balls});Check(PracticeSceneFlow.Configuration.BallCount==balls&&!PracticeSceneFlow.Configuration.HasComputerOpponent,"Waiting practice arena ball count and self opponent");
            }
            typeof(PracticeSceneFlow).GetProperty("Configuration").SetValue(null,oldConfig);
            Check(!modal.GetComponentsInChildren<Button>().Any(b=>b.name.Contains("Close")||b.name.Contains("Cancel")),"No popup close button");
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();camera.scene=scene;camera.enabled=false;
            var canvas=view.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var target=new RenderTexture(1672,941,24);camera.targetTexture=target;
            Canvas.ForceUpdateCanvases();view.Resize();Canvas.ForceUpdateCanvases();
            var backdrop=modal.Find("Shade").GetComponent<ModalBackdrop>();
            typeof(ModalBackdrop).GetMethod("Fit",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(backdrop,null);
            foreach(var graphic in view.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}
            Canvas.ForceUpdateCanvases();camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1672,941,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1672,941),0,0);image.Apply();File.WriteAllBytes("Logs/practice-settings-render.png",image.EncodeToPNG());
            var outside=new UnityEngine.EventSystems.PointerEventData(null){position=new Vector2(-10000,-10000),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            backdrop.OnPointerDown(outside);backdrop.OnPointerClick(outside);
            Check(!view.DesignRoot.Find("Practice Settings Modal"),"Outside click closes popup");
            foreach(var graphic in view.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,1672,941),0,0);image.Apply();File.WriteAllBytes("Logs/single-three-render.png",image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);

            File.WriteAllText("Logs/practice-settings-result.txt","PASS: "+count+" checks; menu, independent selections, four configurations, waiting practice, modal dimming and render");
        }catch(Exception e){File.WriteAllText("Logs/practice-settings-result.txt",e.ToString());Debug.LogException(e);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
