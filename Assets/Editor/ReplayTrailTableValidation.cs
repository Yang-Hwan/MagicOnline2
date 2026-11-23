using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections;using System.Collections.Generic;using UnityEngine;using Assets.Scripts.Often;
using PhysicsView=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
public static class ReplayTrailTableValidation
{
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static int checks;
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);checks++;}
    public static void Run(){
        var host=new GameObject("Inactive replay fixture");host.SetActive(false);var table=new GameObject("TableBg",typeof(SpriteRenderer));var trail=new GameObject("Replay Cue Trail",typeof(LineRenderer));
        var texture=new Texture2D(2,2);var original=Sprite.Create(texture,new Rect(0,0,2,2),Vector2.one*.5f);table.GetComponent<SpriteRenderer>().sprite=original;
        try{
            var physics=host.AddComponent<PhysicsView>();physics.ballcs=new Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC[3];var type=typeof(PhysicsView);
            void Set(string name,object value)=>type.GetField(name,Flags).SetValue(physics,value);
            void Call(string name)=>type.GetMethod(name,Flags).Invoke(physics,null);
            var frames=(IList)type.GetField("replayFrames",Flags).GetValue(physics);var frameType=type.GetNestedType("ReplayFrame",BindingFlags.NonPublic);
            for(int i=0;i<6;i++){var frame=Activator.CreateInstance(frameType);frameType.GetField("time").SetValue(frame,(float)i);frameType.GetField("positions").SetValue(frame,new[]{new Vector3(i,0,0),new Vector3(10,0,i),Vector3.zero});frames.Add(frame);}
            var data=new MatchReplayPresentation{width=1600,height=900};for(int i=0;i<6;i++){data.frames.Add(Array.Empty<int>());data.views.Add(new MatchReplayPresentation.View{rotation=Quaternion.identity,rect=new Rect(0,0,1,1),orthographic=true,size=5,fov=60,near=.1f,far=100});}
            data.events.Add(new MatchReplayPresentation.Event{kind="shot",time=0,value=JsonUtility.ToJson(new MatchShotCommand{seat=0})});
            data.events.Add(new MatchReplayPresentation.Event{kind="arena-table",time=0,value="ClassicWalnut"});
            data.events.Add(new MatchReplayPresentation.Event{kind="shot",time=3,value=JsonUtility.ToJson(new MatchShotCommand{seat=1})});
            data.events.Add(new MatchReplayPresentation.Event{kind="arena-table",time=3,value="TournamentBlue"});
            using(var stream=new MemoryStream()){data.Write(new BinaryWriter(stream));Check(stream.Length<4096,"Resource metadata unexpectedly large");stream.Position=0;data=MatchReplayPresentation.Read(new BinaryReader(stream),6);}
            Set("replayPresentation",data);var line=trail.GetComponent<LineRenderer>();Set("replayTrail",line);Call("BuildMatchReplayEnvironment");
            void Apply(int index){type.GetProperty("ReplayFrameIndex",Flags).SetValue(physics,index);Call("UpdateMatchCueTrail");Call("ApplyMatchReplayTable");}
            Apply(2);Check(line.positionCount==3&&line.GetPosition(2).x==2,"First cue path");
            var walnut=ArenaTableSkins.ReplaySprite("ClassicWalnut");var blue=ArenaTableSkins.ReplaySprite("TournamentBlue");Check(walnut&&blue,"Fixture table resources missing");Check(table.GetComponent<SpriteRenderer>().sprite==walnut,"First arena appearance");
            Apply(3);Check(line.positionCount==1&&!line.enabled,"Previous shot path not cleared");
            Apply(5);Check(line.positionCount==3&&line.GetPosition(0).x==10&&line.GetPosition(2).z==5,"Second cue/forward seek");Check(table.GetComponent<SpriteRenderer>().sprite==blue,"Changed table appearance");
            Apply(1);Check(line.positionCount==2&&line.GetPosition(1).x==1&&table.GetComponent<SpriteRenderer>().sprite==walnut,"Backward seek restores path/table");
            Apply(1);Check(line.positionCount==2,"Repeated paused frame duplicates points");
            Check(MatchReplayStateCapture.IsRemovedOverlay("SupportLines"),"Guides still recorded/displayed");Check(!MatchReplayVisualCapture.IsWorldEffect(line),"Generated path suppressed as live cue effect");
            Check(ArenaTableSkins.ReplayKey(blue)=="TournamentBlue"&&ArenaTableSkins.ReplaySprite("unknown")==null,"Skin key safety/roundtrip");
            Call("StopRecordedPresentation");Check(table.GetComponent<SpriteRenderer>().sprite==original,"Exit did not restore original table");
            File.WriteAllText("Logs/replay-trail-table-result.txt","PASS "+checks+" checks");
        }catch(Exception e){File.WriteAllText("Logs/replay-trail-table-result.txt",e.ToString());Debug.LogException(e);}
        finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(table);UnityEngine.Object.DestroyImmediate(trail);UnityEngine.Object.DestroyImmediate(original);UnityEngine.Object.DestroyImmediate(texture);}
    }
}
