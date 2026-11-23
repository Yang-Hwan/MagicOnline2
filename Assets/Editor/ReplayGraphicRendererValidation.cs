using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Assets.Scripts.Often;
using Assets.Scripts.Sight.Surface.Pavilion;
public static class ReplayGraphicRendererValidation
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    public static void Run()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var owner=new GameObject("Replay fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner,scene);
        var assets=ScriptableObject.CreateInstance<MatchReplayAssets>();
        var field=typeof(MatchReplayAssets).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        var previous=field.GetValue(null);field.SetValue(null,assets);
        MatchReplayStateDisplay display=null;
        try
        {
            var data=new MatchReplayPresentation{presentationVersion=MatchReplayStateCapture.Format};
            string[] kinds={"sparks","glow","elastic","graphic","image","raw"};
            for(int i=0;i<kinds.Length;i++)
            {
                data.events.Add(new MatchReplayPresentation.Event{kind="visual-node",value=JsonUtility.ToJson(new ReplayVisualNode{id=i,name=i==0?"Impact embers":kinds[i],kind=kinds[i],rectTransform=true})});
                for(int frame=0;frame<2;frame++)
                {
                    var state=new ReplayVisualState{id=i,frame=frame,active=true,enabled=true,scale=Vector3.one,rotation=Quaternion.identity,size=new Vector2(600,200),alpha=1,color=Color.white,canvasTint=frame==0?Color.white:Color.green,fill=.4f+frame*.2f};
                    data.events.Add(new MatchReplayPresentation.Event{kind="visual-state",time=frame,value=JsonUtility.ToJson(state)});
                }
            }
            for(int pass=0;pass<2;pass++)
            {
                display=new MatchReplayStateDisplay(owner.transform,data,null);
                foreach(int frame in new[]{0,1,0})
                {
                    display.Apply(frame);
                    var graphics=display.Root.GetComponentsInChildren<Graphic>(true);
                    Check(graphics.Length==kinds.Length,"Missing replay graphics");
                    foreach(var graphic in graphics)
                    {
                        var renderer=graphic.GetComponent<CanvasRenderer>();
                        Check(renderer&&graphic.canvasRenderer==renderer,"Missing CanvasRenderer on "+graphic.name);
                        Check(renderer.GetColor()==(frame==0?Color.white:Color.green),"Tint/seek restoration failed on "+graphic.name);
                    }
                }
                display.Dispose();display=null;
            }
            var live=new GameObject("Live sparks",typeof(RectTransform),typeof(TechniqueSparkGraphic));live.transform.SetParent(owner.transform,false);
            Check(live.GetComponent<CanvasRenderer>(),"Live sparks dependency missing");
            File.WriteAllText("Logs/replay-graphic-renderer-result.txt","PASS "+checks+" checks");
        }
        catch(Exception e){File.WriteAllText("Logs/replay-graphic-renderer-result.txt",e.ToString());Debug.LogException(e);}
        finally{display?.Dispose();field.SetValue(null,previous);UnityEngine.Object.DestroyImmediate(assets);UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
    }
}
