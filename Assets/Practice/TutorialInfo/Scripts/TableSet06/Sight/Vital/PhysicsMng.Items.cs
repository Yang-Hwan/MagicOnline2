using System;
using System.Collections.Generic;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Often;
using UnityEngine;
using DG.Tweening;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public ItemMatchState Items { get; private set; }
        public bool HasItemAuthority => Items != null && Items.IsAuthority;
        readonly Dictionary<long, StuffBase> itemViews = new Dictionary<long, StuffBase>();
        readonly Dictionary<int, long> appliedBonuses = new Dictionary<int, long>();
        public IEnumerable<StuffBase> ActiveItems => itemViews.Values;
        int itemSessionVersion;
        readonly long[] pendingCoinDisplay = new long[2];
        public sealed class CoinDisplayBatch
        {
            internal int session, player;
            internal long remaining;
        }
        public CoinDisplayBatch BeginCoinDisplay(int playerId, long amount)
        {
            if (!IsLocalPractice || playerId < 0 || playerId >= pendingCoinDisplay.Length) return null;
            var batch = new CoinDisplayBatch { session = itemSessionVersion, player = playerId, remaining = amount };
            pendingCoinDisplay[playerId] += amount;
            return batch;
        }
        public void AdvanceCoinDisplay(CoinDisplayBatch batch, long amount = long.MaxValue)
        {
            if (batch == null || batch.session != itemSessionVersion) return;
            long delta = Math.Min(batch.remaining, amount);
            batch.remaining -= delta;
            pendingCoinDisplay[batch.player] -= delta;
            if (batch.player < coinTxts.Length && coinTxts[batch.player])
                coinTxts[batch.player].text = Utility.CoinNumToStr(DisplayedMatchCoin(batch.player));
        }
        public long DisplayedMatchCoin(int playerId)
        {
            return PoolPlayer.players[playerId].matchCoin -
                (IsLocalPractice && playerId >= 0 && playerId < pendingCoinDisplay.Length ? pendingCoinDisplay[playerId] : 0);
        }
        bool externalItemSession;
        void BeginLocalItemSession()
        {
            ConfigureItemSession(Guid.NewGuid().ToString("N"), true);
            externalItemSession = false;
        }

        // Call after resetting players at a match boundary. The transport must use
        // a server-issued match id and deliver only authenticated authority events.
        public void ConfigureItemSession(string matchId, bool authority)
        {
            // Local shot/reset sessions preserve the player's item toggle choice.
            if (!IsLocalPractice) ItemsEnabled = true;
            EndBallPlacement();
            ClearShotReplay();
            undoSnapshot = null; simulationVersion++;
            externalItemSession = true;
            itemSessionVersion++;
            Array.Clear(pendingCoinDisplay, 0, pendingCoinDisplay.Length);
            if (ObjectPooler.instance)
                foreach (var effect in ObjectPooler.instance.GetActivePools<EffCtrl>("Effect")) effect.DeactiveDelay();
            coinsQueue.Clear();
            foreach (var coin in animatedCoins) if (coin) { coin.transform.DOKill(); coin.SetActive(false); coinsQueue.Enqueue(coin); }
            if (Items != null) { Items.Applied -= ApplyItemChange; Items.SnapshotApplied -= RestoreItemViews; }
            foreach (var view in itemViews.Values) if (view) view.DeactiveDelay();
            itemViews.Clear(); appliedBonuses.Clear();
            Items = new ItemMatchState(matchId, authority);
            Items.Applied += ApplyItemChange;
            Items.SnapshotApplied += RestoreItemViews;
        }
        public bool TryCollectItem(StuffBase item, int playerId = -1)
        {
            if (!ItemsEnabled || !HasItemAuthority || !item || !item.gameObject.activeInHierarchy) return false;
            if (playerId < 0) playerId = PoolPlayer.currentPlayer?.playerId ?? -1;
            if (PoolPlayer.players == null || Array.Find(PoolPlayer.players, p => p != null && p.playerId == playerId) == null) return false;
            return Items.Collect(item.SpawnId, playerId);
        }
        void SpawnItemView(ItemMatchState.Item item)
        {
            if(!ItemsEnabled) return;
            if (item.positionId >= drawStuffPos.stuffListPos.Count)
                throw new InvalidOperationException("Item board configuration differs from authority.");
            var view = ObjectPooler.instance.SpawnFromPool<StuffBase>(item.type.ToString(), drawStuffPos.stuffListPos[item.positionId]);
            view.Bind(this, item);
            itemViews.Add(item.id, view);
        }
        void ApplyItemChange(ItemMatchState.Change change)
        {
            if (change.kind == ItemMatchState.ChangeKind.Cushion)
            {
                if(!ItemsEnabled) return;
                cushionHit.HitDir((CushionDir)change.cushionDirection);
                ObjectPooler.instance.TrySpawnVisual("Cosmos", Vector3.zero, 8);
                return;
            }
            if (change.kind == ItemMatchState.ChangeKind.Spawn) { SpawnItemView(change.item); return; }
            ApplyBonusTotals();
            if (change.kind == ItemMatchState.ChangeKind.Bonus) return;
            if (!itemViews.TryGetValue(change.item.id, out var view)) return;
            Vector3 position = view.transform.position;
            itemViews.Remove(change.item.id);
            view.DeactiveDelay();
            if (change.kind != ItemMatchState.ChangeKind.Collect) return;
            if (change.reward > 0)
            {
                CoinPickUpAnimate(position + Vector3.up * .1f, 2, change.playerId);
                ObjectPooler.instance.TrySpawnVisual("CoinLightUp", position);
            }
            else CreateEff(change.item.type, position, change.playerId);
        }
        void ApplyBonusTotals()
        {
            if (PoolPlayer.players == null) return;
            foreach (var player in PoolPlayer.players)
            {
                if (player == null) continue;
                long total = Items.BonusFor(player.playerId);
                appliedBonuses.TryGetValue(player.playerId, out long previous);
                long difference = total - previous;
                while (difference != 0)
                {
                    int delta = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, difference));
                    PoolPlayer.SetHitAdd(player.playerId, delta, false);
                    difference -= delta;
                }
                appliedBonuses[player.playerId] = total;
                if (player.playerId < coinTxts.Length && coinTxts[player.playerId])
                    coinTxts[player.playerId].text = Utility.CoinNumToStr(DisplayedMatchCoin(player.playerId));
            }
        }
        void RestoreItemViews()
        {
            itemSessionVersion++;
            foreach (var view in itemViews.Values) if (view) view.DeactiveDelay();
            itemViews.Clear();
            foreach (var item in Items.Capture().items) SpawnItemView(item);
            ApplyBonusTotals();
        }
    }
}
