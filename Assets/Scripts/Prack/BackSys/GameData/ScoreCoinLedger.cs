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
        int lastSeat = -1, lastStreak;
        readonly long unit;
        public ScoreCoinLedger(long startA, long startB, long entry, int target)
        {
            if (entry < 0 || target < 1 || startA < entry || startB < entry)
                throw new ArgumentException("Invalid coin round balances/rules.");
            BalanceA = startA - entry; BalanceB = startB - entry; unit = entry / target;
        }
        public void Add(int shot, int seat, int firstStreak, int points)
        {
            if (BustWinner >= 0 || shot <= LastShot || shot > 1000000 || seat < 0 || seat > 1 || points < 1 || points > 2 ||
                firstStreak < 1 || firstStreak > 100000 || (firstStreak != 1 && (seat != lastSeat || firstStreak != lastStreak + 1)))
                throw new ArgumentException("Invalid scoring coin sequence.");
            int applied = 0;
            for (int i = 0; i < points; i++)
            {
                long available = seat == 0 ? BalanceB : BalanceA;
                long amount = unit;
                // Saturate at the payer balance before doubling: no exponential overflow.
                for (int j = 1; j < firstStreak + i && amount < available && amount > 0; j++)
                    amount = amount > available / 2 ? available : amount * 2;
                amount = Math.Min(amount, available);
                long delta = seat == 0 ? amount : -amount;
                BalanceA = checked(BalanceA + delta); BalanceB = checked(BalanceB - delta);
                NetA = checked(NetA + delta); applied++;
                if (unit > 0 && available == amount) { BustWinner = seat; break; }
            }
            LastShot = shot; lastSeat = seat; lastStreak = firstStreak + applied - 1;
            Events += string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3};", shot, seat, firstStreak, applied);
        }
        public static ScoreCoinLedger Replay(long a, long b, long entry, int target, string events)
        {
            var ledger = new ScoreCoinLedger(a, b, entry, target);
            if (string.IsNullOrEmpty(events)) return ledger;
            if (events.Length > 262144 || !events.EndsWith(";", StringComparison.Ordinal)) throw new ArgumentException("Invalid coin log.");
            var rows = events.Split(';');
            if (rows.Length > 10001) throw new ArgumentException("Coin log too long.");
            for (int i = 0; i < rows.Length - 1; i++)
            {
                var fields = rows[i].Split(',');
                if (fields.Length != 4 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out int shot) ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seat) ||
                    !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int streak) ||
                    !int.TryParse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture, out int points))
                    throw new ArgumentException("Invalid coin event.");
                ledger.Add(shot, seat, streak, points);
            }
            if (ledger.Events != events) throw new ArgumentException("Noncanonical coin log.");
            return ledger;
        }
        public static long Commission(long profit) => profit > 0 ? profit / 20 : 0;
    }
}
