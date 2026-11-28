using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Sight.Surface.Consist;
using Assets.Scripts.Prack.BackSys.ChartData;
[InitializeOnLoad]
public static class WaitingPracticeButtonValidation
{
    static WaitingPracticeButtonValidation(){EditorApplication.update+=()=>{if(!File.Exists("Logs/waiting-practice-button.request")||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;File.Delete("Logs/waiting-practice-button.request");Run();};}
    [MenuItem("Tools/Gameplay/Validate Waiting Practice Button")]
    public static void Run()
    {
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");int checks=0;
        void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        try {
            Directory.CreateDirectory("Logs/LobbyLounge");var roots=scene.GetRootGameObjects();
            var lobby=roots.SelectMany(r=>r.GetComponentsInChildren<PnlLobby>(true)).Single();var host=lobby.transform;
            for(var t=host;t;t=t.parent)t.gameObject.SetActive(true);
            var main=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).First().transform;
            while(main&&main.name!="PnlMainSwipe")main=main.parent;if(main)main.gameObject.SetActive(false);
            TMP_Text Source(string path)=>host.Find(path).GetComponent<TMP_Text>();
            var title=Source("TxtTitle");var status=Source("TxtCounter");var self=Source("PnlPlayerSelf/TxtName");var other=Source("PnlPlayerOther/TxtName");var back=host.Find("BtnBack").GetComponent<Button>();
            title.text="캐롬카페";status.text="상대 대기";self.text="플레이어";other.text="";
            var view=LobbyLoungeView.Install(host,title,status,self,other,back);
            var button=view.GetComponentsInChildren<Button>(true).Single(b=>b.name=="WaitingPractice");
            var image=button.GetComponent<Image>();var rect=(RectTransform)button.transform;
            Check(image.sprite&&image.sprite.texture.name=="waiting-practice-button","Approved button artwork binding");
            Check(image.raycastTarget&&button.targetGraphic==image,"Button image must receive input");
            Check(rect.anchoredPosition.x>=1008&&rect.anchoredPosition.x+rect.sizeDelta.x<=1672,"Cancel gap and right edge");
            Check(-rect.anchoredPosition.y+rect.sizeDelta.y<=941,"Button bottom edge");
            Check(!button.gameObject.activeSelf,"Offline practice entry remains hidden");
            bool cancelled=false;back.onClick.AddListener(()=>cancelled=true);
            view.GetComponentsInChildren<Button>(true).Single(b=>b.name=="CancelWaiting").onClick.Invoke();
            Check(cancelled,"Existing cancel action remains connected");
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);
            var canvas=view.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var fixture=new SkillMatchData{HallName="캐롬카페",MatchBall=3,PrizeCoin=1000,TargetHit=10,MatchTotMin=15};
            foreach(var size in new[]{new Vector2Int(1672,941),new Vector2Int(1024,768)}) {
                var rt=new RenderTexture(size.x,size.y,24);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();view.Refresh(fixture);
                button.gameObject.SetActive(true);view.transform.Find("Design/PracticeLabel").gameObject.SetActive(true);
                foreach(var g in view.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
                var output=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);output.ReadPixels(new Rect(0,0,size.x,size.y),0,0);output.Apply();File.WriteAllBytes("Logs/LobbyLounge/practice-button-"+size.x+".png",output.EncodeToPNG());
                RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(output);UnityEngine.Object.DestroyImmediate(rt);checks++;
            }
            File.WriteAllText("Logs/LobbyLounge/practice-button-result.txt","PASS: "+checks+" checks; artwork, input, placement, offline visibility, cancel delegation and two aspect ratios");
        }catch(Exception e){File.WriteAllText("Logs/LobbyLounge/practice-button-result.txt",e.ToString());Debug.LogException(e);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
