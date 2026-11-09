using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Sight.Surface.Pavilion;
using Assets.Scripts.Sight.Surface.Consist;

[InitializeOnLoad]
public static class RematchLoungeValidation
{
    static RematchLoungeValidation() { EditorApplication.update += () => {
        const string request = "Logs/rematch-lounge-validation.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try { File.Delete(request); } catch (IOException) { return; }
        Run();
    }; }
    [MenuItem("Tools/Magic Online/Validate Rematch Lounge")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        Directory.CreateDirectory("Logs/RematchLounge");
        try {
            var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.enabled = false;
            var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            camera.orthographic = true; camera.transform.position = new Vector3(0,0,-10);
            var state = new RematchLoungeView.State { Arena="캐롬카페", PrizeCoin=1710, Result="승리했습니다", Score="3 : 2", SelfName="타이거우즈20", OtherName="상대 플레이어", SelfInfo="3연타", OtherInfo="04:32", SelfVote="선택 대기", OtherVote="재대결 요청!", Status="같은 상대와 한 판 더 도전하시겠어요?", Settlement="경기 결과 정산이 완료되었습니다.", CanRematch=true, CanSave=true };
            int again=0,save=0,leave=0;
            var view=RematchLoungeView.Install(canvas.transform,()=>state,()=>again++,()=>save++,()=>leave++);
            Render(1672,941,"result");
            var portraits=view.GetComponentsInChildren<Image>().Where(i=>i.name=="NeutralAvatar").ToArray();
            if(portraits.Length!=2 || !portraits[0].sprite || !portraits[1].sprite || portraits[0].sprite==portraits[1].sprite)
                throw new Exception("Missing neutral result portraits");
            var hostPortrait=portraits[0].sprite;
            state.LocalAvatarSeat=1;view.Refresh();
            if(portraits[1].sprite!=hostPortrait || portraits[0].sprite==hostPortrait)throw new Exception("Guest seat mapping failed");
            state.LocalAvatarSeat=0;view.Refresh();
            Button Find(string name)=>view.GetComponentsInChildren<Button>().Single(b=>b.name==name);
            Find("Rematch").onClick.Invoke();Find("SaveReplay").onClick.Invoke();Find("Leave").onClick.Invoke();
            if(again!=1||save!=1||leave!=1)throw new Exception("Action delegation failed");
            state.SelfVote="재대결 요청";state.Waiting=true;state.CanRematch=false;state.Status="상대의 재대결 응답을 기다리고 있습니다.";
            Render(1672,941,"requested");Find("Rematch").onClick.Invoke();
            if(again!=1)throw new Exception("Waiting/disabled button failed");
            state.OtherVote="재대결 불가";state.Waiting=false;state.Status="상대와 재대결할 수 없습니다.";
            Render(1672,941,"opponent-left");
            state.Result="패배하였습니다"; state.Score="2 : 2"; Render(1672,941,"defeat");
            state.PrizeCoin=0; state.Result="비겼습니다"; Render(1672,941,"draw");
            state.Result="무승부"; Render(1672,941,"draw-label");
            state.PrizeCoin=null; state.Result="경기 결과 확인 필요"; Render(1672,941,"unconfirmed");
            state.OtherVote="재대결 요청";state.Status="3초 후 재대결을 시작합니다.";
            Render(1024,768,"countdown-4x3");Render(2560,1080,"countdown-ultrawide");
            File.WriteAllText("Logs/RematchLounge/result.json","{\"passed\":true,\"renders\":9,\"actionDelegation\":true,\"requestGuard\":true,\"resultVariants\":true,\"winnerBadges\":true}");
            void Render(int width,int height,string name) {
                var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;
                camera.orthographicSize=height/2f;camera.aspect=(float)width/height;
                ((RectTransform)canvas.transform).sizeDelta=new Vector2(width,height);
                Canvas.ForceUpdateCanvases();view.Refresh();
                var images=view.GetComponentsInChildren<RawImage>(true);
                var amount=view.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="PrizeAmount");
                string expected=state.PrizeCoin.HasValue?state.PrizeCoin.Value.ToString("N0",System.Globalization.CultureInfo.InvariantCulture):"—";
                if(amount.text!=expected) throw new Exception("Prize amount mismatch");
                bool selfWon=state.Result.Contains("승리"), otherWon=state.Result.Contains("패배");
                if(images.Single(i=>i.name=="SelfWinner").gameObject.activeSelf!=selfWon ||
                   images.Single(i=>i.name=="OtherWinner").gameObject.activeSelf!=otherWon)
                    throw new Exception("Winner badge shown on wrong side: "+state.Result);
                foreach(var g in view.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();camera.Render();
                foreach(var t in view.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();if(t.isTextOverflowing)throw new Exception("Clipped text: "+t.name);}
                var old=RenderTexture.active;RenderTexture.active=rt;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                File.WriteAllBytes("Logs/RematchLounge/"+name+".png",image.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
            }
        }catch(Exception e){File.WriteAllText("Logs/RematchLounge/error.txt",e.ToString());Debug.LogException(e);}
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
}
