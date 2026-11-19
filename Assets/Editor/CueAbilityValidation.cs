using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Exert.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Shot=Assets.Scripts.Sight.Vital.Pavilion.ShotCtrl;
using Ball=Assets.Scripts.Sight.Vital.Pavilion.Ball;
using Player=Assets.Scripts.Exert.Match.PoolPlayer;
using PracticeShot=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.ShotCtrl;
using PracticeBall=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.BallC;
using TipGuide=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CuePointGuide;
[InitializeOnLoad]
public static class CueAbilityValidation
{
    const string Root="Logs/CueAbilities/";const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Static;
    static CueAbilityValidation(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Root+"run.request"))return;
        File.Delete(Root+"run.request");try{Run();}catch(Exception ex){File.WriteAllText(Root+"error.txt",ex.ToString());Debug.LogException(ex);}
    }
    public static void Run()
    {
        Directory.CreateDirectory(Root);if(File.Exists(Root+"error.txt"))File.Delete(Root+"error.txt");int checks=0;
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Cue Ability Validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);root.SetActive(false);
        var runtime=typeof(MatchEquipmentRuntime);var cached=runtime.GetField("localProfile",Flags);var owner=runtime.GetField("profileOwner",Flags);
        object savedProfile=cached.GetValue(null),savedOwner=owner.GetValue(null);
        var practiceField=runtime.GetField("practiceProfile",Flags);var runField=runtime.GetField("practiceRun",Flags);var shotField=runtime.GetField("practiceShot",Flags);object savedPractice=practiceField.GetValue(null),savedRun=runField.GetValue(null),savedShot=shotField.GetValue(null);var savedPlayers=Player.players;int savedTurn=Player.turnId;
        var random=UnityEngine.Random.state;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        try
        {
            var ability=new CueAbilitySettings{Grade="B",Initial=new[]{60,70,55,55},Maximum=new[]{100,100,100,100},Reach=2};
            var profile=new MatchEquipmentProfile{Ability=ability,Power=60,FollowThrough=70,Tip=55,ShotSeconds=55,CueTimeMin=20,CueTimeMax=40};
            cached.SetValue(null,profile);owner.SetValue(null,Assets.Scripts.Exert.BackSys.GameApiClient.UserId);
            MatchEquipmentRuntime.BeginPracticeRun();Check(Guid.TryParseExact((string)runtime.GetField("practiceRun",Flags).GetValue(null),"D",out _),"progress request uses JSON-compatible GUID format");
            Player.players=new[]{new Player(0,"validation",0,"cue-test-0",null,""),new Player(1,"validation",0,"cue-test-1",null,"")};Player.SetTurn(0);
            var shot=root.AddComponent<Shot>();var ballGO=new GameObject("Cue ball",typeof(SphereCollider),typeof(Rigidbody));ballGO.transform.SetParent(root.transform);
            ballGO.GetComponent<SphereCollider>().radius=.03f;var body=ballGO.GetComponent<Rigidbody>();body.mass=.15f;var ball=ballGO.AddComponent<Ball>();typeof(Ball).GetProperty("body").SetValue(ball,body);typeof(Shot).GetProperty("cueBall").SetValue(shot,ball);
            Transform Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go.transform;}
            shot.cueSlider=Child("cue-slider");shot.cueVertical=Child("cue-vertical");shot.cueDisplacement=Child("cue-tip");shot.cueDisplacement.position=new Vector3(.005f,.005f,0);
            shot.SetFollowThrough(1);typeof(Shot).GetProperty("force").SetValue(shot,1f);
            var strokeRandom=UnityEngine.Random.state;
            var actual=shot.BuildShotImpulse();var guide=shot.BuildGuideImpulse();
            Check(Mathf.Abs(profile.PowerScale-.54f)<.00001f && Mathf.Abs(actual.followThrough-.63f)<.00001f,"B 60/70 caps power/follow at 54/63 percent");
            Check(actual.point==guide.point && actual.impulse==guide.impulse && actual.followThrough==guide.followThrough && actual.spinPersistence==guide.spinPersistence,"Pavilion guide matches released stroke");
            typeof(Shot).GetProperty("force").SetValue(shot,.5f);var half=shot.BuildGuideImpulse();Check(Mathf.Abs(half.impulse.magnitude/actual.impulse.magnitude-.5f)<.00001f,"power UI half position yields half effective power");
            Check(profile.TurnSeconds(20)==30 && profile.TipScale==.5f,"Cue/Sec 20 to 40 and tip midpoint rounding");
            Check(strokeRandom.Equals(UnityEngine.Random.state),"stroke and guide calculations leave physics random state unchanged");
            var practiceObject=new GameObject("Practice shot");practiceObject.transform.SetParent(root.transform);var ps=practiceObject.AddComponent<PracticeShot>();
            var pb=ballGO.AddComponent<PracticeBall>();pb.body=body;ps.cueBall=pb;
            ps.cueSlider=shot.cueSlider;ps.cueVertical=shot.cueVertical;ps.cueDisplacement=shot.cueDisplacement;ps.SetFollowThrough(1);
            foreach(float force in new[]{.25f,.5f,1f})
            {
                typeof(PracticeShot).GetProperty("force").SetValue(ps,force);var a=ps.BuildShotImpulse();var g=ps.BuildGuideImpulse();
                Check(a.point==g.point && a.impulse==g.impulse && a.followThrough==g.followThrough && a.spinPersistence==g.spinPersistence,"practice guide equals actual stroke at "+force);
            }
            var canvasGO=new GameObject("Tip canvas",typeof(RectTransform),typeof(Canvas));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasGO,scene);
            var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;var cr=(RectTransform)canvasGO.transform;cr.sizeDelta=new Vector2(440,440);
            var frame=(RectTransform)new GameObject("Tip Frame",typeof(RectTransform)).transform;frame.SetParent(canvasGO.transform,false);frame.sizeDelta=new Vector2(400,400);
            var marker=(RectTransform)new GameObject("Tip Marker",typeof(RectTransform)).transform;marker.SetParent(frame,false);marker.sizeDelta=new Vector2(20,20);
            TipGuide.Install(frame);var graphic=frame.GetComponentInChildren<TipGuide>();
            Check(Mathf.Abs(TipGuide.Limit(frame,marker)-70)<.001f,"tip movement fits within half-radius faint circle");
            using(var vh=new VertexHelper())
            {
                typeof(TipGuide).GetMethod("OnPopulateMesh",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(VertexHelper)},null).Invoke(graphic,new object[]{vh});
                var a=new UIVertex();var b=new UIVertex();vh.PopulateUIVertex(ref a,580);vh.PopulateUIVertex(ref b,581);
                Check(Mathf.Abs((a.position.magnitude+b.position.magnitude)*.5f-80)<.01f,"visible inner line radius is exactly 50 percent");
            }
            profile.Tip=0;Check(TipGuide.Limit(frame,marker)==0,"zero tip level permits centre only");profile.Tip=55;
            var cameraGO=new GameObject("Tip render camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO,scene);var camera=cameraGO.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=220;camera.transform.position=new Vector3(0,0,-100);camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.07f,.08f);canvas.worldCamera=camera;
            Render("tip-b50");profile.Ability=new CueAbilitySettings();profile.Tip=100;Render("tip-a100");
            Check(frame.rect.width==400 && marker.rect.width==20,"tip selector keeps its original UI size");
            File.WriteAllText(Root+"native-result.json","{\"passed\":true,\"checks\":"+checks+",\"guideEqualsShot\":true,\"tipHalfRadius\":true}");
            void Render(string name)
            {
                graphic.SetAllDirty();Canvas.ForceUpdateCanvases();graphic.Rebuild(CanvasUpdate.PreRender);var rt=new RenderTexture(440,440,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(440,440,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,440,440),0,0);tex.Apply();File.WriteAllBytes(Root+name+".png",tex.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        finally{cached.SetValue(null,savedProfile);owner.SetValue(null,savedOwner);practiceField.SetValue(null,savedPractice);runField.SetValue(null,savedRun);shotField.SetValue(null,savedShot);Player.players=savedPlayers;typeof(Player).GetProperty("turnId").SetValue(null,savedTurn);EditorSceneManager.ClosePreviewScene(scene);UnityEngine.Random.state=random;}
    }
}
