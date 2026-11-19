using Assets.Scripts.Exert.Match;
using Assets.Scripts.Prack.BackSys.GameData;
using Photon.Pun;
using UnityEngine;

namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        int pendingQuickSerial, pendingQuickElapsed = -1, pendingQuickTurn;
        int quickShotElapsedMs = -1, quickShotTurnMs, quickMotionSerial = -1;
        bool quickWindowOpen;
        public bool HasQuickShotBonus => HasScoreCoins && !IsFriendly && !IsGuildWar &&
            historyOffer.match.Rules.Version >= 4;
        int QuickShotElapsedForAward => HasQuickShotBonus &&
            ScoreCoinLedger.QualifiesQuickShot(quickShotElapsedMs, quickShotTurnMs) ? quickShotElapsedMs : -1;
        int QuickShotTurnForAward => QuickShotElapsedForAward >= 0 ? quickShotTurnMs : 0;

        void ResetQuickShot()
        {
            pendingQuickSerial = pendingQuickTurn = quickShotTurnMs = 0;
            pendingQuickElapsed = quickShotElapsedMs = quickMotionSerial = -1;
            quickWindowOpen = false;
        }
        public void BeginQuickShotWindow()
        {
            pendingQuickSerial = pendingQuickTurn = 0; pendingQuickElapsed = -1;
            quickShotElapsedMs = -1; quickShotTurnMs = 0;
            quickWindowOpen = HasQuickShotBonus && !historyEnded;
        }
        // Called at accepted pointer release, before the cue's wind-up/impact animation.
        public void CaptureQuickShotRelease()
        {
            var coach = PoolCoach.Instance;
            if (!quickWindowOpen || !HasQuickShotBonus || historyEnded || !PoolLogic.controlInNetwork ||
                !coach.isMatchTimePlay || !coach.calculateTime) return;
            int turn = Mathf.RoundToInt(coach.maxPlayTime * 1000);
            int elapsed = Mathf.RoundToInt(Mathf.Clamp01(coach.playTime) * turn);
            ReceiveQuickShotTiming(legacyShotSerial + 1, elapsed, turn);
            quickWindowOpen = false;
            coach.StopTimerAtShotRelease();
            view.RPC(nameof(QuickShotReleaseRPC), RpcTarget.Others, LegacyMatchId, legacyShotSerial + 1, elapsed, turn);
        }
        void LatchQuickShotTiming(int serial)
        {
            quickShotElapsedMs = pendingQuickSerial == serial ? pendingQuickElapsed : -1;
            quickShotTurnMs = pendingQuickSerial == serial ? pendingQuickTurn : 0;
            quickWindowOpen = false;
        }
        // Photon authenticates the shooting peer. Terminal contact replay carries the same facts
        // so scoring cannot race a late presentation RPC. Settlement replays the exact integer rule.
        bool ReceiveQuickShotTiming(int serial, int elapsed, int turn)
        {
            if (!HasQuickShotBonus || historyEnded || serial < legacyShotSerial || serial > legacyShotSerial + 1 ||
                elapsed < 0 || elapsed > 300000 || turn <= 0 || turn > 300000) return false;
            if (pendingQuickSerial == serial && (pendingQuickElapsed != elapsed || pendingQuickTurn != turn)) return false;
            pendingQuickSerial = serial; pendingQuickElapsed = elapsed; pendingQuickTurn = turn;
            if (serial == legacyShotSerial) { quickShotElapsedMs = elapsed; quickShotTurnMs = turn; }
            return true;
        }
        [PunRPC] void QuickShotReleaseRPC(string id, int serial, int elapsed, int turn, PhotonMessageInfo info)
        {
            if (!HasQuickShotBonus || historyEnded || id != LegacyMatchId || !PoolLogic.controlFromNetwork ||
                info.Sender.ActorNumber != (historyPlayer.SeatNo == 0 ? historyOffer.actor1 : historyOffer.actor0)) return;
            if (ReceiveQuickShotTiming(serial, elapsed, turn)) PoolCoach.Instance.StopTimerAtShotRelease();
        }
        void PresentQuickShotBonus(int playerId, long bonus)
        {
            if (!HasQuickShotBonus || bonus <= 0 || quickMotionSerial == legacyShotSerial) return;
            quickMotionSerial = legacyShotSerial;
            var panel = Assets.Scripts.Sight.Surface.Pavilion.PnlMatch.Instance;
            if (panel && panel.isActiveAndEnabled) panel.ShowQuickShotBonus(playerId, bonus);
        }
    }
}
