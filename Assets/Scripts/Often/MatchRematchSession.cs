namespace Assets.Scripts.Often
{
    // Votes belong to a completed match and cannot carry into the next match.
    public sealed class MatchRematchSession
    {
        readonly MatchStartSession start;
        readonly MatchResultSession result;
        readonly bool[] votes = new bool[2];
        double deadline;
        public bool Closed { get; private set; }
        public bool Both => !Closed && votes[0] && votes[1];
        public bool LocalRequested => votes[start.Configuration.LocalSeat];
        public MatchRematchSession(MatchStartSession start, MatchResultSession result)
        { this.start = start; this.result = result; }
        public bool Vote(int sender, string matchId, double now)
        {
            Tick(now);
            if (Closed || !result.Finished || start.Phase != MatchStartPhase.Started ||
                matchId != start.MatchId || double.IsNaN(now) || double.IsInfinity(now)) return false;
            int seat = sender == start.ActorForSeat(0) ? 0 : sender == start.ActorForSeat(1) ? 1 : -1;
            if (seat < 0 || votes[seat]) return false;
            if (!votes[0] && !votes[1]) deadline = now + 30;
            votes[seat] = true;
            return true;
        }
        public void Tick(double now)
        { if ((votes[0] || votes[1]) && now >= deadline) Closed = true; }
        public void Close() => Closed = true;
    }
}
