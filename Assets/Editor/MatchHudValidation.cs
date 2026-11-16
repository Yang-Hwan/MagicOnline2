using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MatchHudValidation
{
    const string Request="Logs/MatchHud/validate.request";
    const string BuildRequest="Logs/MatchHud/build.request";
    static MatchHudValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)return;
        if(File.Exists(BuildRequest))
        {
            File.Delete(BuildRequest);
            try
            {
                Run();Directory.CreateDirectory("Build/HudVisualFix");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName="Build/HudVisualFix/MagicOnline2.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("HUD build failed: "+report.summary.result);
                File.WriteAllText("Logs/MatchHud/build.json","{\"Ok\":true,\"Executable\":\"Build/HudVisualFix/MagicOnline2.exe\",\"Errors\":"+report.summary.totalErrors+"}");
            }
            catch(Exception ex){File.WriteAllText("Logs/MatchHud/build-error.txt",ex.ToString());Debug.LogException(ex);}
            return;
        }
        if(!File.Exists(Request))return;
        File.Delete(Request);try{Run();}catch(Exception ex){File.WriteAllText("Logs/MatchHud/error.txt",ex.ToString());Debug.LogException(ex);}
    }
    [MenuItem("Tools/Magic Online/Validate Match HUD")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode first");
        Directory.CreateDirectory("Logs/MatchHud");int checks=0,renders=0;var random=UnityEngine.Random.state;
        var scene=EditorSceneManager.OpenPreviewScene("Assets/Scenes/Pavilion.unity");
        try
        {
            var panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PnlMatch>(true)).First();
            var canvas=panel.GetComponentInParent<Canvas>();var canvasRect=canvas.GetComponent<RectTransform>();
            foreach(var root in scene.GetRootGameObjects())if(root!=canvas.transform.root.gameObject)root.SetActive(false);
            for(Transform t=panel.transform;t;t=t.parent)t.gameObject.SetActive(true);
            foreach(Transform sibling in panel.transform.parent)if(sibling!=panel.transform)sibling.gameObject.SetActive(false);
            foreach(var mb in canvas.GetComponentsInChildren<MonoBehaviour>(true))if(!(mb is Graphic)&&!(mb is CanvasScaler)&&!(mb is Mask)&&!(mb is RectMask2D))mb.enabled=false;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;
            canvas.renderMode=RenderMode.WorldSpace;canvasRect.pivot=Vector2.one*.5f;canvasRect.localScale=Vector3.one;canvasRect.position=Vector3.zero;canvasRect.sizeDelta=new Vector2(1920,1080);
            var cameraRoot=new GameObject("Match HUD validation camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=135;camera.aspect=1920f/270;
            camera.transform.position=new Vector3(0,405,-100);camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.047f);canvas.worldCamera=camera;
            var hud=MatchHudView.Install(panel.transform);hud.SetPlayerName(0,"타이거우즈20");hud.SetPlayerName(1,"초크장인");hud.SetLevel(0,12);hud.SetLevel(1,9);
            panel.transform.Find("PlayerBoxs/PlayerSelf/Avatar").GetComponent<Image>().sprite=MatchAvatarReactions.NeutralForSeat(0);
            panel.transform.Find("PlayerBoxs/PlayerOther/Avatar").GetComponent<Image>().sprite=MatchAvatarReactions.NeutralForSeat(1);
            typeof(MatchHudView).GetMethod("SyncPortraits",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);
            panel.transform.Find("PlayerBoxs/PlayerSelf/TxtMatchCoin").GetComponent<TMP_Text>().text="+300,000";
            panel.transform.Find("PlayerBoxs/PlayerOther/TxtMatchCoin").GetComponent<TMP_Text>().text="+120,000";
            panel.transform.Find("PlayerBoxs/PlayerSelf/TxtCoin").GetComponent<TMP_Text>().text="12,345,678";
            panel.transform.Find("PlayerBoxs/PlayerOther/TxtCoin").GetComponent<TMP_Text>().text="987,654,321";
            panel.transform.Find("TopCenter/TxtTime").GetComponent<TMP_Text>().text="08:42";
            Check(Resources.Load<Texture2D>("Gameplay/MatchHud/BlackGoldShell").width>=2000,"full quality painted shell loads");
            Check(MatchHudGraphic.IsRedRing(5)&&MatchHudGraphic.IsRedRing(9)&&!MatchHudGraphic.IsRedRing(4)&&!MatchHudGraphic.IsRedRing(10),"exact red range is 6 through 10");
            Check(hud.DesignRoot.GetComponentInChildren<MatchHudArtGraphic>().mainTexture.width>=2000,"original concept VS/chrome atlas loaded");
            foreach(var name in new[]{"PlayerSelf","PlayerOther"})
            {
                var nickname=panel.transform.Find("PlayerBoxs/"+name+"/TxtName").GetComponent<TMP_Text>();
                Check(nickname.fontSharedMaterial.GetFloat("_OutlineWidth")>=.2f&&nickname.fontSharedMaterial.GetColor("_OutlineColor").r<.05f,"dark SDF nickname outline "+name);
            }
            foreach(int target in new[]{8,12,15})
            {
                hud.ResetScores(target);hud.SetTime(.25f,30);hud.SetTurn(0);hud.SetTime(.25f,30);hud.AdvanceVisual(0,1);
                Render("target-"+target);
                Check(hud.Counters.All(n=>n.Target==target),"target preserved "+target);
                hud.SetScore(0,target);hud.SetScore(1,target);
                for(int n=0;n<target;n++)hud.AdvanceVisual((n+1)*1.25f,1.25f);
                Check(hud.Counters.All(n=>n.Displayed==target&&n.Moving<0),"all rings complete including target 15");
                Render("complete-"+target);
            }
            hud.ResetScores(12);hud.AdvanceVisual(50,1);hud.SetScore(0,4,false);hud.SetScore(1,3,false);hud.SetSeries("10","7");hud.SetTurn(0);hud.SetTime(.3f,30);hud.AdvanceVisual(51,1);
            var initial=Render("self-turn");hud.AdvanceVisual(51.8f,.8f);Check(initial!=Render("self-turn-reflection"),"turn reflection and bubbles animate");
            hud.SetScore(0,5);hud.AdvanceVisual(52.16f,.36f);Render("score-travel");hud.AdvanceVisual(52.58f,.42f);Render("score-landing");hud.AdvanceVisual(53.2f,.62f);
            Check(hud.Counters[0].Displayed==5,"score move settles at confirmed score");
            hud.SetScore(0,4);hud.AdvanceVisual(54.5f,1.3f);Check(hud.Counters[0].Displayed==4,"foul returns one ring");
            hud.ResetScores(5);hud.SetScore(0,4,false);hud.SetTurn(0);hud.SetTime(.7f,30);hud.AdvanceVisual(55,1);Render("mobile-five-self",879);
            hud.SetTurn(1);hud.SetTime(.3f,30);hud.AdvanceVisual(56,1);Render("mobile-five-opponent",879);
            foreach (int active in new[]{0,1})
            {
                hud.SetTurn(active);
                for (int side=0; side<2; side++)
                {
                    var player=side==0?"PlayerSelf":"PlayerOther";
                    var mask=panel.transform.Find("PlayerBoxs/"+player+"/EquippedPortraitMask");
                    var glow=mask.Find("TurnGlow").GetComponent<Graphic>();
                    var shadow=mask.Find("WaitingShadow").GetComponent<Graphic>();
                    var face=mask.Find("EquippedPortrait");
                    Check(glow.enabled==(side==active) && shadow.enabled==(side!=active)
                        && glow.transform.GetSiblingIndex()<face.GetSiblingIndex() && shadow.transform.GetSiblingIndex()>face.GetSiblingIndex(),
                        "portrait lighting and shadow follow turn "+active+" for "+player);
                }
            }
            hud.ResetScores(12);hud.SetScore(0,4,false);hud.SetScore(1,3,false);
            float selfStart=MatchHudGraphic.RingX(0,7,4,12,0,290,9.4f),selfEnd=MatchHudGraphic.RingX(0,7,5,12,0,290,9.4f);
            Check(selfEnd>selfStart&&MatchHudGraphic.RingX(1,7,5,12,0,290,9.4f)<MatchHudGraphic.RingX(1,7,4,12,0,290,9.4f),"opponent direction mirrors self");
            hud.SetTurn(1);hud.SetTime(.86f,30);hud.AdvanceVisual(55.5f,1);Render("opponent-warning");Check(hud.RemainingSeconds<5&&hud.TimerRunning,"urgent threshold uses actual seconds");
            hud.SetTime(.95f,30);hud.AdvanceVisual(56.5f,1);Render("opponent-critical-red");
            hud.StopTimer();Check(!hud.TimerRunning,"shot stops boiling warning");Render("shot-timer-stopped");
            hud.SetSeries("","");Check(!hud.IsSeries,"no rematch leaves both badges absent");Render("no-rematch");
            hud.SetSeries("10","7");
            foreach(var name in new[]{"PlayerSelf","PlayerOther"})
            {
                var track=(RectTransform)panel.transform.Find("PlayerBoxs/"+name+"/TimeInning");
                var balance=track.parent.Find("TxtCoin").GetComponent<TMP_Text>();
                Check(balance.isActiveAndEnabled && balance.rectTransform.anchoredPosition.y-balance.rectTransform.rect.height>track.anchoredPosition.y,"coin balance visible above timer "+name);
                var bind=typeof(MatchEquipmentChat).GetMethod("Bind",BindingFlags.NonPublic|BindingFlags.Static);int taps=0;
                var button=(Button)bind.Invoke(null,new object[]{track,new UnityEngine.Events.UnityAction(()=>taps++)});button.onClick.Invoke();
                Check(taps==1&&track.rect.width==474&&track.GetComponent<Image>().raycastTarget,"full timer remains clickable "+name);
                Check(panel.transform.Find("PlayerBoxs/"+name+"/Avatar").GetComponent<Image>().raycastTarget,"original profile click target retained "+name);
            }
            hud.SetTime(1,30);Render("empty-timer");
            canvasRect.sizeDelta=new Vector2(1440,1080);typeof(MatchHudView).GetMethod("RefreshLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hud,null);Render("four-by-three");
            Check(hud.DesignRoot.localScale.x<1,"narrow display reserves both side controls");
            Check(!hud.Decoration.raycastTarget&&hud.GetComponentsInChildren<Collider>().Length==0,"effects do not capture shots or alter physics");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(PnlMatch).GetField("matchHud",flags).SetValue(panel,hud);
            typeof(PnlMatch).GetField("hitTarget",flags).SetValue(panel,15);
            typeof(PnlMatch).GetField("hitCntSelf",flags).SetValue(panel,0);
            typeof(PnlMatch).GetField("hitCntOther",flags).SetValue(panel,0);
            hud.ResetScores(15);
            typeof(PnlMatch).GetMethod("HitSelf",flags).Invoke(panel,new object[]{15});
            typeof(PnlMatch).GetMethod("HitOther",flags).Invoke(panel,new object[]{15});
            Check(hud.Counters.All(n=>n.Desired==15),"real PnlMatch scoring handlers drive target 15");
            hud.SetScore(0,2,false);hud.SetScore(0,5);hud.SetScore(0,3);
            for(int i=0;i<5;i++)hud.AdvanceVisual(60+i*1.3f,1.3f);
            Check(hud.Counters[0].Displayed==3,"score correction while moving settles without duplicate rings");
            using(var vertices=new VertexHelper())
            {
                var beforeGeometry=UnityEngine.Random.state;
                hud.AdvanceVisual(hud.VisualTime+.016f,.016f);
                typeof(MatchHudGraphic).GetMethod("OnPopulateMesh",flags,null,new[]{typeof(VertexHelper)},null).Invoke(hud.Decoration,new object[]{vertices});
                Check(vertices.currentVertCount<24000&&vertices.currentVertCount>1000,"bounded mobile UI vertex budget");
                Check(beforeGeometry.Equals(UnityEngine.Random.state),"cosmetic animation leaves deterministic physics random state unchanged");
            }
            if(File.Exists("Logs/MatchHud/error.txt"))File.Delete("Logs/MatchHud/error.txt");
            File.WriteAllText("Logs/MatchHud/validation.json","{\"Ok\":true,\"Checks\":"+checks+",\"Renders\":"+renders+"}");Debug.Log("[MatchHudValidation] PASS "+checks+" checks, "+renders+" renders");
            void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
            string Render(string name,int outputWidth=0)
            {
                Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())if(text.enabled)text.ForceMeshUpdate();
                foreach(var graphic in canvas.GetComponentsInChildren<Graphic>())if(graphic.enabled){graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender);}
                Canvas.ForceUpdateCanvases();int width=outputWidth>0?outputWidth:Mathf.RoundToInt(canvasRect.rect.width),height=Mathf.RoundToInt(270*width/canvasRect.rect.width);camera.aspect=canvasRect.rect.width/270f;var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();var png=tex.EncodeToPNG();File.WriteAllBytes("Logs/MatchHud/"+name+".png",png);renders++;
                string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(png));RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);return hash;
            }
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;}
    }
}
