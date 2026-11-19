using System;
using System.Globalization;

namespace Assets.Scripts.Prack.BackSys.GameData
{
    // Pure arithmetic shared by Unity and the settlement server. No wallet writes.
    public sealed class ScoreCoinLedger
    {
        public long BalanceA { get; private set; }
        public long BalanceB { get; private set; }
        public long NetA { get; private set; }
        public int BustWinner { get; private set; } = -1;
        public string Events { get; private set; } = "";
        public int LastShot { get; private set; }
        public long LastQuickBonus { get; private set; }
        public long LastAward { get; private set; }
        public static bool QualifiesQuickShot(int elapsedMs, int turnMs) =>
            turnMs > 0 && turnMs <= 300000 && elapsedMs >= 0 && (long)elapsedMs * 10 <= (long)turnMs * 3;
        public static long QuickBonus(long scoringCoins) => scoringCoins > 0 ? scoringCoins / 5 : 0;
        int lastSeat = -1, lastStreak;
        readonly long unit;
        public ScoreCoinLedger(long startA, long startB, long entry, int target)
        {
            if (entry < 0 || target < 1 || startA < entry || startB < entry)
                throw new ArgumentException("Invalid coin round balances/rules.");
            BalanceA = startA - entry; BalanceB = startB - entry; unit = entry / target;
        }
        public void Add(int shot, int seat, int firstStreak, int points, bool bank = false, int quickElapsedMs = -1, int quickTurnMs = 0)
        {
            bool quick = quickElapsedMs >= 0;
            if ((quick && !QualifiesQuickShot(quickElapsedMs, quickTurnMs)) || (!quick && quickTurnMs != 0))
                throw new ArgumentException("Invalid quick-shot timing.");
            if ((bank && points != 2) || BustWinner >= 0 || shot <= LastShot || shot > 1000000 || seat < 0 || seat > 1 || points < 1 || points > 2 ||
                firstStreak < 1 || firstStreak > 100000 || (firstStreak != 1 && (seat != lastSeat || firstStreak != lastStreak + 1)))
                throw new ArgumentException("Invalid scoring coin sequence.");
            int applied = 0; long basePaid = 0;
            LastQuickBonus = LastAward = 0;
            for (int i = 0; i < (bank ? 1 : points); i++)
            {
                long available = seat == 0 ? BalanceB : BalanceA;
                long amount = unit;
                // Saturate at the payer balance before doubling: no exponential overflow.
                for (int j = 1; j < firstStreak + i + (bank ? 1 : 0) && amount < available && amount > 0; j++)
                    amount = amount > available / 2 ? available : amount * 2;
                amount = Math.Min(amount, available);
                long delta = seat == 0 ? amount : -amount;
                BalanceA = checked(BalanceA + delta); BalanceB = checked(BalanceB - delta);
                NetA = checked(NetA + delta); basePaid = checked(basePaid + amount); applied++;
                if (unit > 0 && available == amount) { BustWinner = seat; break; }
            }
            if (quick && BustWinner < 0)
            {
                long available = seat == 0 ? BalanceB : BalanceA;
                LastQuickBonus = Math.Min(QuickBonus(basePaid), available);
                long extra = seat == 0 ? LastQuickBonus : -LastQuickBonus;
                BalanceA = checked(BalanceA + extra); BalanceB = checked(BalanceB - extra); NetA = checked(NetA + extra);
                if (LastQuickBonus > 0 && LastQuickBonus == available) BustWinner = seat;
            }
            LastAward = checked(basePaid + LastQuickBonus);
            LastShot = shot; lastSeat = seat; lastStreak = firstStreak + (bank ? points : applied) - 1;
            if (quick)
                Events += string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6};", shot, seat, firstStreak,
                    bank ? points : applied, bank ? 1 : 0, quickElapsedMs, quickTurnMs);
            else Events += string.Format(CultureInfo.InvariantCulture, bank ? "{0},{1},{2},{3},1;" : "{0},{1},{2},{3};", shot, seat, firstStreak, bank ? points : applied);
        }
        public static ScoreCoinLedger Replay(long a, long b, long entry, int target, string events, bool allowBank = true, bool allowQuick = true)
        {
            var ledger = new ScoreCoinLedger(a, b, entry, target);
            if (string.IsNullOrEmpty(events)) return ledger;
            if (events.Length > 262144 || !events.EndsWith(";", StringComparison.Ordinal)) throw new ArgumentException("Invalid coin log.");
            var rows = events.Split(';');
            if (rows.Length > 10001) throw new ArgumentException("Coin log too long.");
            for (int i = 0; i < rows.Length - 1; i++)
            {
                var fields = rows[i].Split(',');
                if ((fields.Length != 4 && fields.Length != 5 && fields.Length != 7) ||
                    (fields.Length == 5 && fields[4] != "1") || (fields.Length == 7 && fields[4] != "0" && fields[4] != "1") || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int shot) ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seat) ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int streak) ||
                    !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int points))
                    throw new ArgumentException("Invalid coin event.");
                bool bank = fields.Length >= 5 && fields[4] == "1";
                if (bank && !allowBank) throw new ArgumentException("Bank bonus is only valid in three-ball matches.");
                int elapsed = -1, turn = 0;
                if (fields.Length == 7 && (!allowQuick ||
                    !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out elapsed) ||
                    !int.TryParse(fields[6], NumberStyles.None, CultureInfo.InvariantCulture, out turn) ||
                    !QualifiesQuickShot(elapsed, turn))) throw new ArgumentException("Invalid quick-shot event.");
                ledger.Add(shot, seat, streak, points, bank, elapsed, turn);
            }
            if (ledger.Events != events) throw new ArgumentException("Noncanonical coin log.");
            return ledger;
        }
        public static long Commission(long profit) => profit > 0 ? profit / 20 : 0;
    }
}
