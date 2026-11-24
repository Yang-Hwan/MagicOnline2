using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.GameData;
public static class CueGuideLengthValidation
{
    static int checks;
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);checks++;}
    public static void Run()
    {
        var profile=(MatchEquipmentProfile)typeof(MatchEquipmentRuntime).GetField("basic",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        int saved=profile.Guide;var ability=profile.Ability;
        var root=new GameObject("Inactive guide fixture");root.SetActive(false);
        var material=new Material(Shader.Find("UI/Default"));
        try
        {
            profile.Ability=null;
            var bentPath=new List<Vector3>{Vector3.zero,new Vector3(3,0,0),new Vector3(3,0,4)};
            Check(!Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,4.6f,.5f,1),"Target hidden before contact");
            Check(Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,6,.5f,1),"Target visible exactly at contact");
            Check(Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,4.6f,1,1),"Target visible past contact");
            Check(!Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,6,1,2),"Use bent path distance, not straight distance");
            Check(!Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,10,1,-1),"No predicted collision means no target guide");
            Check(!Assets.Scripts.Often.CueGuideLength.ReachesContact(bentPath,10,0,1),"Zero guide hides target");
            Check(ReferenceEquals(MatchEquipmentRuntime.Shooter,profile),"Fixture must use baseline equipment");
            foreach(var type in new[]{typeof(Assets.Scripts.Sight.Vital.Pavilion.ShotCtrl),typeof(Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl)})
            {
                var child=new GameObject(type.FullName);child.transform.SetParent(root.transform);
                var shot=child.AddComponent(type);var line=child.AddComponent<LineRenderer>();
                var path=new List<Vector3>{Vector3.zero,new Vector3(3,0,0),new Vector3(3,0,4)};
                var method=type.GetMethod("RenderPredictedPath",BindingFlags.Instance|BindingFlags.NonPublic);
                void Draw(float cap)=>method.Invoke(shot,new object[]{new[]{line},new[]{material},path,cap});
                profile.Guide=100;Draw(4.6f);Check(Vector3.Distance(line.GetPosition(line.positionCount-1),new Vector3(3,0,1.6f))<.001f,"100% original cue path");
                profile.Guide=50;Draw(4.6f);Check(Vector3.Distance(line.GetPosition(line.positionCount-1),new Vector3(2.3f,0,0))<.001f,"50% cue path");
                Draw(float.PositiveInfinity);Check(Vector3.Distance(line.GetPosition(line.positionCount-1),new Vector3(3,0,.5f))<.001f,"50% target path across corner");
                profile.Guide=0;Draw(float.PositiveInfinity);Check(line.positionCount==0,"0% must hide guide");
                profile.Guide=100;Draw(float.PositiveInfinity);Check(line.GetPosition(line.positionCount-1)==new Vector3(3,0,4),"100% target path");
                Check(path.Count==3&&path[2]==new Vector3(3,0,4),"Prediction geometry was modified");
            }
            File.WriteAllText("Logs/cue-guide-length-result.txt","PASS "+checks+" checks");
        }
        catch(Exception e){File.WriteAllText("Logs/cue-guide-length-result.txt",e.ToString());Debug.LogException(e);}
        finally{profile.Guide=saved;profile.Ability=ability;UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(material);}
    }
}
