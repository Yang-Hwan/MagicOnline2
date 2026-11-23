using System;
using System.IO;
using System.Linq;
using Assets.Scripts.Often;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ReplayCoinMenuValidation
{
    const string Request="Logs/replay-coin-menu.request";
    static ReplayCoinMenuValidation(){EditorApplication.update+=()=>{if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete(Request);Run();};}
    [MenuItem("Tools/Gameplay/Validate Replay Coins and Message Menu")]
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();MatchReplayStateDisplay display=null;MatchReplayStateCapture capture=null;int checks=0;
        void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
        try{
            var host=new GameObject("Replay test");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host,scene);
            var assets=MatchReplayAssets.Current;
            string key=assets.particleKeys.First(k=>k.Contains("CoinAni/Root|"));
            var data=new MatchReplayPresentation{presentationVersion=MatchReplayStateCapture.Format};
            void Node(int id,int parent,string name,string kind="transform"){data.events.Add(new MatchReplayPresentation.Event{kind="visual-node",value=JsonUtility.ToJson(new ReplayVisualNode{id=id,parent=parent,name=name,kind=kind,particle=kind=="particle"?key:null,particleBuffer=kind=="particle"})});}
            void State(int id,int frame,Vector3 scale){data.events.Add(new MatchReplayPresentation.Event{kind="visual-state",value=JsonUtility.ToJson(new ReplayVisualState{id=id,frame=frame,active=true,enabled=true,scale=scale,rotation=Quaternion.identity,alpha=1})});}
            Node(0,-1,"Coin container");Node(1,0,"Root","particle");Node(2,-1,"Chat parent");Node(3,2,"EquippedChatMenu");Node(4,3,"Message row");Node(5,2,"Modal Backdrop");Node(6,2,"SelfChat");Node(7,2,"OtherChat");
            for(int i=0;i<8;i++)State(i,0,i==1?Vector3.one*.06f:Vector3.one);
            State(1,1,Vector3.one*.12f);
            display=new MatchReplayStateDisplay(host.transform,data,null);display.Apply(0);
            var particle=display.Root.GetComponentInChildren<ParticleSystem>(true);
            Check(particle.main.scalingMode==ParticleSystemScalingMode.Local,"Coin fixture must use Local scale");
            Check(Vector3.Distance(particle.transform.localScale,Vector3.one*.06f)<.00001f,"Coin particle retains authored .06 scale on its own transform");
            Transform Find(string name)=>display.Root.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
            Check(!Find("EquippedChatMenu").gameObject.activeInHierarchy&&!Find("Message row").gameObject.activeInHierarchy&&!Find("Modal Backdrop").gameObject.activeInHierarchy,"Existing recording hides chooser, rows and dimming");
            Check(Find("SelfChat").gameObject.activeInHierarchy&&Find("OtherChat").gameObject.activeInHierarchy,"Sent speech bubbles remain visible");
            display.Apply(1);Check(Vector3.Distance(particle.transform.localScale,Vector3.one*.12f)<.00001f,"Changing particle scale replays");
            display.Apply(0);Check(Vector3.Distance(particle.transform.localScale,Vector3.one*.06f)<.00001f&&!Find("EquippedChatMenu").gameObject.activeInHierarchy,"Backward seeking restores scale without showing menu");
            display.Dispose();display=null;
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas,scene);
            foreach(string name in new[]{"EquippedChatMenu","EquippedChatMenu Backdrop","SelfChat","OtherChat"}){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(canvas.transform,false);}
            var recorded=new MatchReplayPresentation();capture=new MatchReplayStateCapture(recorded);capture.Capture(scene,null,0);recorded.FlushPending();
            var names=recorded.events.Where(e=>e.kind=="visual-node").Select(e=>JsonUtility.FromJson<ReplayVisualNode>(e.value).name).ToArray();
            Check(!names.Contains("EquippedChatMenu")&&!names.Contains("EquippedChatMenu Backdrop"),"New recordings exclude chooser and backdrop");
            Check(names.Contains("SelfChat")&&names.Contains("OtherChat"),"New recordings retain speech bubbles");
            File.WriteAllText("Logs/replay-coin-menu-result.txt","PASS "+checks+" checks");
        }catch(Exception e){File.WriteAllText("Logs/replay-coin-menu-result.txt",e.ToString());Debug.LogException(e);}
        finally{capture?.Dispose();display?.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
    }
}
