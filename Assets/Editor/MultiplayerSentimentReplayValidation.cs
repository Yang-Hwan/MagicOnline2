using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad] public static class MultiplayerSentimentReplayValidation
{
    const string Request="Logs/multiplayer-sentiment-replay.request";
    [Serializable] sealed class Report {public bool passed;public int checks,frames,events,bytes;public double maxCaptureMs;public string error="",crow="";}
    static MultiplayerSentimentReplayValidation(){EditorApplication.update+=()=>{if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Request);EditorApplication.delayCall+=Run;};}
    public static void Run()
    {
        var report=new Report();Scene source=default,scene=default;MatchReplayStateCapture capture=null;MatchReplayStateDisplay display=null;RenderTexture target=null;
        var ballProperty=typeof(PoolCoach).GetProperty("matchBall");var coach=PoolCoach.Instance;var savedBall=coach.matchBall;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);report.checks++;}
        try{
            MatchReplayAssetBuilder.Generate();
            source=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Pavilion.unity");scene=EditorSceneManager.NewPreviewScene();scene.name="Pavilion";
            GameObject Node(string name,Transform parent=null){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(parent,false);return go;}
            void Local(Transform a,Transform b){a.localPosition=b.localPosition;a.localRotation=b.localRotation;a.localScale=b.localScale;}
            var originalTable=source.GetRootGameObjects().Single(g=>g.name=="Table").transform;
            var table=Node("Table").transform;table.SetPositionAndRotation(originalTable.position,originalTable.rotation);table.localScale=originalTable.lossyScale;
            var cloth=Node("ClothSpace",table).transform;Local(cloth,originalTable.Find("ClothSpace"));
            var addition=Node("Addition",table).transform;Local(addition,originalTable.Find("Addition"));
            var actors=Node("Sentiment",addition).transform;Local(actors,originalTable.Find("Addition/Sentiment"));
            foreach(string name in new[]{"s5_10","s5_8","s5_9","s5_11","s8_5","s5_4","s1_4"}){
                var original=originalTable.Find("Addition/Sentiment/"+name);Check(original,"Actual multiplayer scene contains "+name);
                var clone=UnityEngine.Object.Instantiate(original.gameObject);clone.name=name;SceneManager.MoveGameObjectToScene(clone,scene);clone.transform.SetParent(actors,false);Local(clone.transform,original);
            }
            var cameraGo=Node("Validation camera");var camera=cameraGo.AddComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.orthographic=true;camera.orthographicSize=cloth.TransformVector(Vector3.forward).magnitude*.68f;camera.aspect=16f/9;camera.transform.SetPositionAndRotation(cloth.position+Vector3.up*6,Quaternion.Euler(90,0,0));camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.04f,.03f);
            target=new RenderTexture(1280,720,24);camera.targetTexture=target;Directory.CreateDirectory("Logs/MultiplayerSentiments");
            Color32[] Render(string filename,Transform root,Action refresh=null){
                var canvases=root.GetComponentsInChildren<Canvas>(true).Where(c=>c.isRootCanvas&&c.gameObject.activeInHierarchy).ToArray();
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
                Canvas.ForceUpdateCanvases();refresh?.Invoke();Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Logs/MultiplayerSentiments/"+filename+".png",image.EncodeToPNG());var pixels=image.GetPixels32();UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;
                foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceOverlay;c.worldCamera=null;}
                Canvas.ForceUpdateCanvases();refresh?.Invoke();return pixels;
            }
            double Difference(Color32[] a,Color32[] b)=>a.Select((p,i)=>(double)Math.Abs(p.r-b[i].r)+Math.Abs(p.g-b[i].g)+Math.Abs(p.b-b[i].b)).Sum()/(a.Length*3);
            var owner=Node("Multiplayer controller");owner.AddComponent<RectTransform>();owner.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var controller=owner.AddComponent<MultiplayerSentimentController>();
            foreach(int count in new[]{3,4}){
                ballProperty.SetValue(coach,(MatchBall)count);controller.Initialize(actors);
                Check(actors.Find("s5_10").GetComponent<PracticeTableDemolition>()&&actors.Find("s5_8").GetComponent<PracticeTableStorm>()&&actors.Find("s5_9").GetComponent<PracticeSkullCurse>(),"Multiplayer initializes all three new effects for "+count+" balls");
                var choices=((System.Collections.Generic.List<PracticeSentimentActor>[])typeof(MultiplayerSentimentController).GetField("animationGroups",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller))[5];
                int hammerIndex=choices.FindIndex(a=>a.name=="s5_10");Check(hammerIndex>=0,"Demolition hammer is included in multiplayer's category-five animation selection");
                int hammerSeed=Enumerable.Range(0,1000).First(s=>new System.Random(s).Next(choices.Count)==hammerIndex);
                controller.Present(5,hammerSeed,cloth.position);Check(actors.Find("s5_10").GetComponent<PracticeTableDemolition>().VisualRoot.activeSelf,"Network presentation entry point can select and play the new hammer");controller.Hide();
                foreach(string name in new[]{"s5_10","s5_8","s5_9","s5_11","s8_5"}){
                    var actor=actors.Find(name).GetComponent<PracticeSentimentActor>();var cue=cloth.TransformPoint(new Vector3(.1f,0,-.12f));
                    var data=new MatchReplayPresentation();capture=new MatchReplayStateCapture(data);
                    controller.Play(actor,cue,true);
                    var hammer=actor.GetComponent<PracticeTableDemolition>();var storm=actor.GetComponent<PracticeTableStorm>();var skull=actor.GetComponent<PracticeSkullCurse>();
                    var tears=actor.GetComponent<PracticeTearFountain>();
                    var pride=actor.GetComponent<PracticeProudHalo>();
                    if(hammer)hammer.Overlay.SetCamera(camera);if(skull)skull.SetViewCamera(camera);if(tears)tears.SetViewCamera(camera);if(pride)pride.SetViewCamera(camera);Canvas.ForceUpdateCanvases();
                    void Sample(float time){if(hammer)hammer.Sample(time);if(storm)storm.Sample(time);if(skull)skull.Sample(time);if(tears)tears.Sample(time);if(pride)pride.Sample(time);Canvas.ForceUpdateCanvases();}
                    Sample(.4f);
                    Canvas overlay=hammer?hammer.Overlay.Canvas:storm?storm.WeatherCanvas:skull?skull.OverlayCanvas:tears?tears.OverlayCanvas:pride.OverlayCanvas;
                    Check(overlay.renderMode==RenderMode.ScreenSpaceOverlay&&overlay.sortingOrder==32767,"Effect overlays HUD and cue UI: "+name);
                    if(hammer){Check(hammer.StrikePoints.All(p=>{var local=cloth.InverseTransformPoint(p);return Mathf.Abs(local.x)<.5f&&Mathf.Abs(local.z)<.5f;}),"Hammer impacts stay within table");}
                    if(storm)Check(storm.WeatherCanvas.GetComponentsInChildren<Image>(true).Count(i=>i.name.StartsWith("Storm cloud"))==5,"Storm has only one upper cloud row");
                    if(skull)Check(Vector3.Distance(skull.CuePoint,cue)<.02f,"Skull remains centered around cue point");
                    if(tears)Check(Vector3.Distance(tears.CuePoint,cue)<.02f,"Tear fountain remains centered around cue point");
                    if(pride)Check(Vector3.Distance(pride.CuePoint,cue)<.02f,"Proud halo remains centered around cue point");
                    var times=tears?new[]{.4f,1.15f,2.90f,3.12f,3.7f,4.9f}:new[]{.4f,1.15f,1.4f,2.5f,3.3f,4.9f};
                    string expected=null;int expectedVisible=0;Vector2 expectedPosition=default;Color32[] sourcePixels=null;
                    for(int i=0;i<times.Length;i++){
                        Sample(times[i]);capture.Capture(scene,camera,times[i]);if(i>=3)report.maxCaptureMs=Math.Max(report.maxCaptureMs,capture.LastMilliseconds);
                        if(i==3){var visible=overlay.GetComponentsInChildren<Image>(true).Where(g=>g.enabled&&g.gameObject.activeInHierarchy&&g.color.a>.001f).ToArray();expectedVisible=visible.Length;Check(expectedVisible>0,"Source effect has visible artwork: "+name);var item=visible.First();expected=item.name;expectedPosition=item.rectTransform.anchoredPosition;sourcePixels=Render(count+"-"+name+"-source",overlay.transform,()=>Sample(times[i]));}
                    }
                    capture.CompleteAssets();
                    string folder=hammer?"Demolition/":storm?"Storm/":skull?"SkullCurse/":tears?"TearFountain/":"ProudHalo/";
                    string[] names=hammer?new[]{"HammerAngles","CrackStages","ConcreteShards"}:storm?new[]{"StormAtlas"}:skull?new[]{"SkullAtlas"}:tears?new[]{"TearAtlas","FountainLoop"}:new[]{"PrideAtlas"};
                    var keys=names.Select(n=>MatchReplayAssets.Current.Id(Resources.Load<Texture2D>("Gameplay/Sentiments/"+folder+n))).ToArray();
                    var spriteAssets=data.events.Where(e=>e.kind=="visual-sprite").Select(e=>JsonUtility.FromJson<ReplaySpriteAsset>(e.value)).ToArray();
                    Check(keys.All(k=>spriteAssets.Any(s=>s.texture==k)),"New effect atlases use stable IDs without copying pixels: "+name);
                    using(var stream=new MemoryStream()){
                        using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))data.Write(writer);report.bytes+=(int)stream.Length;stream.Position=0;
                        MatchReplayPresentation restored;using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))restored=MatchReplayPresentation.Read(reader,data.frames.Count);
                        display=new MatchReplayStateDisplay(table,restored,null);display.Apply(3);Canvas.ForceUpdateCanvases();
                        var replayOverlay=display.Root.GetComponentsInChildren<Canvas>(true).Single(c=>c.name==overlay.name);
                        var visible=replayOverlay.GetComponentsInChildren<Image>(true).Where(g=>g.enabled&&g.gameObject.activeInHierarchy&&g.color.a>.001f).ToArray();
                        Check(visible.Length==expectedVisible,"Saved replay restores every visible sprite: "+name);
                        Check(Vector2.Distance(visible.Single(g=>g.name==expected).rectTransform.anchoredPosition,expectedPosition)<.01f,"Replay retains cue/table/screen placement: "+name);
                        Check(replayOverlay.sortingOrder==32767&&visible.All(g=>!g.raycastTarget),"Replay preserves overlay order and non-interactive graphics: "+name);
                        var replayPixels=Render(count+"-"+name+"-replay",display.Root);
                        Check(Difference(sourcePixels,replayPixels)<1,"Saved replay visually matches the live effect: "+name);
                        if(hammer){display.Apply(1);Check(replayOverlay.GetComponentsInChildren<Image>(true).Any(g=>g.name=="Demolition hammer"&&g.enabled&&g.gameObject.activeInHierarchy&&g.color.a>.1f),"Replay restores the moving hammer itself, not only the cracks");Render(count+"-s5_10-hammer-replay",display.Root);}
                        if(tears){
                            display.Apply(1);int countJets=replayOverlay.GetComponentsInChildren<Image>(true).Count(g=>g.name.StartsWith("Tear jet")&&g.enabled&&g.gameObject.activeInHierarchy);Check(countJets>=2&&countJets<=4,"Replay restores both connected fountain sprites and frame crossfade");
                            display.Apply(2);Check(replayOverlay.GetComponentsInChildren<Image>(true).Count(g=>g.name.StartsWith("Final large tear")&&g.enabled&&g.gameObject.activeInHierarchy)==2,"Replay restores the two oversized falling tears");
                        }
                        display.Apply(0);display.Apply(3);Check(replayOverlay.GetComponentsInChildren<Image>(true).Count(g=>g.enabled&&g.gameObject.activeInHierarchy&&g.color.a>.001f)==expectedVisible,"Backward and forward seek restores effect: "+name);
                        display.Apply(5);Check(!replayOverlay.gameObject.activeInHierarchy,"Replay clears the effect at its end: "+name);
                        report.frames+=restored.frames.Count;report.events+=restored.events.Count;display.Dispose();display=null;
                    }
                    capture.Dispose();capture=null;controller.Hide();
                }
                // Exercise the new two-bomb effect through the actual multiplayer adapter.
                var bomb=actors.Find("s1_4").GetComponent<PracticeSentimentActor>();
                Check(bomb.GetComponent<PracticeBombExplosion>(),"Multiplayer installs the new bomb effect instead of its legacy animation");
                controller.Play(bomb,cloth.position,true);var explosion=bomb.GetComponent<PracticeBombExplosion>();
                var bombData=new MatchReplayPresentation();capture=new MatchReplayStateCapture(bombData);
                float[] bombTimes={.2f,.71f,1.0f,1.41f,2.1f,4.6f};Color32[] bombPixels=null;
                SpriteRenderer Part(string n)=>explosion.VisualRoot.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==n);
                for(int i=0;i<bombTimes.Length;i++){
                    explosion.Sample(bombTimes[i]);capture.Capture(scene,camera,bombTimes[i]);
                    if(i==1)Check(Part("Detonation flash").enabled&&Part("Shockwave").enabled,"First bomb has flash and shockwave in multiplayer");
                    if(i==2)Check(Part("Falling bomb (2)").enabled,"Second bomb falls after the first blast in multiplayer");
                    if(i==3){Check(Part("Detonation flash (2)").enabled&&Part("Shockwave (2)").enabled,"Second bomb detonates in multiplayer");bombPixels=Render(count+"-s1_4-source",explosion.VisualRoot.transform);}
                }
                capture.CompleteAssets();
                using(var stream=new MemoryStream()){
                    using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))bombData.Write(writer);report.bytes+=(int)stream.Length;stream.Position=0;MatchReplayPresentation restored;
                    using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))restored=MatchReplayPresentation.Read(reader,bombTimes.Length);
                    display=new MatchReplayStateDisplay(table,restored,null);display.Apply(3);
                    SpriteRenderer ReplayPart(string n)=>display.Root.GetComponentsInChildren<SpriteRenderer>(true).Single(r=>r.name==n);
                    Check(ReplayPart("Detonation flash (2)").enabled&&ReplayPart("Shockwave (2)").enabled,"Saved replay restores the second explosion");
                    Check(Difference(bombPixels,Render(count+"-s1_4-replay",display.Root))<1,"Saved replay visually matches the new multiplayer bomb effect");
                    display.Apply(0);Check(ReplayPart("Falling bomb").enabled,"Backward seek restores the falling first bomb");
                    display.Apply(5);Check(!ReplayPart("Residual smoke 0").gameObject.activeInHierarchy,"Replay clears the explosion and smoke at the end");
                    display.Dispose();display=null;
                }
                capture.Dispose();capture=null;controller.Hide();
            }
            // Moving world-space crow dots cannot be reconstructed at the emitter's final position.
            var crow=actors.Find("s5_4");crow.gameObject.SetActive(true);crow.Find("bird").gameObject.SetActive(false);var ps=crow.Find("egg").GetComponent<ParticleSystem>();ps.gameObject.SetActive(true);
            Check(MatchReplayStateCapture.RequiresParticleHistory(ps)&&!MatchReplayStateCapture.CanRegenerateParticles(ps),"Crow dots always use exact particle history");
            ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=5;main.startSpeed=0;main.startSize=.03f;ps.useAutoRandomSeed=false;ps.randomSeed=54;
            var emission=ps.emission;emission.enabled=false;ps.Play(false);
            var crowData=new MatchReplayPresentation();capture=new MatchReplayStateCapture(crowData);
            for(int i=0;i<3;i++){ps.transform.position=cloth.position+cloth.right*(i*.3f);ps.Emit(4);ps.Pause(false);capture.Capture(scene,camera,i*.1f);}
            var points=new ParticleSystem.Particle[32];int countDots=ps.GetParticles(points);Check(countDots==12,"Moving crow fixture leaves dots at three distinct positions");
            Render("crow-source",actors);
            capture.CompleteAssets();crowData.FlushPending();var node=crowData.events.Where(e=>e.kind=="visual-node").Select(e=>JsonUtility.FromJson<ReplayVisualNode>(e.value)).Single(n=>n.kind=="particle"&&n.name=="egg");
            Check(node.particleBuffer&&!node.particleEvents,"Crow replay stores particle buffers instead of seed-only simulation");
            using(var stream=new MemoryStream()){
                using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))crowData.Write(writer);stream.Position=0;MatchReplayPresentation restored;
                using(var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true))restored=MatchReplayPresentation.Read(reader,3);
                display=new MatchReplayStateDisplay(table,restored,null);display.Apply(2);
                var replay=display.Root.GetComponentsInChildren<ParticleSystem>(true).Single(p=>p.name=="egg");var replayPoints=new ParticleSystem.Particle[32];int n=replay.GetParticles(replayPoints);
                Check(replay.main.simulationSpace==ps.main.simulationSpace,"Crow replay retains its recorded particle coordinate space");
                Check(n==countDots&&Enumerable.Range(0,n).All(i=>Vector3.Distance(points[i].position,replayPoints[i].position)<.001f),"Replay preserves every crow dot at its original emission position");
                var replayPixels=Render("crow-replay",display.Root);Check(replayPixels.Any(p=>p.r>32||p.g>32||p.b>32),"Crow dots are visibly rendered in the saved replay");
                display.Apply(0);Check(replay.particleCount==4,"Backward seek restores the early crow dots");display.Apply(2);Check(replay.particleCount==12,"Forward seek restores the complete crow trail");
                report.crow="12 particles preserved across moving emitter positions and backward/forward seek";
            }
            report.passed=true;
        }catch(Exception e){report.error=e.ToString();Debug.LogException(e);}finally{
            display?.Dispose();capture?.Dispose();ballProperty.SetValue(coach,savedBall);if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene);if(source.IsValid())EditorSceneManager.ClosePreviewScene(source);if(target){target.Release();UnityEngine.Object.DestroyImmediate(target);}File.WriteAllText("Logs/multiplayer-sentiment-replay.json",JsonUtility.ToJson(report,true));
        }
    }
}
