using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Sight.Surface.Pavilion;
using Assets.Scripts.Prack.BackSys.GameData;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QuickShotValidation
{
    const string Root="Logs/QuickShot/";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static QuickShotValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer || !File.Exists(Root+"run.request"))return;
        File.Delete(Root+"run.request");
        try{Run();}catch(Exception ex){File.WriteAllText(Root+"error.txt",ex.ToString());Debug.LogException(ex);}
    }
    public static void Run()
    {
        Directory.CreateDirectory(Root);if(File.Exists(Root+"error.txt"))File.Delete(Root+"error.txt");
        int checks=0,renders=0;var random=UnityEngine.Random.state;
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Pavilion.unity");
        try
        {
            var panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PnlMatch>(true)).First();
            var canvas=panel.GetComponentInParent<Canvas>();var cr=(RectTransform)canvas.transform;
            foreach(var root in scene.GetRootGameObjects())if(root!=canvas.transform.root.gameObject)root.SetActive(false);
            for(Transform t=panel.transform;t;t=t.parent)t.gameObject.SetActive(true);
            foreach(Transform sibling in panel.transform.parent)if(sibling!=panel.transform)sibling.gameObject.SetActive(false);
            foreach(var mb in canvas.GetComponentsInChildren<MonoBehaviour>(true))if(!(mb is Graphic)&&!(mb is CanvasScaler)&&!(mb is Mask)&&!(mb is RectMask2D))mb.enabled=false;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;
            canvas.renderMode=RenderMode.WorldSpace;cr.pivot=Vector2.one*.5f;cr.localScale=Vector3.one;cr.position=Vector3.zero;cr.sizeDelta=new Vector2(1920,1080);
            var cg=new GameObject("Quick Shot Validation Camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cg,scene);
            var camera=cg.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=200;camera.aspect=1920f/400;
            camera.transform.position=new Vector3(0,340,-100);camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.047f);canvas.worldCamera=camera;
            var hud=MatchHudView.Install(panel.transform);hud.SetPlayerName(0,"타이거우즈20");hud.SetPlayerName(1,"초크장인");hud.SetLevel(0,12);hud.SetLevel(1,9);hud.ResetScores(12);hud.SetScore(0,4,false);hud.SetScore(1,3,false);
            Check(!hud.QuickShotEnabled,"quick bonus enabled outside a regular paid multiplayer match");
            typeof(MatchHudView).GetField("QuickShotPreview",Flags).SetValue(hud,true);
            panel.transform.Find("PlayerBoxs/PlayerSelf/TxtMatchCoin").GetComponent<TMP_Text>().text="+1,200";
            panel.transform.Find("PlayerBoxs/PlayerOther/TxtMatchCoin").GetComponent<TMP_Text>().text="+1,000";
            panel.transform.Find("TopCenter/TxtTime").GetComponent<TMP_Text>().text="08:42";
            Check(Math.Abs(MatchHudGraphic.QuickThreshold(0,0,100)-70)<.001 && Math.Abs(MatchHudGraphic.QuickThreshold(1,0,100)-30)<.001,"opponent threshold mirrors the outer 30% segment");
            var art=Resources.Load<Texture2D>("Gameplay/MatchHud/QuickShot");Check(art && art.width>1000,"gold brush artwork loaded");
            foreach(int side in new[]{0,1})
            {
                hud.SetTurn(side);hud.SetTime(.15f,30);hud.AdvanceVisual(10,0);Render("ready-"+side);
                hud.SetTime(.3f,30);Render("boundary-"+side);
                hud.SetTime(.30333f,30);Render("fading-"+side);
                hud.SetTime(.31f,30);Render("expired-"+side);
                hud.SetTime(.15f,30);hud.StopTimer();Render("released-"+side);
                hud.ShowQuickShotBonus(side,200);var motion=hud.GetComponentInChildren<QuickShotBonusMotion>();
                Check(motion && motion.Bonus==200,"bonus title not connected");
                motion.Sample(.75f);
                Check(motion.IsShowing && motion.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="+200"),"actual bonus amount absent");
                Check(motion.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"bonus intercepts table input");
                Render("bonus-"+side);
                motion.Hide();Check(!motion.IsShowing,"bonus does not clear");
            }
            hud.ShowQuickShotBonus(0,200);hud.ResetScores(7);Check(!hud.GetComponentInChildren<QuickShotBonusMotion>().IsShowing,"new match retained bonus animation");
            Check(!hud.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("퀵샷 가능")),"unapproved availability label present");
            using(var vertices=new VertexHelper())
            {
                hud.SetTurn(0);hud.SetTime(.1f,30);var before=UnityEngine.Random.state;
                typeof(MatchHudGraphic).GetMethod("OnPopulateMesh",Flags,null,new[]{typeof(VertexHelper)},null).Invoke(hud.Decoration,new object[]{vertices});
                Check(vertices.currentVertCount<24000,"gold gauge exceeds mobile geometry budget");
                Check(before.Equals(UnityEngine.Random.state),"presentation changed physics random sequence");
            }
            // Identical authenticated shot facts produce the same bonus on both peers and server replay.
            foreach(int balls in new[]{3,4})foreach(int elapsed in new[]{0,8999,9000,9001,30000})
            {
                var a=new ScoreCoinLedger(10000,10000,1000,5);var b=new ScoreCoinLedger(10000,10000,1000,5);
                bool quick=ScoreCoinLedger.QualifiesQuickShot(elapsed,30000);int e=quick?elapsed:-1,d=quick?30000:0;
                a.Add(1,0,1,balls==3?2:1,balls==3,e,d);b.Add(1,0,1,balls==3?2:1,balls==3,e,d);
                var server=ScoreCoinLedger.Replay(10000,10000,1000,5,a.Events,balls==3);
                Check(a.Events==b.Events && a.NetA==server.NetA && a.LastQuickBonus==server.LastQuickBonus,"replicas differ at timing boundary");
                Check((a.LastQuickBonus>0)==quick,"boundary eligibility incorrect");
            }
            File.WriteAllText(Root+"result.json","{\"passed\":true,\"checks\":"+checks+",\"renders\":"+renders+",\"twoPeerCoinReplay\":true}");
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            void Render(string name)
            {
                Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())if(text.enabled)text.ForceMeshUpdate();
                foreach(var g in canvas.GetComponentsInChildren<Graphic>())if(g.enabled){g.SetAllDirty();g.Rebuild(CanvasUpdate.PreRender);g.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();var rt=new RenderTexture(1920,400,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(1920,400,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,400),0,0);tex.Apply();File.WriteAllBytes(Root+name+".png",tex.EncodeToPNG());renders++;
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;}
    }
}
