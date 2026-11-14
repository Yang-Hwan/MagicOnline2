#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Come;
using Assets.Scripts.Sight.Surface.Consist;
using Assets.Scripts.Sight.Surface.Pavilion;
using Assets.Scripts.Sight.Vital.Pavilion;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

// Opt-in real two-player Pavilion test. No production route, credentials in reports, or fabricated scores.
public sealed class EquipmentMatchE2E : MonoBehaviour
{
    [Serializable] public sealed class ShotEvidence
    {
        public int Seat;
        public Vector3 Impulse, Point;
        public float Follow, ExpectedFollow, Magnitude, ExpectedMagnitude, MaxDisplacement;
        public bool Settled, ReactionSeen;
        public string Emotion;
    }
    [Serializable] public sealed class Report
    {
        public string Role, Stage, UserId, MatchId, Error, Settlement;
        public int Seat;
        public bool Ok, LoginForm, NeutralImages, LocalMessage, RemoteMessage, LocalEmotion, RemoteEmotion;
        public float TurnSeconds, ExpectedTurnSeconds, TipLimit, ExpectedTipLimit;
        public MatchEquipmentSnapshot Snapshot;
        public List<ShotEvidence> Shots = new List<ShotEvidence>();
        public List<string> Messages = new List<string>();
    }
    Report report;
    string output, peerFile;
    PunNetwork network;
    PhysicsMng physics;
    ShotCtrl shot;
    PnlMatch panel;
    Ball movingBall;
    ShotEvidence activeShot;
    Vector3 shotStart;
    bool sentShot;
    static string Arg(string key)
    { var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length?args[i+1]:null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!Debug.isDebugBuild || !Environment.GetCommandLineArgs().Contains("-equipment-match-e2e")) return;
        var go=new GameObject(nameof(EquipmentMatchE2E)); DontDestroyOnLoad(go); go.AddComponent<EquipmentMatchE2E>();
    }
    void Save() { if (report != null) File.WriteAllText(output,JsonUtility.ToJson(report,true)); }
    void Stage(string stage) { report.Stage=stage; Save(); Debug.Log("[EquipmentE2E] "+report.Role+" "+stage); }
    Report Peer()
    { try { return File.Exists(peerFile)?JsonUtility.FromJson<Report>(File.ReadAllText(peerFile)):null; } catch { return null; } }
    async Task Until(Func<bool> predicate,int seconds,string context)
    {
        var deadline=DateTime.UtcNow.AddSeconds(seconds);
        while (!predicate())
        {
            var peer=Peer(); if (!string.IsNullOrEmpty(peer?.Error)) throw new Exception("Peer failed: "+peer.Error);
            if (DateTime.UtcNow>deadline) throw new TimeoutException(context);
            await Task.Delay(100);
        }
    }
    Image Portrait(int seat) => panel.transform.Find(seat==report.Seat?"PlayerBoxs/PlayerSelf/Avatar":"PlayerBoxs/PlayerOther/Avatar").GetComponent<Image>();
    string Asset(int seat,string emotion) => report.Snapshot.Players[seat].Emotions.First(e=>e.Code==emotion).Asset;
    bool PortraitIs(int seat,string emotion) => Portrait(seat).overrideSprite==Resources.Load<Sprite>(Asset(seat,emotion));
    void ReceivedMessage(int seat,string text)
    {
        var expected=report.Snapshot.MessageAt(seat,0);
        if (text!=expected) { report.Error="Unexpected selected message"; Save(); return; }
        var path=seat==report.Seat?"SelfChat/Text":"OtherChat/Text";
        bool shown=panel.transform.Find(path).GetComponent<TMP_Text>().text==text && panel.transform.Find(path).gameObject.activeInHierarchy;
        if (seat==report.Seat) report.LocalMessage=shown; else report.RemoteMessage=shown;
        report.Messages.Add(seat+":"+text); Save();
    }
    void OnShot(Ball ball,Impulse impulse)
    {
        int seat=PoolPlayer.turnId==PoolPlayer.mainPlayer.playerId?report.Seat:1-report.Seat;
        var profile=report.Snapshot.Players[seat]; float follow=profile.EffectiveFollow(.5f);
        float blend=follow<=ShotCtrl.DefaultFollowThrough?follow/ShotCtrl.DefaultFollowThrough:(follow-ShotCtrl.DefaultFollowThrough)/(1-ShotCtrl.DefaultFollowThrough);
        float speed=follow<=ShotCtrl.DefaultFollowThrough?Mathf.Lerp(1.04f,1,blend):Mathf.Lerp(1,.92f,blend);
        activeShot=new ShotEvidence { Seat=seat,Impulse=impulse.impulse,Point=impulse.point,Follow=impulse.followThrough,ExpectedFollow=follow,
            Magnitude=impulse.impulse.magnitude,ExpectedMagnitude=.25f*6*ball.body.mass*profile.PowerScale*speed };
        report.Shots.Add(activeShot); movingBall=ball; shotStart=ball.body.position; Save();
    }
    void ShotEnded(float seconds) { if (activeShot!=null) { activeShot.Settled=true; Save(); } }
    void Update()
    {
        if (activeShot==null || !movingBall || !panel) return;
        activeShot.MaxDisplacement=Mathf.Max(activeShot.MaxDisplacement,Vector3.Distance(shotStart,movingBall.body.position));
        int seat=activeShot.Seat;
        string emotion=Portrait(seat).GetComponent<EquipmentPortrait>().CurrentEmotion;
        if (!activeShot.ReactionSeen && !string.IsNullOrEmpty(emotion) && emotion!="기본" && PortraitIs(seat,emotion))
        {
            activeShot.ReactionSeen=true;
            activeShot.Emotion=emotion;
            if (seat==report.Seat) report.LocalEmotion=true; else report.RemoteEmotion=true;
            Save();
        }
    }
    async Task Screenshot(string suffix)
    {
        string file=Path.Combine(Path.GetDirectoryName(output),report.Role+"-"+suffix+".png");
        ScreenCapture.CaptureScreenshot(file); await Until(()=>File.Exists(file),10,"screenshot "+suffix);
    }
    void Click(string name) => panel.transform.Find(name).GetComponent<Button>().onClick.Invoke();
    async void Start()
    {
        output=Arg("-equipment-output"); if (string.IsNullOrEmpty(output)) return;
        report=new Report { Role=Arg("-equipment-role"),Stage="boot" }; peerFile=Path.Combine(Path.GetDirectoryName(output),report.Role=="A"?"B.json":"A.json");
        try
        {
            if (!new Uri(GameApiClient.BaseUrl).IsLoopback || !PracticeMatchTestRoute.LocalPublicIntegrationTest || PracticeMatchTestRoute.Enabled)
                throw new Exception("Isolated public development route and local API required");
            Application.runInBackground=true; Application.targetFrameRate=40; Stage("login");
            await Until(()=>FindAnyObjectByType<PnlLogin>()!=null,60,"login screen");
            var login=FindAnyObjectByType<PnlLogin>();
            login.transform.Find("InpId").GetComponent<TMP_InputField>().text=Environment.GetEnvironmentVariable("MAGIC_EQUIPMENT_LOGIN");
            login.transform.Find("InpPw").GetComponent<TMP_InputField>().text=Environment.GetEnvironmentVariable("MAGIC_EQUIPMENT_PASSWORD");
            login.transform.Find("BtnLogin").GetComponent<Button>().onClick.Invoke();
            await Until(()=>PhotonNetwork.InLobby && SceneManager.GetActiveScene().name=="Consist",90,"login and Photon lobby");
            report.LoginForm=true; report.UserId=GameApiClient.UserId; Stage("joining");
            FindAnyObjectByType<RoomSwipe>(FindObjectsInactive.Include).GotoWaitRoom(int.Parse(Arg("-equipment-hall")));
            await Until(()=>PhotonNetwork.InRoom,60,"isolated room"); Stage("waiting");
            await Until(()=>SceneManager.GetActiveScene().name=="Pavilion" && PnlMatch.Instance!=null,120,"Pavilion game");
            network=(PunNetwork)NetworkManager.network; panel=PnlMatch.Instance; physics=FindAnyObjectByType<PhysicsMng>(); shot=FindAnyObjectByType<ShotCtrl>();
            await Until(()=>network.HasLegacyMatch && network.MatchEquipment!=null && PoolCoach.Instance.isMatchTimePlay && !physics.inMove && shot.cueBall!=null,70,"opening scatter and equipment");
            report.MatchId=network.LegacyMatchId; report.Seat=network.LegacyLocalSeat; report.Snapshot=network.MatchEquipment;
            network.EquippedMessageReceived+=ReceivedMessage;
            physics.OnMatchReplayShot+=OnShot; physics.OnEndShot+=ShotEnded;
            await Until(()=>PortraitIs(0,"기본") && PortraitIs(1,"기본"),10,"both equipped neutral portraits"); report.NeutralImages=true; Stage("ready");
            await Until(()=>Peer()?.MatchId==report.MatchId && Peer()?.NeutralImages==true,40,"both clients ready");
            await Screenshot("neutral");
            Click("PlayerBoxs/PlayerSelf/Avatar"); await Task.Delay(100); Click("EquippedChatMenu/Pack0/Rows/Content/Entry0");
            await Until(()=>report.LocalMessage && report.RemoteMessage,20,"selected messages on both screens"); await Screenshot("messages");
            Stage("shots");
            var deadline=DateTime.UtcNow.AddSeconds(130);
            while (!(report.Shots.Any(s=>s.Seat==0 && s.Settled) && report.Shots.Any(s=>s.Seat==1 && s.Settled)))
            {
                if (DateTime.UtcNow>deadline) throw new TimeoutException("both real player shots");
                if (!sentShot && PoolLogic.controlInNetwork && PoolPlayer.mainPlayer.myTurn && !physics.inMove && !physics.IsResolvingShot && !shot.IsShotAnimating)
                {
                    var local=report.Snapshot.Players[report.Seat]; report.TurnSeconds=PoolCoach.Instance.maxPlayTime; report.ExpectedTurnSeconds=local.TurnSeconds(30);
                    if (Mathf.Abs(report.TurnSeconds-report.ExpectedTurnSeconds)>.01f) throw new Exception("equipped turn clock mismatch");
                    var point=FindObjectsByType<BallPointBig>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
                    var frame=point.transform.Find("BallFrm").GetComponent<RectTransform>(); var marker=frame.Find("Point").GetComponent<RectTransform>();
                    report.TipLimit=Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital.CuePointGuide.Limit(frame,marker);
                    report.ExpectedTipLimit=Mathf.Max(0,Mathf.Min(frame.rect.width,frame.rect.height)*.5f*.8f*local.TipScale-Mathf.Max(marker.rect.width,marker.rect.height)*.5f);
                    if (Mathf.Abs(report.TipLimit-report.ExpectedTipLimit)>.01f) throw new Exception("equipped tip UI mismatch");
                    shot.SetCueTargetingPosition(Vector3.zero,Vector2.zero); shot.SetFollowThrough(.5f);
                    typeof(ShotCtrl).GetProperty("force").SetValue(shot,.25f); sentShot=true; Save();
                    shot.WaitAndStartShot("").Forget();
                }
                await Task.Delay(100);
            }
            await Task.Delay(2000); Save();
            foreach(var evidence in report.Shots)
                if (Mathf.Abs(evidence.Magnitude-evidence.ExpectedMagnitude)>.003f || Mathf.Abs(evidence.Follow-evidence.ExpectedFollow)>.001f || evidence.MaxDisplacement<.01f)
                    throw new Exception("real shot equipment/movement mismatch seat="+evidence.Seat);
            await Screenshot("shots"); Stage("shots-complete");
            await Until(()=>Peer()?.Stage=="shots-complete" || Peer()?.Ok==true,30,"peer shot evidence");
            if (report.Role=="A") panel.btnMatchEnd_Click();
            await Until(()=>BackendMatchHistory.Instance.IsSettled(report.MatchId),65,"real match surrender settlement");
            var state=await BackendMatchHistory.Instance.GetState(report.MatchId); report.Settlement=state.Receipt.Reason;
            report.Ok=true; Stage("complete");
        }
        catch(Exception e) { report.Error=e.ToString(); Save(); Debug.LogError("[EquipmentE2E] "+e); }
    }
}
#endif
