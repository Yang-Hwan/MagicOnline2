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
public static class LobbyLoungeValidation
{
    static LobbyLoungeValidation() { EditorApplication.update += () => {
        const string request = "Logs/lobby-lounge-validation.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request); Run();
    }; }
    [MenuItem("Tools/Magic Online/Validate Waiting Lounge")]
    public static void Run()
    {
        var scene = default(UnityEngine.SceneManagement.Scene);
        try {
            Directory.CreateDirectory("Logs/LobbyLounge");
            scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Consist.unity");
            var roots=scene.GetRootGameObjects();
            var lobby=roots.SelectMany(r=>r.GetComponentsInChildren<PnlLobby>(true)).Single();
            var host=lobby.transform;
            for(var t=host;t;t=t.parent)t.gameObject.SetActive(true);
            var main=roots.SelectMany(r=>r.GetComponentsInChildren<MainSwipe>(true)).First();
            var mainHost=main.transform;while(mainHost && mainHost.name!="PnlMainSwipe")mainHost=mainHost.parent;
            if(mainHost)mainHost.gameObject.SetActive(false);
            TMP_Text Source(string path)=>host.Find(path).GetComponent<TMP_Text>();
            var title=Source("TxtTitle");var status=Source("TxtCounter");var self=Source("PnlPlayerSelf/TxtName");var other=Source("PnlPlayerOther/TxtName");
            var back=host.Find("BtnBack").GetComponent<Button>();
            title.text="캐롬카페";status.text="상대 대기";self.text="플레이어";other.text="";
            var view=LobbyLoungeView.Install(host,title,status,self,other,back);
            if(!view)throw new Exception("Installation failed");
            var fixture=new SkillMatchData{HallName="캐롬카페",MatchBall=4,PrizeCoin=1000,TargetHit=3,MatchTotMin=5};
            var canvas=view.GetComponentInParent<Canvas>();
            var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
            camera.scene=scene;camera.enabled=false;camera.rect=new Rect(0,0,1,1);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            Render(1672,941,"waiting");
            var portraits=view.GetComponentsInChildren<Image>(true).Where(i=>i.name=="NeutralAvatar").ToArray();
            if(view.GetComponentInChildren<Assets.Scripts.Sight.Surface.Pavilion.RematchAvatarShape>(true))
                throw new Exception("Waiting portraits must use the lounge artwork's rectangular frames");
            if(portraits.Length!=2 || !portraits[0].sprite || portraits[1].gameObject.activeInHierarchy)
                throw new Exception("Expected own portrait and one empty waiting seat");
            var lights=view.GetComponentInChildren<WaitingLights>();
            if(!lights.Running || lights.raycastTarget)throw new Exception("Waiting lights state/input interception");
            int[] expected={0,1,2,1,0};
            for(int step=0;step<expected.Length;step++) {
                lights.Preview(step+.5f);
                if(lights.ActiveDot!=expected[step])throw new Exception("Waiting light direction/timing");
                if(step<3)Render(1672,941,"lights-"+step);
            }
            other.text="상대 플레이어";status.text="3";
            Render(1672,941,"countdown");
            if(!portraits[1].gameObject.activeInHierarchy || !portraits[1].sprite || portraits[0].sprite==portraits[1].sprite)
                throw new Exception("Joined player should have a distinct neutral portrait");
            if(!view.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="3초 후 경기가 시작됩니다."))throw new Exception("Countdown binding failed");
            if(lights.Running)throw new Exception("Waiting lights must stop during countdown");
            status.text="연결 끊김\n뒤로 가서 다시 시도해 주세요";
            Render(1672,941,"disconnected");
            status.text="상대 대기";other.text="";
            Render(1024,768,"waiting-4x3");Render(2560,1080,"waiting-ultrawide");
            bool delegated=false;back.onClick.AddListener(()=>delegated=true);
            view.GetComponentsInChildren<Button>().Single().onClick.Invoke();
            if(!delegated)throw new Exception("Cancel did not delegate to existing button");
            File.WriteAllText("Logs/LobbyLounge/result.json","{\"passed\":true,\"renders\":8,\"waitingLights\":true,\"cancelDelegation\":true,\"countdown\":true}");
            Debug.Log("Waiting lounge validation passed.");
            void Render(int width,int height,string name) {
                var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;
                Canvas.ForceUpdateCanvases();view.Refresh(fixture);
                foreach(var graphic in view.GetComponentsInChildren<Graphic>()){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();camera.Render();
                foreach(var text in view.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();if(text.isTextOverflowing)throw new Exception("Clipped text: "+text.name);}
                var old=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes("Logs/LobbyLounge/"+name+".png",image.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
            }
        } catch(Exception e){File.WriteAllText("Logs/LobbyLounge/error.txt",e.ToString());Debug.LogException(e);}
        finally{if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);}
    }
}
