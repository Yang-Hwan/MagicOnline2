using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad] public static class ShotClockAlarmValidation
{
    const string Request="Logs/shot-clock-alarm.request";
    [Serializable] sealed class Report {public bool passed;public int checks,bytes;public string error="";}
    static ShotClockAlarmValidation(){EditorApplication.update+=()=>{if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Request);EditorApplication.delayCall+=Run;};}
    public static void Run()
    {
        var report=new Report();Scene scene=default;MatchReplayStateCapture capture=null;MatchReplayStateDisplay display=null;RenderTexture target=null;Material material=null;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);report.checks++;}
        try{
            MatchReplayAssetBuilder.Generate();scene=EditorSceneManager.NewPreviewScene();
            GameObject Node(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
            var table=Node("Table").transform;var cloth=Node("ClothSpace").transform;cloth.SetParent(table,false);cloth.localScale=new Vector3(4,1,2);
            var board=GameObject.CreatePrimitive(PrimitiveType.Quad);SceneManager.MoveGameObjectToScene(board,scene);board.name="Static table cloth";board.transform.SetParent(table,false);board.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(90,0,0));board.transform.localScale=new Vector3(4,2,1);material=new Material(Shader.Find("Unlit/Color"));material.color=new Color(.03f,.25f,.12f);board.GetComponent<MeshRenderer>().sharedMaterial=material;
            var camera=Node("Alarm camera").AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.orthographic=true;camera.orthographicSize=1.55f;camera.aspect=16f/9;camera.transform.SetPositionAndRotation(Vector3.up*6,Quaternion.Euler(90,0,0));camera.enabled=false;camera.backgroundColor=new Color(.025f,.025f,.035f);camera.clearFlags=CameraClearFlags.SolidColor;target=new RenderTexture(1280,720,24);camera.targetTexture=target;
            var owner=Node("Alarm fixture HUD");owner.AddComponent<RectTransform>();owner.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var hud=owner.AddComponent<MatchHudView>();var alarm=ShotClockAlarm.Install(hud,true);alarm.SetView(camera,cloth);Canvas.ForceUpdateCanvases();
            void Sample(float seconds,bool running=true,bool self=true){hud.SetTime(1-seconds/30,30,running);alarm.Present(seconds,running,self);Canvas.ForceUpdateCanvases();}
            Directory.CreateDirectory("Logs/ShotClockAlarm");
            void Render(string name,float seconds){var canvas=alarm.OverlayCanvas;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();Sample(seconds);camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/ShotClockAlarm/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;Canvas.ForceUpdateCanvases();Sample(seconds);}
            Check(ShotClockAlarm.StageFor(10.01f,true)==-1&&ShotClockAlarm.StageFor(10,true)==0&&ShotClockAlarm.StageFor(8,true)==1&&ShotClockAlarm.StageFor(4,true)==2,"Clock thresholds are exactly 10/8/4 seconds");
            Check(ShotClockAlarm.StageFor(0,true)==-1&&ShotClockAlarm.StageFor(4,false)==-1&&ShotClockAlarm.StageFor(float.NaN,true)==-1,"Timeout, disabled timer and invalid values hide alarm");
            Check(alarm.OverlayCanvas.sortingOrder==32766&&alarm.OverlayCanvas.renderMode==RenderMode.ScreenSpaceOverlay,"Clock is visible above normal HUD");
            Check(alarm.GetComponentsInChildren<Image>(true).All(i=>!i.raycastTarget)&&!alarm.GetComponent<GraphicRaycaster>(),"Alarm never blocks cue or UI input");
            var ballPositions=new[]{new Vector3(-.4f,0,0),new Vector3(.3f,0,.2f),new Vector3(.6f,0,-.3f),new Vector3(.9f,0,.3f)};var radii=new[]{.04f,.04f,.04f,.04f};
            Check(ShotClockAlarm.TryChoose(cloth,ballPositions,radii,ballPositions[0],Vector3.forward,42,out var p),"Empty position is found among four balls and aim corridor");
            var world=cloth.TransformPoint(new Vector3(p.x,0,p.y));Check(ballPositions.All(b=>Vector3.Distance(b,world)>.32f),"Chosen alarm footprint clears all balls");
            Check(ShotClockAlarm.TryChoose(cloth,ballPositions,radii,ballPositions[0],Vector3.forward,42,out var same)&&p==same,"Placement seed is deterministic across clients");
            Check(ShotClockAlarm.TryChoose(cloth,ballPositions,radii,ballPositions[0],Vector3.forward,43,out var other)&&p!=other,"Different turns can choose different empty positions");
            var blocked=new[]{Vector3.zero};Check(!ShotClockAlarm.TryChoose(cloth,blocked,new[]{10f},Vector3.zero,Vector3.zero,1,out _),"Crowded table uses the bounded no-space fallback");
            Sample(12);Check(!alarm.Body.enabled,"Clock is absent before 10 seconds");
            Sample(10);var sprite=alarm.Body.sprite;var position=alarm.Body.rectTransform.anchoredPosition;var anchor=alarm.Anchor;int bells=alarm.BellCount;Render("10-bored",10);
            Sample(9.35f);Check(alarm.Body.sprite==sprite&&alarm.Body.rectTransform.anchoredPosition==position&&alarm.Body.rectTransform.localScale==Vector3.one,"Bored body is completely stationary while eyelids animate");
            Check(alarm.BellCount==bells,"Bored entry chime is not repeated every frame");
            Sample(7.9f);Render("08-annoyed",7.9f);Check(alarm.Stage==1&&alarm.Body.sprite.texture.name=="AlarmAnnoyed","Eight-second stage uses annoyed sprite deformation");
            float weak=0;for(int i=0;i<8;i++){Sample(7.9f-i*.03f);weak=Mathf.Max(weak,Mathf.Abs(alarm.Body.rectTransform.localScale.x-1));}
            Sample(3.9f);Render("04-angry",3.9f);Check(alarm.Stage==2&&alarm.Body.sprite.texture.name=="AlarmAngry","Four-second stage uses angry sprite deformation");
            float strong=0;for(int i=0;i<8;i++){Sample(3.9f-i*.03f);strong=Mathf.Max(strong,Mathf.Abs(alarm.Body.rectTransform.localScale.x-1));}
            Check(strong>weak*1.5f&&strong<.02f,"Angry body shudder is stronger but remains restrained");
            Check(alarm.Anchor==anchor,"Warning never jumps to a new random position during the turn");
            hud.StopTimer();Check(!alarm.Body.enabled&&alarm.Stage==-1,"Shot-clock stop hides alarm immediately");
            Sample(0);Check(!alarm.Body.enabled,"Timeout does not leave a clock behind");
            alarm.ResetTurn();Sample(10);Check(alarm.Stage==0,"A new turn can show a fresh warning");
            Sample(8,false);Check(!alarm.Body.enabled,"Unlimited practice does not show warning");
            // Exercise remote anchor delivery without logging in or sending RPCs.
            var peerOwner=Node("Remote HUD");peerOwner.AddComponent<RectTransform>();peerOwner.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var peerHud=peerOwner.AddComponent<MatchHudView>();var peer=ShotClockAlarm.Install(peerHud,false);peer.SetView(camera,cloth);peerHud.SetTime(.7f,30,true);peer.ReceiveAnchor(anchor,false);peer.Present(9,true,false);
            Check(peer.Body.enabled&&peer.Anchor==anchor,"Opponent can see the authoritative alarm at the same table position");
            peer.ResetTurn();peer.Present(7,true,false);Check(peer.Body.enabled&&peer.Stage==1,"Opponent warning remains visible before a placement message arrives");
            peer.ResetTurn();peerHud.StopTimer();peer.ReceiveAnchor(anchor,false);peerHud.SetTime(.7f,30,true);peer.Present(7,true,false);Check(peer.Body.enabled&&peer.Anchor==anchor,"Placement arriving before timer update is retained");
            peer.Hide();UnityEngine.Object.DestroyImmediate(peer.gameObject);peerOwner.SetActive(false);
            // Save actual Image changes and bell events, then replay/seek the binary archive.
            alarm.ResetTurn();var data=new MatchReplayPresentation();capture=new MatchReplayStateCapture(data);float[] seconds={12,10,9.35f,7.9f,3.9f,0};
            int before=alarm.BellCount;
            for(int i=0;i<seconds.Length;i++){Sample(seconds[i]);capture.Capture(scene,camera,i);if(alarm.BellCount!=before){before=alarm.BellCount;data.Queue(i,"shot-clock-bell",new ReplayShotClockBell{stage=alarm.Stage,volume=.26f});}}
            capture.CompleteAssets();Check(data.events.Any(e=>e.kind=="visual-sprite"),"Clock frame crops are stored in replay");
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))data.Write(writer);report.bytes=(int)stream.Length;stream.Position=0;MatchReplayPresentation restored;using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))restored=MatchReplayPresentation.Read(reader,seconds.Length);
                Check(restored.events.Count(e=>e.kind=="shot-clock-bell")>=3,"Stage bell events survive save and reload");display=new MatchReplayStateDisplay(owner.transform,restored,null);
                Image ReplayBody()=>display.Root.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Shot clock alarm sprite"&&i.transform.IsChildOf(display.Root));
                display.Apply(0);display.Apply(1);Check(ReplayBody().enabled&&ReplayBody().sprite.texture.name=="AlarmBored","Replay shows ten-second bored clock");
                display.Apply(2);display.Apply(3);Check(ReplayBody().enabled&&ReplayBody().sprite.texture.name=="AlarmAnnoyed","Replay shows annoyed body frames");
                display.Apply(4);Check(ReplayBody().sprite.texture.name=="AlarmAngry","Replay shows angry body frames");
                Check(display.Root.GetComponentsInChildren<ShotClockAlarmSound>(true).Any(s=>s.BellRequests>=3),"Normal forward replay requests the recorded warning sounds");
                display.Apply(5);Check(!ReplayBody().enabled,"Replay removes alarm at timer stop");
                display.Apply(1);Check(ReplayBody().sprite.texture.name=="AlarmBored","Backward seek restores early alarm appearance");display.Apply(4);Check(ReplayBody().sprite.texture.name=="AlarmAngry","Forward seek restores later alarm appearance");
                Check(display.Root.GetComponentsInChildren<Button>(true).Length==0,"Replay does not create interactive alarm controls");
            }
            report.passed=true;
        }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}finally{display?.Dispose();capture?.Dispose();if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}if(material)UnityEngine.Object.DestroyImmediate(material);File.WriteAllText("Logs/shot-clock-alarm-validation.json",JsonUtility.ToJson(report,true));}
    }
}
