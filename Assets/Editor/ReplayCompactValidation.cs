using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEngine;
using UnityEditor;

public static class ReplayCompactValidation
{
    static int checks;
    static void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
    static Vector3 V(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    static Quaternion Q(BinaryReader r)=>new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
    [MenuItem("Tools/Gameplay/Validate Compact Match Replay")]
    public static void Run(){
        try{
            checks=0;Motion();Innings();VisualState();Geometry();ArchiveReader();ScoreButtons();HudClock();ParticleEvents();
            string input=Environment.GetEnvironmentVariable("REPLAY_VALIDATION_INPUT");string metrics=string.IsNullOrEmpty(input)?"":Measure(input);
            File.WriteAllText("Logs/replay-compact-result.txt","PASS "+checks+" checks\n"+metrics);
        }catch(Exception e){File.WriteAllText("Logs/replay-compact-result.txt",e.ToString());Debug.LogException(e);}
    }
    static void Motion(){
        var frames=new List<MatchBallState[]>();var times=new List<float>();
        for(int f=0;f<=36000;f++){
            float x=f%900<600?0:Mathf.Sin(f*.03f);times.Add(f/30f);
            frames.Add(Enumerable.Range(0,4).Select(i=>new MatchBallState{position=new Vector3(x+i,0,-x),rotation=Quaternion.Euler(x*13,0,0),velocity=new Vector3(x,0,x*2),angularVelocity=Vector3.one*x}).ToArray());
        }
        using(var memory=new MemoryStream()){
            MatchReplayMotionCodec.Write(new BinaryWriter(memory),times,frames,4);long size=memory.Length;memory.Position=0;
            var restored=new List<MatchBallState[]>();var clock=new List<float>();MatchReplayMotionCodec.Read(new BinaryReader(memory),frames.Count,4,clock,restored);
            Check(times.SequenceEqual(clock)&&frames.SelectMany(f=>f).SequenceEqual(restored.SelectMany(f=>f)),"20 minute motion and float bits must round-trip exactly");
            Check(size<3000000,"20 minute mixed idle/motion fixture exceeds motion budget: "+size);
            byte[] truncated=memory.ToArray().Take((int)memory.Length-8).ToArray();bool rejected=false;
            try{MatchReplayMotionCodec.Read(new BinaryReader(new MemoryStream(truncated)),frames.Count,4,new List<float>(),new List<MatchBallState[]>());}catch(IOException){rejected=true;}
            Check(rejected,"Truncated motion accepted");
        }
    }
    static void VisualState(){
        var s=new ReplayVisualState{id=9,frame=22,active=true,enabled=true,position=new Vector3(.1234567f,-10,3),scale=Vector3.one*.06f,rotation=Quaternion.Euler(12,30,20),text="한글🙂",particles=null,geometry="@part:1",uv=new Rect(.1f,.2f,.3f,.4f)};
        string json=JsonUtility.ToJson(s);s=JsonUtility.FromJson<ReplayVisualState>(json);var restored=JsonUtility.FromJson<ReplayVisualState>(ReplayVisualStateCodec.Decode(ReplayVisualStateCodec.Encode(json)));
        Check(s.Equals(restored)&&s.frame==restored.frame,"Binary visual state altered fields");
        var p=new MatchReplayPresentation{width=1600,height=900};p.events.Add(new MatchReplayPresentation.Event{kind="visual-state",value=json,time=0});s.frame=23;s.text="다음";p.events.Add(new MatchReplayPresentation.Event{kind="visual-state",value=JsonUtility.ToJson(s),time=1});
        using(var m=new MemoryStream()){p.Write(new BinaryWriter(m));m.Position=0;var copy=MatchReplayPresentation.Read(new BinaryReader(m),0);Check(copy.events.Count==2&&JsonUtility.FromJson<ReplayVisualState>(copy.events[1].value).Equals(s),"Version 3 visual state round trip");}
    }
    static void Innings(){
        var p=new MatchReplayPresentation{width=1600,height=900,localSeat=1};for(int i=0;i<100;i++)p.frames.Add(Array.Empty<int>());
        var h=new InningScoreHistory();h.Reset(1);int total0=0,total1=0;
        for(int visit=0;visit<6;visit++){
            int seat=visit%2==0?1:0;h.Activate(seat);
            p.events.Add(new MatchReplayPresentation.Event{kind="inning-score",time=visit*10,value=JsonUtility.ToJson(new ReplayInningEvent{frame=visit*10,scores=h.Capture(),name0="상대",name1="나"})});
            if(seat==0)total0+=visit;else total1+=visit;h.SetTotals(total0,total1);
            p.events.Add(new MatchReplayPresentation.Event{kind="inning-score",time=visit*10+1,value=JsonUtility.ToJson(new ReplayInningEvent{frame=visit*10+1,scores=h.Capture()})});
        }
        var index=new MatchReplayInnings(p);
        Check(index.Visits.Count==6&&index.Find(0,2).frame==50&&index.Find(1,1).frame==20,"Inning 3 opponent and inning 2 self destinations");
        Check(index.At(50).seat==0&&index.At(20).seat==1&&index.At(20).column==1,"Backward/forward active visit selection");
        Check(index.Scores.Total(0)==total0&&index.Scores.Total(1)==total1&&index.Find(1,3)==null,"Final scores and missing visits");
    }
    static void Geometry(){
        var line=new ReplayLine{positions=new[]{new Vector3(.1234567f,0,3),new Vector3(-2,0,1)},world=true,width=.123f,color=new Gradient(),widthCurve=AnimationCurve.Linear(0,1,1,0)};
        string json=JsonUtility.ToJson(line);var restored=JsonUtility.FromJson<ReplayLine>(ReplayGeometryCodec.DecodeLine(ReplayGeometryCodec.EncodeLine(json)));
        Check(line.positions.SequenceEqual(restored.positions)&&line.width==restored.width&&restored.world,"Binary line coordinates must be lossless");
        var samples=Enumerable.Range(0,1000).Select(i=>new Vector3(i*.001f,0,i<500?0:(i-500)*.001f)).ToArray();var simplified=ReplayGeometryCodec.SimplifyLine(samples);
        Check(simplified.Length<10&&simplified[0]==samples[0]&&simplified[simplified.Length-1]==samples[samples.Length-1],"Visual line compaction keeps endpoints and corner");
        Check(samples.All(point=>Enumerable.Range(0,simplified.Length-1).Any(i=>{var delta=simplified[i+1]-simplified[i];var t=delta.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-simplified[i],delta)/delta.sqrMagnitude):0;return Vector3.Distance(point,simplified[i]+t*delta)<=.00011f;})),"Visual line deviation exceeds tolerance");
        var mesh=new ReplayLocalGeometry{positions=line.positions,colors=new[]{new Color32(1,2,3,4),new Color32(255,254,253,252)},indices=new[]{0,1,0},uv0=new[]{Vector4.one,Vector4.zero},uv1=Array.Empty<Vector4>(),uv2=Array.Empty<Vector4>(),uv3=Array.Empty<Vector4>()};
        var copy=JsonUtility.FromJson<ReplayLocalGeometry>(ReplayGeometryCodec.DecodeMesh(ReplayGeometryCodec.EncodeMesh(JsonUtility.ToJson(mesh))));
        Check(mesh.positions.SequenceEqual(copy.positions)&&mesh.colors.SequenceEqual(copy.colors)&&mesh.indices.SequenceEqual(copy.indices)&&mesh.uv0.SequenceEqual(copy.uv0),"Binary mesh fields must be lossless");
    }
    static void ArchiveReader(){
        var p=new MatchReplayPresentation{width=1600,height=900};for(int i=0;i<2;i++){p.frames.Add(Array.Empty<int>());p.views.Add(MatchReplayPresentation.View.Capture(null));}
        var archive=new MatchReplayArchive();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var type=typeof(MatchReplayArchive);type.GetField("<Presentation>k__BackingField",flags).SetValue(archive,p);type.GetField("ballCount",flags).SetValue(archive,4);type.GetField("complete",flags).SetValue(archive,true);type.GetField("recordingStarted",flags).SetValue(archive,true);
        var frames=(List<MatchBallState[]>)type.GetField("frames",flags).GetValue(archive);var times=(List<float>)type.GetField("times",flags).GetValue(archive);
        for(int f=0;f<2;f++){frames.Add(Enumerable.Range(0,4).Select(i=>new MatchBallState{position=new Vector3(i+f,0,0),rotation=Quaternion.identity}).ToArray());times.Add(f);}
        Check(archive.SaveToSlot("Logs/CompactArchiveTest",0),"Save v5 archive: "+archive.FailureReason);
        var host=new GameObject("Inactive replay reader");host.SetActive(false);
        try{
            var physics=host.AddComponent<Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng>();physics.ballcs=new Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC[4];
            var physicsType=physics.GetType();var frameType=physicsType.GetNestedType("ReplayFrame",System.Reflection.BindingFlags.NonPublic);
            var loaded=(System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(frameType));
            using(var reader=new BinaryReader(File.OpenRead("Logs/CompactArchiveTest/slot01.bin"))){object[] args={reader,loaded,0,0f,null};physicsType.GetMethod("ReadReplay",flags).Invoke(physics,args);}
            Check(loaded.Count==2&&physicsType.GetField("parsedPresentation",flags).GetValue(physics)!=null,"Actual replay browser reads v5 motion and presentation");
        }finally{UnityEngine.Object.DestroyImmediate(host);}
    }
    static void ScoreButtons(){
        var host=new GameObject("Inning test canvas",typeof(RectTransform),typeof(Canvas));host.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)host.transform).sizeDelta=new Vector2(1600,900);
        try{
            var history=new InningScoreHistory();history.Reset(1);for(int i=0;i<6;i++)history.Activate(i%2==0?1:0);history.Finish();
            int selectedSeat=-1,selectedColumn=-1;var view=InningScoreView.Install(host.transform,()=>history,seat=>seat==1?"나":"상대",()=>1);
            view.SetReplayNavigation((seat,column)=>{selectedSeat=seat;selectedColumn=column;},(seat,column)=>history.Score(seat,column).HasValue,(seat,column)=>seat==1&&column==1);view.SetOpen(true);
            var score=view.transform.Find("Inning Score UI/Inning Score Panel/Scrollable Innings Only/Innings Viewport/Innings Content/Inning Column 2/Score 1").GetComponent<UnityEngine.UI.Button>();
            Check(score.interactable,"Opponent third inning score is clickable");score.onClick.Invoke();Check(selectedSeat==0&&selectedColumn==2,"Score click maps self-seat 1 to opponent third inning");
            score=view.transform.Find("Inning Score UI/Inning Score Panel/Scrollable Innings Only/Innings Viewport/Innings Content/Inning Column 1/Score 0").GetComponent<UnityEngine.UI.Button>();score.onClick.Invoke();Check(selectedSeat==1&&selectedColumn==1,"Self second inning click maps to backward seek target");
        }finally{UnityEngine.Object.DestroyImmediate(host);}
    }
    static void HudClock(){
        var host=new GameObject("Replay HUD clock",typeof(RectTransform));var view=host.AddComponent<MatchHudView>();view.enabled=false;
        try{
            view.ApplyReplayVisual(JsonUtility.ToJson(new MatchHudView.ReplayVisual{side=1,ratio=.5f,seconds=15,time=10,blend=1,timer=true,counters=new[]{new MatchHudView.Counter(),new MatchHudView.Counter()}}));view.AdvanceReplayClock(3);
            Check(Mathf.Abs(view.RemainingSeconds-12)<.0001f&&Mathf.Abs(view.RemainingRatio-.4f)<.0001f&&view.VisualTime==13&&view.TurnBlend<.001f,"HUD timer and glow advance from event anchor");
            view.AdvanceReplayClock(0);Check(view.RemainingSeconds==15&&view.VisualTime==10,"HUD timer supports backward seeking");
        }finally{UnityEngine.Object.DestroyImmediate(host);}
    }
    static void ParticleEvents(){
        var saved=MatchReplayAssets.Current;var assets=saved;bool temporary=false;
        if(!assets){assets=ScriptableObject.CreateInstance<MatchReplayAssets>();assets.particleKeys=JsonUtility.FromJson<Keys>(File.ReadAllText("Logs/particle-keys.json")).keys;assets.particleResource="OriginalParticles";typeof(MatchReplayAssets).GetField("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,assets);temporary=true;}
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();MatchReplayStateDisplay display=null;GameObject source=null;
        try{
            string key=assets.particleKeys.First(k=>k.Contains("CoinAni/Root|"));source=UnityEngine.Object.Instantiate(assets.Particle(key));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(source,scene);source.SetActive(true);source.transform.localScale=Vector3.one*.06f;
            var ps=source.GetComponent<ParticleSystem>();ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);ps.useAutoRandomSeed=false;ps.randomSeed=123;var emission=ps.emission;emission.enabled=true;
            ps.Simulate(.1f,false,true,true);int first=ps.particleCount;
            ps.Simulate(.15f,false,true,true);emission.enabled=false;ps.Simulate(.05f,false,false,true);int stopped=ps.particleCount;
            var data=new MatchReplayPresentation();
            data.events.Add(new MatchReplayPresentation.Event{kind="visual-node",value=JsonUtility.ToJson(new ReplayVisualNode{id=0,parent=-1,name="Coin event",kind="particle",particle=key,particleEvents=true,particleSpeed=1,particleScaling=(int)ps.main.scalingMode,particleSpace=(int)ps.main.simulationSpace})});
            var state=new ReplayVisualState{id=0,frame=0,active=true,enabled=true,scale=Vector3.one*.06f,rotation=Quaternion.identity,seed=123,emitting=true,alpha=1};
            data.events.Add(new MatchReplayPresentation.Event{kind="visual-state",time=0,value=JsonUtility.ToJson(state)});
            data.events.Add(new MatchReplayPresentation.Event{kind="visual-clock:0",time=.1f,value="0.1"});
            state.frame=1;state.emitting=false;state.particleTime=.15f;data.events.Add(new MatchReplayPresentation.Event{kind="visual-state",time=.15f,value=JsonUtility.ToJson(state)});data.events.Add(new MatchReplayPresentation.Event{kind="visual-clock:1",time=.2f,value="0.2"});
            display=new MatchReplayStateDisplay(source.transform,data,null);display.Apply(0);var replay=display.Root.GetComponentInChildren<ParticleSystem>();
            Check(first>0&&replay.particleCount==first&&replay.transform.localScale==source.transform.localScale,"Coin event starts at the live size and emits particles");
            display.Apply(1);Check(stopped>0&&replay.particleCount==stopped,"Stopping emission retains living particles");
            display.Apply(0);display.Apply(1);Check(replay.particleCount==stopped,"Backward seek and replay preserve stopped-emitter particles");
        }finally{display?.Dispose();if(source)UnityEngine.Object.DestroyImmediate(source);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);if(temporary){typeof(MatchReplayAssets).GetField("instance",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).SetValue(null,saved);UnityEngine.Object.DestroyImmediate(assets);}}
    }
    [Serializable] sealed class Keys {public string[] keys;}
    static string Measure(string input){
        var times=new List<float>();var frames=new List<MatchBallState[]>();MatchReplayPresentation p;int balls,count;
        using(var r=new BinaryReader(File.OpenRead(input))){
            Check(r.ReadInt32()==0x4D525031&&r.ReadInt32()==4,"Size fixture must be a v4 archive");balls=r.ReadInt32();r.ReadInt32();r.ReadSingle();count=r.ReadInt32();if(r.ReadBoolean())r.BaseStream.Seek(32,SeekOrigin.Current);
            for(int f=0;f<count;f++){times.Add(r.ReadSingle());var frame=new MatchBallState[balls];for(int i=0;i<balls;i++)frame[i]=new MatchBallState{position=V(r),rotation=Q(r),velocity=V(r),angularVelocity=V(r)};frames.Add(frame);r.BaseStream.Seek(54,SeekOrigin.Current);}
            p=MatchReplayPresentation.Read(r,count);
        }
        var oldIndex=new MatchReplayInnings(p);Check(oldIndex.Available,"Old replay HUD must provide inning navigation");
        var templates=Resources.Load<GameObject>("OriginalParticles");var keys=JsonUtility.FromJson<Keys>(File.ReadAllText("Logs/particle-keys.json")).keys;
        var nodes=new Dictionary<int,ReplayVisualNode>();var partValues=new Dictionary<string,string>();var retained=new HashSet<string>();var compact=new List<MatchReplayPresentation.Event>();
        var lastHud=new Dictionary<int,(string id,MatchHudView.ReplayVisual visual)>();
        var sameHud=typeof(MatchReplayStateCapture).GetMethod("SameHud",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        int effects=0;
        foreach(var original in p.events){var e=original;
            if(e.kind.StartsWith("visual-part:",StringComparison.Ordinal)){partValues[e.kind.Substring(12)]=e.value;compact.Add(e);continue;}
            if(e.kind=="visual-node"){
                var node=JsonUtility.FromJson<ReplayVisualNode>(e.value);nodes[node.id]=node;
                node.proceduralHud=node.kind=="hud"||node.kind=="hud-art";
                if(node.kind=="particle"){
                    int at=Array.IndexOf(keys,node.particle);if(at>=0){var ps=templates.transform.GetChild(at).GetComponent<ParticleSystem>();if(MatchReplayStateCapture.CanRegenerateParticles(ps)){node.particleBuffer=false;node.particleEvents=true;node.particleSpeed=ps.main.simulationSpeed;node.particleScaling=(int)ps.main.scalingMode;node.particleSpace=(int)ps.main.simulationSpace;effects++;}}
                }e.value=JsonUtility.ToJson(node);
            }else if(e.kind=="visual-state"){
                var state=JsonUtility.FromJson<ReplayVisualState>(e.value);var node=nodes[state.id];
                if(node.particleEvents)state.particles=null;
                if(node.proceduralHud&&!string.IsNullOrEmpty(state.geometry)&&partValues.TryGetValue(state.geometry,out var json)){
                    var visual=JsonUtility.FromJson<MatchHudView.ReplayVisual>(json);
                    if(lastHud.TryGetValue(state.id,out var before)&&(bool)sameHud.Invoke(null,new object[]{visual,before.visual}))state.geometry=before.id;
                    else lastHud[state.id]=(state.geometry,visual);
                }
                foreach(string part in new[]{state.geometry,state.particles})if(!string.IsNullOrEmpty(part))retained.Add(part);
                e.value=JsonUtility.ToJson(state);
            }
            compact.Add(e);
        }
        p.events.Clear();p.events.AddRange(compact.Where(e=>!e.kind.StartsWith("visual-part:",StringComparison.Ordinal)||retained.Contains(e.kind.Substring(12))));
        using(var motion=new MemoryStream())using(var presentation=new MemoryStream()){
            MatchReplayMotionCodec.Write(new BinaryWriter(motion),times,frames,balls);p.Write(new BinaryWriter(presentation));
            presentation.Position=0;var restored=MatchReplayPresentation.Read(new BinaryReader(presentation),count);var index=new MatchReplayInnings(restored);
            Check(index.Visits.Count==oldIndex.Visits.Count,"Compaction must preserve old replay turn boundaries");
            string result=$"Source={new FileInfo(input).Length}, duration={times.Last():F2}s, motion={motion.Length}, presentation={presentation.Length}, total={motion.Length+presentation.Length+57}, eventEffects={effects}, visits={index.Visits.Count}";
            File.WriteAllBytes("Logs/compact-fixture-presentation.bin",presentation.ToArray());return result;
        }
    }
}
