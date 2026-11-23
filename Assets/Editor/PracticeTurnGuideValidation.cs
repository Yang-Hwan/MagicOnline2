using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Shot = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl;
using Manager = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.PhysicsMng;
using Ball = Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC;
using Player = Assets.TutorialInfo.Scripts.TableSet06.Excert.Match.PoolPlayer;

public static class PracticeTurnGuideValidation
{
    [MenuItem("Tools/Gameplay/Validate Practice Turn Power Reset")]
    public static void Run()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var root=new GameObject("Practice turn guide regression");root.SetActive(false);
        int savedTurn=Player.turnId;
        try
        {
            Transform Child(string name) { var t=new GameObject(name).transform;t.SetParent(root.transform);return t; }
            var manager=root.AddComponent<Manager>();var shot=root.AddComponent<Shot>();
            typeof(Shot).GetField("physicsManager",fields).SetValue(shot,manager);
            shot.cuePivot=Child("pivot");shot.cueVertical=Child("vertical");shot.cueDisplacement=Child("tip");
            shot.cueSlider=Child("slider");shot.forceHandle=Child("power");
            foreach(int ballCount in new[]{3,4})
            {
                manager.ballcs=new Ball[ballCount];
                for(int i=0;i<ballCount;i++) {
                    var t=Child("ball "+i);t.gameObject.AddComponent<SphereCollider>();
                    var body=t.gameObject.AddComponent<Rigidbody>();body.useGravity=false;
                    var ball=t.gameObject.AddComponent<Ball>();ball.body=body;manager.ballcs[i]=ball;
                }
                // Both alternating turns and consecutive scoring turns must clear a prior weak shot.
                foreach(int turn in new[]{0,1,0,0,1})
                foreach(float previousPower in new[]{.01f,.08f,.5f,1f})
                {
                    typeof(Player).GetProperty("turnId").SetValue(null,turn);
                    typeof(Shot).GetProperty("force").SetValue(shot,previousPower);
                    typeof(Shot).GetField("forceSliderYPos",fields).SetValue(shot,-.91f*previousPower);
                    typeof(Shot).GetField("predictionDirty",fields).SetValue(shot,false);
                    shot.forceHandle.localPosition=Vector3.down*.4f;shot.cueSlider.localPosition=Vector3.forward*.1f;
                    shot.CueReadyShot();
                    if(shot.force!=0 || (float)typeof(Shot).GetField("forceSliderYPos",fields).GetValue(shot)!=0)
                        throw new Exception("Previous shot power leaked into the next guide");
                    if(shot.forceHandle.localPosition!=Vector3.zero || shot.cueSlider.localPosition!=Vector3.zero)
                        throw new Exception("Handle and cue did not reset together");
                    if(shot.cueBall!=manager.ballcs[turn] || manager.cuball!=manager.ballcs[turn].body)
                        throw new Exception("Guide uses the wrong cue ball");
                    if(!(bool)typeof(Shot).GetField("predictionDirty",fields).GetValue(shot))
                        throw new Exception("Next-turn prediction was not invalidated");
                }
            }
            Debug.Log("PASS practice turn guide reset: 40 cases, 3/4 balls, weak/full power, alternate/consecutive turns");
        }
        finally { typeof(Player).GetProperty("turnId").SetValue(null,savedTurn);UnityEngine.Object.DestroyImmediate(root); }
    }
}
