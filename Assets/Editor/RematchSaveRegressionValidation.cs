using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
public static class RematchSaveRegressionValidation {
 [MenuItem("Tools/Gameplay/Validate Rematch Replay Storage")]
 public static void Run() {
  var p=new MatchReplayPresentation {width=1368,height=768};
  string large="{\"id\":0,\"text\":\""+new string('x',30000)+"\"}";
  for(int i=0;i<10000;i++)p.events.Add(new MatchReplayPresentation.Event{time=i/30f,kind="visual-state",value=large});
  if((long)large.Length*p.events.Count<=MatchReplayPresentation.MaxBytes)throw new Exception("Fixture does not exceed old raw limit");
  Roundtrip(p);
  var unicode=new MatchReplayPresentation{width=1368,height=768};
  foreach(string value in new[]{"한글😀tail","한글😁tail","다른 문장🚀","","끝"})
   unicode.events.Add(new MatchReplayPresentation.Event{time=unicode.events.Count,kind="test-unicode",value=value});
  Roundtrip(unicode);
  using(var legacy=Legacy(false)) {var read=MatchReplayPresentation.Read(new BinaryReader(legacy),0);if(read.events[0].value!="기존 기록")throw new Exception("Legacy compatibility");}
  using(var invalid=Legacy(true)) {bool rejected=false;try{MatchReplayPresentation.Read(new BinaryReader(invalid),0);}catch(InvalidDataException){rejected=true;}if(!rejected)throw new Exception("Forward reference accepted");}
  CheckVisibility();
  Debug.Log("PASS replay storage: >286 MiB legacy payload, lossless delta roundtrip, Korean/emoji, v1 compatibility, invalid-reference rejection");
 }
 static void CheckVisibility() {
  var settings=Resources.Load<ElasticControlSettings>("Gameplay/ElasticControls/Settings");bool original=settings.useNewAppearance;
  var root=new GameObject("Rematch visibility regression");
  try {
   settings.useNewAppearance=true;var owner=root.AddComponent<RematchControlHost>();
   var cameraObject=new GameObject("Camera",typeof(Camera));cameraObject.transform.SetParent(root.transform);
   var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.transform.position=Vector3.back*10;
   var space=new GameObject("Original controls").transform;space.SetParent(root.transform);
   var power=new GameObject("Power",typeof(SpriteRenderer)).transform;power.SetParent(space);
   var follow=new GameObject("Follow",typeof(SpriteRenderer)).transform;follow.SetParent(space);
   bool inMatch=true,opponent=false;
   var view=ElasticShotControls.Install(owner,power,follow,null,null,()=>camera,()=>opponent,()=>inMatch);
   var tick=typeof(ElasticShotControls).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance);
   tick.Invoke(view,null);if(!ElasticShotControls.UsingNewAppearance)throw new Exception("Own-turn control missing");
   opponent=true;space.gameObject.SetActive(false);tick.Invoke(view,null);
   if(!ElasticShotControls.UsingNewAppearance||!ElasticShotControls.InputBlocked)throw new Exception("Opponent cover missing");
   inMatch=false;space.gameObject.SetActive(true);tick.Invoke(view,null);
   if(ElasticShotControls.UsingNewAppearance||view.GetComponentInChildren<Canvas>(true).gameObject.activeSelf||power.GetComponent<SpriteRenderer>().enabled)throw new Exception("Control leaked into rematch");
   settings.useNewAppearance=false;tick.Invoke(view,null);if(power.GetComponent<SpriteRenderer>().enabled)throw new Exception("Legacy control leaked into result");
   inMatch=true;opponent=false;tick.Invoke(view,null);if(!power.GetComponent<SpriteRenderer>().enabled)throw new Exception("Legacy control not restored");
   settings.useNewAppearance=true;tick.Invoke(view,null);if(!ElasticShotControls.UsingNewAppearance)throw new Exception("Rematch control not restored");
   Debug.Log("PASS visibility: own turn, opponent cover, match finished, legacy toggle, next match");
  } finally {settings.useNewAppearance=original;UnityEngine.Object.DestroyImmediate(root);}
 }
 static void Roundtrip(MatchReplayPresentation p) {
  using(var stream=new MemoryStream()) {
   p.Write(new BinaryWriter(stream));long bytes=stream.Length;stream.Position=0;
   var copy=MatchReplayPresentation.Read(new BinaryReader(stream),0);
   if(copy.events.Count!=p.events.Count)throw new Exception("Event count");
   for(int i=0;i<p.events.Count;i++)if((p.events[i].kind=="visual-state"?!JsonUtility.FromJson<ReplayVisualState>(copy.events[i].value).Equals(JsonUtility.FromJson<ReplayVisualState>(p.events[i].value)):copy.events[i].value!=p.events[i].value)||copy.events[i].kind!=p.events[i].kind||copy.events[i].time!=p.events[i].time)throw new Exception("Event changed "+i);
   Debug.Log("Replay roundtrip events="+p.events.Count+" storedBytes="+bytes);
  }
 }
 static MemoryStream Legacy(bool invalid) {
  using(var raw=new MemoryStream()) {
   var w=new BinaryWriter(raw);w.Write(invalid?2:1);w.Write(1368);w.Write(768);w.Write(0);w.Write(-3);w.Write(0);w.Write(0);
   for(int i=0;i<4;i++)w.Write("");for(int i=0;i<6;i++)w.Write(0);
   w.Write(1);w.Write(0f);w.Write("test");if(invalid){w.Write(0);w.Write(0);w.Write(0);}w.Write("기존 기록");w.Flush();
   using(var packed=new MemoryStream()) {
    using(var gz=new GZipStream(packed,System.IO.Compression.CompressionLevel.Fastest,true)){raw.Position=0;raw.CopyTo(gz);}
    var result=new MemoryStream();var output=new BinaryWriter(result);output.Write((int)raw.Length);output.Write((int)packed.Length);output.Write(packed.ToArray());output.Flush();result.Position=0;return result;
   }
  }
 }
}

public class RematchControlHost:MonoBehaviour {}
