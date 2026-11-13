using System;
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
        public static MatchEquipmentProfile ForSeat(int seat)
        {
            if (NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch && pun.MatchEquipment?.MatchId == pun.LegacyMatchId && seat >= 0 && seat < 2)
                return pun.MatchEquipment.Players[seat];
            return basic;
        }
        public static MatchEquipmentProfile Local => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch ? ForSeat(pun.LegacyLocalSeat) : basic;
        public static MatchEquipmentProfile Other => NetworkManager.TryGetExistingNetwork(out var net) && net is PunNetwork pun && pun.HasLegacyMatch ? ForSeat(1 - pun.LegacyLocalSeat) : basic;
        public static MatchEquipmentProfile Shooter => PoolPlayer.mainPlayer != null && PoolPlayer.turnId != PoolPlayer.mainPlayer.playerId ? Other : Local;
    }
}
