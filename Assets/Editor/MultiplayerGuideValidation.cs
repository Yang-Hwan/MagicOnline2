using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Assets.Scripts.Sight.Vital.Pavilion;

public static class MultiplayerGuideValidation
{
    public static void Run()
    {
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var rotation=typeof(ShotCtrl).GetMethod("SettleNetworkRotation",flags);
        var position=typeof(ShotCtrl).GetMethod("SettleNetworkPosition",flags);
        foreach(int fps in new[]{30,60,120})
        foreach(float angle in new[]{0f,.02f,359.98f,90f,180f})
        {
            var target=Quaternion.Euler(0,angle,0);
            var current=Quaternion.Euler(0,359.9f,0);
            Vector3 point=new Vector3(.01f,-.02f,.03f),destination=new Vector3(.2f,.1f,-.5f);
            for(int frame=0;frame<fps*4;frame++)
            {
                current=(Quaternion)rotation.Invoke(null,new object[]{current,target,5f/fps});
                point=(Vector3)position.Invoke(null,new object[]{point,destination,5f/fps});
            }
            if(!current.Equals(target) || !point.Equals(destination))throw new Exception("Remote aim never settled: "+fps+" / "+angle);
            for(int frame=0;frame<120;frame++)
            {
                var next=(Quaternion)rotation.Invoke(null,new object[]{current,target,5f/fps});
                if(!next.Equals(current))throw new Exception("Stationary remote aim jitter");
            }
        }
        var root=new GameObject("Guide regression");root.SetActive(false);
        try
        {
            var shot=root.AddComponent<ShotCtrl>();
            var field=typeof(ShotCtrl).GetField("predictionDirty",BindingFlags.NonPublic|BindingFlags.Instance);
            var request=typeof(ShotCtrl).GetMethod("TryCalculateShot",BindingFlags.NonPublic|BindingFlags.Instance);
            field.SetValue(shot,false);
            for(int i=0;i<120;i++) request.Invoke(shot,new object[]{true});
            if((bool)field.GetValue(shot))throw new Exception("Unchanged input invalidated preview");
            field.SetValue(shot,true);request.Invoke(shot,new object[]{true});
            if(!(bool)field.GetValue(shot))throw new Exception("Explicit geometry/turn invalidation lost");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("PASS guide regression: wraparound, finite interpolation, stationary stability at 30/60/120fps, repeated requests, explicit invalidation");
    }
}
