using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Pavilion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MatchPlayerInfoValidation
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [MenuItem("Tools/Magic Online/Validate Player Information")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play mode first");
        var scene=EditorSceneManager.NewPreviewScene(); int checks=0;
        try
        {
            Directory.CreateDirectory("Logs/PlayerInformation");
            var cameraRoot=new GameObject("Profile preview camera",typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot,scene);
            var camera=cameraRoot.GetComponent<Camera>();camera.scene=scene;camera.enabled=false;camera.orthographic=true;
            camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.055f);
            var canvasRoot=new GameObject("Profile preview canvas",typeof(RectTransform),typeof(Canvas));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot,scene);
            var canvas=canvasRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            var window=canvasRoot.AddComponent<MatchPlayerInfoWindow>();window.Initialize(null);
            var data=new MatchPlayerInfo {
                Nickname="타이거우즈20",Level=12,TodayStreak=3,TotalPrize=30000000,CueName="인페르노",CueDescription="검은 뿔과 붉은 용암 문양의 큐대",CueThumbnail="Gameplay/ShopCues/Inferno",
                Total=new PlayerRecord{Wins=151,Losses=99},FourBall=new PlayerRecord{Wins=60,Losses=40},ThreeBall=new PlayerRecord{Wins=91,Losses=59},
                Equipment=new MatchEquipmentProfile { Power=20,FollowThrough=-10,Tip=-5,ShotSeconds=-2,
                    Emotions=new[]{new MatchEquipmentEmotion{Code="기본",Asset="Gameplay/BandanaAvatar/Neutral"}} }
            };
            Build();Render(1280,720,"with-streak");
            Check(Find("TodayStreak"),"daily winning streak badge shown");
            Check(Text("Nickname").text==data.Nickname && !Text("Nickname").richText,"nickname is data with rich text disabled");
            Check(Text("Level").text=="Lv.12" && Text("Level").rectTransform.anchoredPosition.x>Text("Nickname").rectTransform.anchoredPosition.x,"small level is beside nickname");
            Check(Text("Amount").text=="30,000,000 코인" && Text("WinRate").text=="60.4%","actual prize and win rate formatting");
            Check(Find("CueImage").GetComponent<Image>().overrideSprite,"real equipped cue artwork loaded");
            Check(window.GetComponentsInChildren<TMP_Text>().All(t=>!t.text.Contains("8승 3패")),"no daily win-loss field");
            window.Close();Check(!MatchPlayerInfoWindow.IsOpen,"close releases shot input lock");
            data.TodayStreak=0;Build();Render(1024,768,"without-streak");
            Check(!Find("TodayStreak"),"no daily streak leaves blank space");
            window.Close();data.TodayStreak=1;Build();Render(960,540,"single-win");
            Check(!Find("TodayStreak"),"single win is not labelled a winning streak");
            // The timer hit target is its fixed track even when the fill is empty.
            var track=new GameObject("TimeInning",typeof(RectTransform));track.transform.SetParent(canvasRoot.transform,false);
            var rect=track.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(420,40);
            var fill=new GameObject("Bar",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track.transform,false);fill.GetComponent<RectTransform>().sizeDelta=Vector2.zero;
            int taps=0;
            var bind=typeof(MatchEquipmentChat).GetMethod("Bind",BindingFlags.NonPublic|BindingFlags.Static);
            var button=(Button)bind.Invoke(null,new object[]{rect,new UnityEngine.Events.UnityAction(()=>taps++)});
            button.onClick.Invoke();Check(taps==1 && button.GetComponent<Image>().raycastTarget && rect.rect.width==420,"empty timer fill retains full message click target");
            window.Close();File.WriteAllText("Logs/PlayerInformation/result.json","{\"Ok\":true,\"Checks\":"+checks+",\"Renders\":3}");
            Debug.Log("[PlayerInfoValidation] PASS "+checks+" checks, 3 renders");
            Transform Find(string name)=>window.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
            TMP_Text Text(string name)=>Find(name).GetComponent<TMP_Text>();
            void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
            void Build(){typeof(MatchPlayerInfoWindow).GetMethod("BuildShell",Flags).Invoke(window,null);typeof(MatchPlayerInfoWindow).GetMethod("Draw",Flags).Invoke(window,new object[]{data,0});}
            void Render(int width,int height,string name)
            {
                var rt=new RenderTexture(width,height,24);camera.targetTexture=rt;camera.aspect=(float)width/height;
                camera.orthographicSize=height*.5f;canvasRoot.GetComponent<RectTransform>().sizeDelta=new Vector2(width,height);
                Canvas.ForceUpdateCanvases();typeof(MatchPlayerInfoWindow).GetMethod("Resize",Flags).Invoke(window,null);
                foreach(var fallback in window.GetComponentsInChildren<CueImageFallback>())typeof(CueImageFallback).GetMethod("LateUpdate",Flags).Invoke(fallback,null);
                foreach(var text in window.GetComponentsInChildren<TMP_Text>()) {text.ForceMeshUpdate(); if(!string.IsNullOrWhiteSpace(text.text) && text.enabled && text.textInfo.characterCount==0)throw new Exception("Text mesh missing: "+text.name);}
                foreach(var graphic in window.GetComponentsInChildren<Graphic>()) { graphic.SetAllDirty();graphic.Rebuild(CanvasUpdate.PreRender);graphic.Rebuild(CanvasUpdate.LatePreRender); }
                Canvas.ForceUpdateCanvases();camera.Render();
                var old=RenderTexture.active;RenderTexture.active=rt;
                var texture=new Texture2D(width,height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
                File.WriteAllBytes("Logs/PlayerInformation/"+name+".png",texture.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
}
