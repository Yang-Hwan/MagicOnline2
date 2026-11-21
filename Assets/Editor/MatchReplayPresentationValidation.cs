using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StagePhysics=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;

[InitializeOnLoad]
public static class MatchReplayPresentationValidation
{
    const string Folder="Logs/MatchReplayPresentation";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] class Report { public bool passed;public int checks,frames,draws,meshes,textures;public long bytes;public string error,display;public float pixelError; }
    static MatchReplayPresentationValidation(){EditorApplication.update+=()=>{
        if(!File.Exists(Folder+"/run.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        File.Delete(Folder+"/run.request");Run();
    };}
    [MenuItem("Tools/Magic Online/Validate Full Match Presentation Replay")]
    public static void Run()
    {
        Directory.CreateDirectory(Folder);var report=new Report();var scene=EditorSceneManager.NewPreviewScene();
        RenderTexture rt=null;MatchReplayDisplay player=null;MatchReplayVisualCapture capture=null;Texture2D reference=null,effectTexture=null;Sprite effectSprite=null;Material effectMaterial=null;Mesh gpuMesh=null,readableMesh=null;var previous=RenderTexture.active;
        string directory=Path.Combine(Path.GetTempPath(),"Magic-Replay-"+Guid.NewGuid().ToString("N"));
        void Check(bool value,string why){if(!value)throw new Exception(why);report.checks++;}
        try{
            foreach(string shaderName in new[]{"Shader Graphs/Smoke","Shader Graphs/BoardHit","Shader Graphs/CueBallPosion","Shader Graphs/Diagram_DirCue","Shader Graphs/BallPosition","Shader Graphs/LineArrow02"}){
                var shader=Shader.Find(shaderName);Check(shader!=null,"Live match shader exists: "+shaderName);
                var material=new Material(shader);bool loggedError=false;
                Application.LogCallback callback=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception)loggedError=true;};
                Application.logMessageReceived+=callback;
                try{MatchReplayVisualCapture.MainTexture(material);Check(!loggedError,"Shader Graph without _MainTex records without errors: "+shaderName);}
                finally{Application.logMessageReceived-=callback;MatchReplayDisplay.Release(material);}
            }
            var cameraRoot=new GameObject("Replay test camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;camera.nearClipPlane=0;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            rt=new RenderTexture(800,450,24);camera.targetTexture=rt;
            var root=new GameObject("Recorded fixture",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            RectTransform Node(string name,Transform parent,Vector2 position,Vector2 size){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=Vector2.zero;r.anchoredPosition=position;r.sizeDelta=size;return r;}
            var background=Node("HUD background",root.transform,Vector2.zero,new Vector2(800,450)).gameObject.AddComponent<Image>();background.color=new Color(.03f,.07f,.09f,.7f);
            var effectObject=new GameObject("SentimentEffect",typeof(SpriteRenderer));SceneManager.MoveGameObjectToScene(effectObject,scene);effectObject.transform.position=new Vector3(.6f,-.25f,0);
            effectTexture=new Texture2D(2,2);effectTexture.SetPixels(new[]{Color.red,Color.red,Color.red,Color.red});effectTexture.Apply();effectSprite=Sprite.Create(effectTexture,new Rect(0,0,2,2),Vector2.one*.5f,8);
            effectMaterial=new Material(Shader.Find("UI/Default"));var effectRenderer=effectObject.GetComponent<SpriteRenderer>();effectRenderer.sprite=effectSprite;effectRenderer.sharedMaterial=effectMaterial;
            gpuMesh=new Mesh();gpuMesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};gpuMesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};gpuMesh.triangles=new[]{0,1,2};gpuMesh.UploadMeshData(true);
            readableMesh=(Mesh)typeof(MatchReplayArchive).Assembly.GetType("Assets.Scripts.Often.MatchReplayMeshReadback").GetMethod("Read",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{gpuMesh});
            Check(readableMesh.vertices.SequenceEqual(new[]{Vector3.zero,Vector3.right,Vector3.up})&&readableMesh.triangles.SequenceEqual(new[]{0,1,2}),"GPU-only imported effect meshes can be archived");
            var font=Resources.Load<TMP_FontAsset>("Common/Fonts/Asset/NanumBarunGothic SDF");
            TMP_Text Text(string name,Vector2 at,string value){var t=Node(name,root.transform,at,new Vector2(580,48)).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=30;t.text=value;t.color=Color.white;return t;}
            var title=Text("Players and score",new Vector2(30,360),"플레이어 A  0 : 0  플레이어 B");
            var technique=Text("Technique",new Vector2(30,250),"상대 턴");
            var coin=Node("득점 코인",root.transform,new Vector2(160,160),new Vector2(45,45)).gameObject.AddComponent<Image>();coin.color=new Color(1,.7f,.1f);
            var mask=Node("Character mask",root.transform,new Vector2(640,280),new Vector2(110,150));
            mask.gameObject.AddComponent<Assets.Scripts.Sight.Surface.Pavilion.MatchHudPortraitMask>();mask.gameObject.AddComponent<Mask>();
            var portrait=Node("Character",mask,Vector2.zero,new Vector2(130,160)).gameObject.AddComponent<Image>();portrait.color=Color.cyan;
            void Prepare(){Canvas.ForceUpdateCanvases();foreach(var g in root.GetComponentsInChildren<Graphic>()){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}Canvas.ForceUpdateCanvases();camera.Render();}
            Texture2D Screenshot(string name){camera.Render();RenderTexture.active=rt;var image=new Texture2D(800,450,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,800,450),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());return image;}
            var data=new MatchReplayPresentation{localSeat=1,matchId="fixture",winner=1,score0=2,score1=7,result="승리"};
            capture=new MatchReplayVisualCapture(data);data.width=800;data.height=450;
            Prepare();capture.Capture(scene,camera,0);
            title.text="플레이어 A  2 : 5  플레이어 B";technique.text="뒤돌려치기 · 득점 +2";coin.rectTransform.anchoredPosition=new Vector2(300,210);portrait.color=Color.green;
            Prepare();capture.Capture(scene,camera,1);reference=Screenshot("source-score");
            title.text="플레이어 A  2 : 7  플레이어 B";technique.text="승리했습니다";coin.gameObject.SetActive(false);Prepare();capture.Capture(scene,camera,3);
            Check(data.frames.Count==3&&data.draws.Count>0&&data.textures.Count>0,"HUD geometry and embedded artwork are captured");
            Check(data.events.Any(e=>e.value.Contains("뒤돌려치기"))&&data.events.Any(e=>e.value.Contains("승리했습니다")),"Technique and final result text events are preserved");
            byte[] encoded;using(var stream=new MemoryStream()){using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))data.Write(writer);encoded=stream.ToArray();report.bytes=encoded.Length;}
            MatchReplayPresentation loaded;using(var reader=new BinaryReader(new MemoryStream(encoded)))loaded=MatchReplayPresentation.Read(reader,3);
            Check(loaded.localSeat==1&&loaded.winner==1&&loaded.result=="승리"&&loaded.score1==7,"Local perspective and authoritative result round-trip");
            Check(loaded.frames.Select(f=>string.Join(",",f)).SequenceEqual(data.frames.Select(f=>string.Join(",",f))),"All presentation frame references round-trip");
            Check(loaded.views.Count==3&&loaded.views[1].position==camera.transform.position,"World camera poses share the HUD timeline");
            var raw=new byte[20000];var changed=(byte[])raw.Clone();changed[123]=71;changed[140]=22;
            var encodedBase=MatchReplayMeshCodec.Encode(raw,null,-1);var delta=MatchReplayMeshCodec.Encode(changed,raw,0);
            Check(delta.Length<100&&MatchReplayMeshCodec.Decode(delta,1,_=>MatchReplayMeshCodec.Decode(encodedBase,0,null)).SequenceEqual(changed),"HUD deltas retain exact bytes without storing unchanged timer artwork");
            bool rejected=false;try{using(var r=new BinaryReader(new MemoryStream(encoded.Take(encoded.Length-3).ToArray())))MatchReplayPresentation.Read(r,3);}catch(IOException){rejected=true;}
            Check(rejected,"Truncated presentation is rejected");
            rejected=false;try{using(var r=new BinaryReader(new MemoryStream(encoded)))MatchReplayPresentation.Read(r,4);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Mismatched ball/HUD timelines are rejected");
            var viewer=new GameObject("Replay viewer host");SceneManager.MoveGameObjectToScene(viewer,scene);
            player=new MatchReplayDisplay(viewer.transform,loaded);
            var replayCanvas=viewer.GetComponentsInChildren<Canvas>().First(c=>c.name=="Recorded match presentation");replayCanvas.renderMode=RenderMode.ScreenSpaceCamera;replayCanvas.worldCamera=camera;replayCanvas.planeDistance=.5f;
            player.Apply(1);Canvas.ForceUpdateCanvases();
            report.frames=loaded.frames.Count;report.draws=loaded.draws.Count;report.meshes=loaded.meshes.Count;report.textures=loaded.textures.Count;
            var visible=replayCanvas.GetComponentsInChildren<RecordedMatchGraphic>();
            report.display="viewport="+replayCanvas.pixelRect+", stage="+replayCanvas.transform.GetChild(0).localScale+", first="+(visible.Length>0?visible[0].rectTransform.anchoredPosition+" / "+visible[0].canvasRenderer.cull+" / "+visible[0].material.shader.name:"empty");
            var replay=Screenshot("replayed-score");var a=reference.GetPixels32();var b=replay.GetPixels32();double error=0;
            for(int i=0;i<a.Length;i++)error+=(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b))/765.0;
            report.pixelError=(float)(error/a.Length);MatchReplayDisplay.Release(replay);
            Check(report.pixelError<.01f,"Replayed HUD matches recorded text, colors, portrait mask and coin pose");
            var effectPoint=camera.WorldToScreenPoint(effectObject.transform.position);int sample=Mathf.Clamp((int)effectPoint.y,0,449)*800+Mathf.Clamp((int)effectPoint.x,0,799);
            Check(Math.Abs(a[sample].r-b[sample].r)<12&&Math.Abs(a[sample].g-b[sample].g)<12,"World animation positions match the recorded camera and HUD");
            player.Apply(2);Canvas.ForceUpdateCanvases();MatchReplayDisplay.Release(Screenshot("replayed-result"));
            player.Apply(0);player.Apply(2);player.Apply(1);
            Check(player.Frame==1&&loaded.events.Count==data.events.Count,"Seeking does not rerun or duplicate gameplay/effect events");
            Check(replayCanvas.GetComponentsInChildren<Button>(true).Length==0,"Recorded HUD cannot send button/network/payment actions");
            player.Dispose();player=null;Check(title.enabled&&portrait.enabled,"Exiting playback restores the live viewer");
            // Exercise the same file writer and reader used by multiplayer saves.
            Prepare();var owner=root.AddComponent<ElasticControlPreviewHost>();var archive=new MatchReplayArchive();
            var balls=Enumerable.Range(0,3).Select(i=>new MatchBallState{position=new Vector3(i*.1f,0,0),rotation=Quaternion.identity}).ToArray();
            archive.StartTimeline(owner,()=>balls,()=>true,1,"roundtrip");archive.Begin(new MatchShotCommand{seat=1,power=.4f,followThrough=.2f},Quaternion.identity);archive.Seal();
            float last=0;var times=(System.Collections.Generic.List<float>)typeof(MatchReplayArchive).GetField("times",Private).GetValue(archive);if(times.Count>0)last=times[times.Count-1];
            typeof(MatchReplayArchive).GetMethod("TimelineFrame",Private).Invoke(archive,new object[]{last+.1f,balls});
            archive.Presentation.frames.Add(archive.Presentation.frames.Last());
            archive.Presentation.views.Add(archive.Presentation.views.Last());
            typeof(MatchReplayArchive).GetMethod("TimelineComplete",Private).Invoke(archive,null);
            Check(archive.SaveToSlot(directory,0),"Multiplayer version-4 archive saves atomically");
            var inactive=new GameObject("Inactive replay reader");SceneManager.MoveGameObjectToScene(inactive,scene);inactive.SetActive(false);var physics=inactive.AddComponent<StagePhysics>();physics.ballcs=new Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC[3];
            Type frameType=typeof(StagePhysics).GetNestedType("ReplayFrame",BindingFlags.NonPublic);var frames=(IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(frameType));
            var args=new object[]{null,frames,0,0f,null};using(var reader=new BinaryReader(File.OpenRead(Path.Combine(directory,"slot01.bin")))){args[0]=reader;typeof(StagePhysics).GetMethod("ReadReplay",Private).Invoke(physics,args);}
            Check(frames.Count>=2&&typeof(StagePhysics).GetField("parsedPresentation",Private).GetValue(physics)!=null,"Version-4 file loads through the real replay-slot reader");
            // Version 3 remains supported for existing motion-only recordings.
            var old=new MatchReplayArchive();old.Begin(new MatchShotCommand{seat=0,power=.2f},Quaternion.identity);old.Sample(balls);old.Finish(balls);Check(old.SaveToSlot(directory,1),"Legacy archive remains writable");
            frames.Clear();using(var reader=new BinaryReader(File.OpenRead(Path.Combine(directory,"slot02.bin")))){args[0]=reader;typeof(StagePhysics).GetMethod("ReadReplay",Private).Invoke(physics,args);}
            Check(frames.Count==2&&typeof(StagePhysics).GetField("parsedPresentation",Private).GetValue(physics)==null,"Old motion-only replay remains readable");
            report.frames=loaded.frames.Count;report.draws=loaded.draws.Count;report.meshes=loaded.meshes.Count;report.textures=loaded.textures.Count;report.passed=true;
        }
        catch(Exception error){report.error=error.ToString();Debug.LogException(error);}
        finally{
            player?.Dispose();capture?.Dispose();MatchReplayDisplay.Release(reference);MatchReplayDisplay.Release(effectSprite);MatchReplayDisplay.Release(effectTexture);MatchReplayDisplay.Release(effectMaterial);MatchReplayDisplay.Release(gpuMesh);MatchReplayDisplay.Release(readableMesh);RenderTexture.active=previous;
            EditorSceneManager.ClosePreviewScene(scene);MatchReplayDisplay.Release(rt);
            string cleanup=Path.GetFullPath(directory),tempRoot=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            if(Directory.Exists(cleanup)&&Path.GetDirectoryName(cleanup)==tempRoot&&Path.GetFileName(cleanup).StartsWith("Magic-Replay-",StringComparison.Ordinal))Directory.Delete(cleanup,true);
            File.WriteAllText(Folder+"/validation.json",JsonUtility.ToJson(report,true));
        }
    }
}
