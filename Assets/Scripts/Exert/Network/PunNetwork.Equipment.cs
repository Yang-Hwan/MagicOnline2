using System;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Match;
using Assets.Scripts.Prack.BackSys.GameData;
using Photon.Pun;
using UnityEngine;

namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        [Serializable] sealed class EquipmentOpponent { public string OpponentUserId; }
        public MatchEquipmentSnapshot MatchEquipment { get; private set; }
        public event Action<int, string> EquippedMessageReceived;
        float nextEquipmentSend;
        readonly float[] nextEquipmentReceive = new float[2];

        // Cosmetic waiting-room preview only; gameplay still uses the server match snapshot.
        const string LoungePortraitKey = "equippedNeutralPortrait";
        public void SetLoungePortrait(string asset)
        {
            var player = PhotonNetwork.LocalPlayer;
            if (player == null || player.CustomProperties[LoungePortraitKey] as string == asset) return;
            player.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { [LoungePortraitKey] = asset });
        }
        public string LoungeOpponentPortrait
        {
            get
            {
                if (!PhotonNetwork.InRoom) return null;
                var opponent = PhotonNetwork.PlayerList.FirstOrDefault(p => !p.IsLocal && !p.IsInactive);
                var asset = opponent?.CustomProperties[LoungePortraitKey] as string;
                return asset != null && asset.Length <= 2048 ? asset : null;
            }
        }

        async Task LoadMatchEquipment(string id, bool create = false)
        {
            var offer = historyOffer;
            var snapshot = await GameApiClient.Request<MatchEquipmentSnapshot>("/api/matches/" + id + "/equipment", create ? "POST" : "GET",
                create ? new EquipmentOpponent { OpponentUserId = offer.match.PlayerBUserId } : null);
            if (historyOffer != offer || offer.match.MatchId != id || historyEnded) throw new InvalidOperationException("EquipmentMatchChanged");
            if (snapshot?.Version != 1 || snapshot.MatchId != id || snapshot.Players == null || snapshot.Players.Length != 2 ||
                snapshot.Players[0].UserId != offer.match.PlayerAUserId || snapshot.Players[1].UserId != offer.match.PlayerBUserId)
                throw new InvalidOperationException("경기 장비 정보를 확인할 수 없습니다.");
            MatchEquipment = snapshot;
        }

        public void RecordCasualCuePoint(int playerId,int points=1)
        {
            if(!HasLegacyMatch || HasScoreCoins || historyEnded || (!IsFriendly && !IsGuildWar) || playerId!=PoolPlayer.mainPlayer.playerId || MatchEquipment==null)return;
            MatchEquipmentRuntime.RecordCasualPoint(LegacyMatchId,legacyShotSerial,MatchEquipment.Players[LegacyLocalSeat].CueId,points);
        }
        public bool SendEquippedMessage(int index)
        {
            if (!HasLegacyMatch || historyEnded || MatchEquipment?.MatchId != LegacyMatchId || Time.realtimeSinceStartup < nextEquipmentSend) return false;
            var profile = MatchEquipment.Players[LegacyLocalSeat];
            if (index < 0 || index >= profile.Messages.Length) return false;
            nextEquipmentSend = Time.realtimeSinceStartup + 2;
            view.RPC(nameof(EquippedChatRPC), RpcTarget.All, LegacyMatchId, index, false);
            return true;
        }
        [PunRPC] void EquippedChatRPC(string id, int index, bool emotion, PhotonMessageInfo info)
        {
            if (emotion || !HasLegacyMatch || historyEnded || MatchEquipment?.MatchId != id || id != LegacyMatchId || historyOffer == null) return;
            int seat = info.Sender.ActorNumber == historyOffer.actor0 ? 0 : info.Sender.ActorNumber == historyOffer.actor1 ? 1 : -1;
            if (seat < 0 || index < 0 || Time.realtimeSinceStartup < nextEquipmentReceive[seat]) return;
            string message = MatchEquipment.MessageAt(seat, index);
            if (message == null) return;
            nextEquipmentReceive[seat] = Time.realtimeSinceStartup + 1.9f;
            EquippedMessageReceived?.Invoke(seat, message);
        }
    }

    public static class MatchEquipmentRuntime
    {
        static readonly MatchEquipmentProfile basic = new MatchEquipmentProfile();
        static MatchEquipmentProfile localProfile,practiceProfile;
        static string profileOwner,practiceRun;
        static int practiceShot;
        public static void ClearLocal(){localProfile=practiceProfile=null;profileOwner=practiceRun=null;practiceShot=0;}
        public static async Task RefreshLocalAsync()
        {
            var owner=GameApiClient.UserId;if(string.IsNullOrEmpty(owner))return;
            await FlushProgressAsync();
            var p=await GameApiClient.Request<MatchEquipmentProfile>("/api/me/cue");
            if(GameApiClient.UserId==owner){localProfile=p;profileOwner=owner;}
        }
        public static void BeginPracticeRun()
        {
            practiceProfile=CurrentLocal;practiceRun=Guid.NewGuid().ToString("D");practiceShot=0;
        }
        static MatchEquipmentProfile CurrentLocal=>profileOwner==GameApiClient.UserId && localProfile!=null?localProfile:basic;
        static bool InPractice=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TableSet07" &&
            Assets.Scripts.Often.PracticeSceneFlow.Configuration.Execution==Assets.Scripts.Often.MatchExecutionMode.LocalPractice &&
            Assets.Scripts.Often.PracticeSceneFlow.SelectedMode!=Assets.Scripts.Often.PracticeSceneFlow.Mode.Stage;
        [Serializable] sealed class ProgressPoint {public string RunId;public int Shot;public long CueId;public int Points=1;}
        [Serializable] sealed class ProgressQueue {public System.Collections.Generic.List<ProgressPoint> Items=new System.Collections.Generic.List<ProgressPoint>();}
        static readonly System.Threading.SemaphoreSlim progressGate=new System.Threading.SemaphoreSlim(1,1);
        static ProgressQueue ReadQueue(string key)
        {
            try{var q=JsonUtility.FromJson<ProgressQueue>(PlayerPrefs.GetString(key,""))??new ProgressQueue();if(q.Items==null)q.Items=new System.Collections.Generic.List<ProgressPoint>();return q;}
            catch{return new ProgressQueue();}
        }
        public static async void RecordPracticePoint(int points=1)
        {
            if(!InPractice || practiceProfile==null || practiceProfile.CueId<1 || string.IsNullOrEmpty(GameApiClient.UserId))return;
            EnqueuePoint(new ProgressPoint{RunId=practiceRun,Shot=++practiceShot,CueId=practiceProfile.CueId,Points=points});
            await FlushProgressAsync();
        }
        public static async void RecordCasualPoint(string match,int shot,long cue,int points=1)
        {
            if(cue<1 || string.IsNullOrEmpty(GameApiClient.UserId) || !Guid.TryParse(match,out var run))return;
            EnqueuePoint(new ProgressPoint{RunId=run.ToString("D"),Shot=shot,CueId=cue,Points=points});await FlushProgressAsync();
        }
        static void EnqueuePoint(ProgressPoint point)
        {
            string key="CueProgress:"+GameApiClient.BaseUrl+":"+GameApiClient.UserId;var queue=ReadQueue(key);
            if(queue.Items.Exists(p=>p.RunId==point.RunId && p.Shot==point.Shot))return;
            queue.Items.Add(point);
            PlayerPrefs.SetString(key,JsonUtility.ToJson(queue));PlayerPrefs.Save();
        }
        static async Task FlushProgressAsync()
        {
            string owner=GameApiClient.UserId;if(string.IsNullOrEmpty(owner))return;
            string key="CueProgress:"+GameApiClient.BaseUrl+":"+owner;await progressGate.WaitAsync();
            try {
                while(owner==GameApiClient.UserId)
                {
                    var queue=ReadQueue(key);if(queue.Items.Count==0)return;
                    try {
                        var input=queue.Items[0];if(input.Points<1)input.Points=1;
                        if(Guid.TryParse(input.RunId,out var run))input.RunId=run.ToString("D");
                        var next=await GameApiClient.Request<MatchEquipmentProfile>("/api/me/cue-progress/practice","POST",input);
                        if(owner!=GameApiClient.UserId)return;
                        localProfile=next;profileOwner=owner;
                        queue=ReadQueue(key);queue.Items.RemoveAt(0);PlayerPrefs.SetString(key,JsonUtility.ToJson(queue));PlayerPrefs.Save();
                    }catch(Exception e){Debug.LogWarning("큐대 성장 진도는 연결 복구 후 다시 동기화합니다: "+e.Message);return;}
                }
            }finally{progressGate.Release();}
        }
        public static MatchEquipmentProfile ForSeat(int seat)
        {
            if (NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch && pun.MatchEquipment?.MatchId == pun.LegacyMatchId && seat >= 0 && seat < 2)
                return pun.MatchEquipment.Players[seat];
            return basic;
        }
        public static MatchEquipmentProfile Local => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch ? ForSeat(pun.LegacyLocalSeat) : CurrentLocal;
        public static MatchEquipmentProfile Other => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch ? ForSeat(1 - pun.LegacyLocalSeat) : basic;
        public static MatchEquipmentProfile Shooter => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TableSet07" &&
            (Assets.Scripts.Often.PracticeSceneFlow.SelectedMode==Assets.Scripts.Often.PracticeSceneFlow.Mode.Stage || Assets.Scripts.Often.PracticeSceneFlow.Configuration.Execution==Assets.Scripts.Often.MatchExecutionMode.Replay) ? basic : InPractice ? (practiceProfile??CurrentLocal) : PoolPlayer.mainPlayer != null && PoolPlayer.turnId != PoolPlayer.mainPlayer.playerId ? Other : Local;
    }
}
