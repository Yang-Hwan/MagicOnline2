using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Assets.Scripts.Often;
public static class WaitingPracticeValidation
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks++;}
    public static void Run()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var host=new GameObject("Coordinator");SceneManager.MoveGameObjectToScene(host,scene);
        var flow=host.AddComponent<WaitingRoomPractice>();
        var camera=new GameObject("LobbyCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(camera,scene);
        var canvas=new GameObject("LobbyCanvas",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(canvas,scene);
        var events=new GameObject("LobbyEvents",typeof(EventSystem));SceneManager.MoveGameObjectToScene(events,scene);
        var disabled=new GameObject("DisabledCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(disabled,scene);disabled.GetComponent<Camera>().enabled=false;
        var type=typeof(WaitingRoomPractice);
        try
        {
            Check(!WaitingRoomPractice.CanEnter,"Offline entry must be rejected");
            var legacyPlayers=Assets.Scripts.Exert.Match.PoolPlayer.players;
            var configure=typeof(PracticeSceneFlow).GetMethod("ConfigureWaitingPractice",BindingFlags.NonPublic|BindingFlags.Static);
            configure.Invoke(null,new object[]{3});
            Check(PracticeSceneFlow.Configuration.BallCount==3 && PracticeSceneFlow.SelectedMode==PracticeSceneFlow.Mode.ThreeBall,"Three-ball arena mapping");
            Check(PracticeSceneFlow.Configuration.Execution==MatchExecutionMode.LocalPractice && PracticeSceneFlow.OnlineSession==null,"Practice must remain local");
            configure.Invoke(null,new object[]{4});
            Check(PracticeSceneFlow.Configuration.BallCount==4 && PracticeSceneFlow.SelectedMode==PracticeSceneFlow.Mode.FourBall,"Four-ball arena mapping");
            Check(ReferenceEquals(legacyPlayers,Assets.Scripts.Exert.Match.PoolPlayer.players),"Practice configuration changed match players");
            type.GetField("lobbyScene",Hidden).SetValue(flow,scene);
            type.GetField("originalTimeScale",Hidden).SetValue(flow,Time.timeScale);
            type.GetField("current",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,flow);
            Check(WaitingRoomPractice.StartBlocked,"Match must wait while practice is active");
            type.GetMethod("HideLobby",Hidden).Invoke(flow,null);
            Check(!camera.GetComponent<Camera>().enabled&&!canvas.GetComponent<Canvas>().enabled&&!events.GetComponent<EventSystem>().enabled,"Lobby rendering/input not suspended");
            Check(flow.enabled&&host.activeInHierarchy,"Lobby session logic was disabled");
            PracticeSceneFlow.ReturnToMenu();
            Check((bool)type.GetField("returnRequested",Hidden).GetValue(flow),"Practice exit did not return to existing lobby");
            type.GetMethod("Restore",Hidden).Invoke(flow,null);
            Check(camera.GetComponent<Camera>().enabled&&canvas.GetComponent<Canvas>().enabled&&events.GetComponent<EventSystem>().enabled,"Lobby was not restored");
            Check(!disabled.GetComponent<Camera>().enabled,"Previously disabled component was enabled");
            Check(!WaitingRoomPractice.Active&&!PracticeSceneFlow.ReturnToPracticeMenu&&PracticeSceneFlow.OnlineSession==null,"Waiting session not cleaned up");
            Check(ReferenceEquals(legacyPlayers,Assets.Scripts.Exert.Match.PoolPlayer.players),"Return modified multiplayer players");
            File.WriteAllText("Logs/waiting-practice-result.txt","PASS "+checks+" checks");
        }
        catch(Exception error){File.WriteAllText("Logs/waiting-practice-result.txt",error.ToString());Debug.LogException(error);}
        finally{foreach(var root in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(root);}
    }
}
