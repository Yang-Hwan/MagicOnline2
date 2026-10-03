using System;
using System.Collections.Generic;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    // Transport-independent authoritative item state. Receive events only from the
    // authenticated authority on a reliable ordered channel, on Unity's main thread.
    public sealed class ItemMatchState
    {
        [Serializable] public struct Item
        {
            public long id;
            public StuffType type;
            public int positionId, lifeSeconds;
        }
        public enum ChangeKind { Spawn, Collect, Expire, Bonus, Cushion }
        [Serializable] public struct Change
        {
            public string matchId;
            public long sequence;
            public ChangeKind kind;
            public Item item;
            public int playerId, reward, cushionDirection;
        }
        [Serializable] public struct Score { public int playerId; public long bonus; }
        [Serializable] public sealed class Snapshot
        {
            public string matchId;
            public long sequence;
            public Item[] items;
            public Score[] scores;
        }

        readonly Dictionary<long, Item> items = new Dictionary<long, Item>();
        readonly HashSet<int> occupied = new HashSet<int>();
        readonly Dictionary<int, long> bonuses = new Dictionary<int, long>();
        long nextId;
        public string MatchId { get; }
        public bool IsAuthority { get; }
        public long Sequence { get; private set; }
        public int Count => items.Count;
        public event Action<Change> Applied;
        public event Action<Change> Published;
        public event Action SnapshotApplied;

        public ItemMatchState(string matchId, bool isAuthority)
        {
            if (string.IsNullOrEmpty(matchId)) throw new ArgumentException(nameof(matchId));
            MatchId = matchId;
            IsAuthority = isAuthority;
        }
        public bool Contains(long id) => items.ContainsKey(id);
        public long BonusFor(int playerId) => bonuses.TryGetValue(playerId, out var value) ? value : 0;
        public bool Spawn(StuffType type, int positionId, int lifeSeconds)
        {
            if (!IsAuthority || positionId < 0 || occupied.Contains(positionId) || lifeSeconds <= 0 ||
                !Enum.IsDefined(typeof(StuffType), type)) return false;
            return Commit(new Change { kind = ChangeKind.Spawn, item = new Item {
                id = ++nextId, type = type, positionId = positionId, lifeSeconds = lifeSeconds } });
        }
        public bool Collect(long id, int playerId)
        {
            if (!IsAuthority || playerId < 0 || !items.TryGetValue(id, out var item)) return false;
            return Commit(new Change { kind = ChangeKind.Collect, item = item, playerId = playerId,
                reward = item.type == StuffType.StuffCoin01 ? 20 : 0 });
        }
        public bool Expire(long id)
        {
            if (!IsAuthority || !items.TryGetValue(id, out var item)) return false;
            return Commit(new Change { kind = ChangeKind.Expire, item = item });
        }
        public bool AwardBonus(int playerId, int reward)
        {
            if (!IsAuthority || playerId < 0 || reward <= 0) return false;
            return Commit(new Change { kind = ChangeKind.Bonus, playerId = playerId, reward = reward });
        }
        public bool Cushion(int direction)
        {
            if (!IsAuthority) return false;
            return Commit(new Change { kind = ChangeKind.Cushion, cushionDirection = direction });
        }
        bool Commit(Change change)
        {
            change.matchId = MatchId;
            change.sequence = Sequence + 1;
            if (!Apply(change)) return false;
            Published?.Invoke(change);
            return true;
        }
        // False means duplicate, foreign match, gap or invalid event. For a gap,
        // request a snapshot instead of applying later events out of order.
        public bool Receive(Change change) => !IsAuthority && Apply(change);
        bool Apply(Change change)
        {
            if (change.matchId != MatchId || change.sequence != Sequence + 1) return false;
            if (change.kind == ChangeKind.Spawn)
            {
                if (change.item.id <= 0 || items.ContainsKey(change.item.id) ||
                    occupied.Contains(change.item.positionId) || change.item.positionId < 0 ||
                    change.item.lifeSeconds <= 0 || !Enum.IsDefined(typeof(StuffType), change.item.type)) return false;
                items.Add(change.item.id, change.item);
                occupied.Add(change.item.positionId);
            }
            else if (change.kind == ChangeKind.Collect || change.kind == ChangeKind.Expire)
            {
                if (!items.TryGetValue(change.item.id, out var item) || item.positionId != change.item.positionId ||
                    item.type != change.item.type) return false;
                if (change.kind == ChangeKind.Collect && (change.playerId < 0 ||
                    change.reward != (item.type == StuffType.StuffCoin01 ? 20 : 0))) return false;
                items.Remove(item.id);
                occupied.Remove(item.positionId);
            }
            else if (change.kind != ChangeKind.Cushion &&
                (change.kind != ChangeKind.Bonus || change.reward <= 0 || change.playerId < 0)) return false;
            if (change.kind == ChangeKind.Collect || change.kind == ChangeKind.Bonus)
                bonuses[change.playerId] = BonusFor(change.playerId) + change.reward;
            Sequence = change.sequence;
            Applied?.Invoke(change);
            return true;
        }
        public Snapshot Capture()
        {
            var snapshot = new Snapshot { matchId = MatchId, sequence = Sequence,
                items = new Item[items.Count], scores = new Score[bonuses.Count] };
            items.Values.CopyTo(snapshot.items, 0);
            int index = 0;
            foreach (var pair in bonuses) snapshot.scores[index++] = new Score { playerId = pair.Key, bonus = pair.Value };
            return snapshot;
        }
        // Local practice only: a new branch never reuses the old network sequence.
        public static ItemMatchState CreatePracticeBranch(Snapshot snapshot, string newMatchId)
        {
            var branch = new ItemMatchState(newMatchId, true);
            foreach (var item in snapshot.items)
            {
                branch.items.Add(item.id, item);
                branch.occupied.Add(item.positionId);
                branch.nextId = Math.Max(branch.nextId, item.id);
            }
            foreach (var score in snapshot.scores) branch.bonuses.Add(score.playerId, score.bonus);
            return branch;
        }
        public bool Restore(Snapshot snapshot)
        {
            if (IsAuthority || snapshot == null || snapshot.matchId != MatchId || snapshot.sequence < Sequence ||
                snapshot.items == null || snapshot.scores == null) return false;
            var ids = new HashSet<long>();
            var positions = new HashSet<int>();
            var players = new HashSet<int>();
            foreach (var item in snapshot.items)
                if (item.id <= 0 || item.positionId < 0 || item.lifeSeconds <= 0 || !ids.Add(item.id) ||
                    !positions.Add(item.positionId) || !Enum.IsDefined(typeof(StuffType), item.type)) return false;
            foreach (var score in snapshot.scores)
                if (score.playerId < 0 || score.bonus < 0 || !players.Add(score.playerId)) return false;
            items.Clear(); occupied.Clear(); bonuses.Clear();
            foreach (var item in snapshot.items) { items.Add(item.id, item); occupied.Add(item.positionId); }
            foreach (var score in snapshot.scores) bonuses.Add(score.playerId, score.bonus);
            Sequence = snapshot.sequence;
            SnapshotApplied?.Invoke();
            return true;
        }
    }
}
