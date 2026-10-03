using System;

namespace Assets.Scripts.Often
{
    public enum MatchStartPhase { Configuring, Loading, Ready, Started, Aborted, Suspended }

    // SenderActor must come from the transport envelope, never a client payload.
    public sealed class MatchStartSession
    {
        readonly int actor0, actor1;
        readonly bool[] accepted = new bool[2];
        readonly bool[] loaded = new bool[2];
        readonly double deadline;
        public string MatchId { get; }
        public string ConfigurationKey { get; }
        public MatchConfiguration Configuration { get; }
        public int AuthorityActor => actor0;
        public int LocalActor => Configuration.LocalSeat == 0 ? actor0 : actor1;
        public MatchStartPhase Phase { get; private set; }
        public string AbortReason { get; private set; }
        public event Action<int, string, string> LocalSceneReady;
        public event Action Changed;

        public MatchStartSession(string matchId, string configurationKey, MatchConfiguration configuration,
            int seat0Actor, int seat1Actor, double now, double timeoutSeconds = 30)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(configurationKey))
                throw new ArgumentException("A match identity and agreed configuration key are required.");
            if (configuration == null || configuration.Execution != MatchExecutionMode.OnlineMatch)
                throw new ArgumentException("An online configuration is required.");
            if (seat0Actor < 1 || seat1Actor < 1 || seat0Actor == seat1Actor)
                throw new ArgumentException("Two distinct Photon actors are required.");
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(timeoutSeconds) ||
                double.IsInfinity(timeoutSeconds) || timeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            MatchId = matchId; ConfigurationKey = configurationKey; Configuration = configuration;
            actor0 = seat0Actor; actor1 = seat1Actor; deadline = now + timeoutSeconds;
        }

        int Seat(int actor) => actor == actor0 ? 0 : actor == actor1 ? 1 : -1;
        public int ActorForSeat(int seat) => seat == 0 ? actor0 : seat == 1 ? actor1 : -1;
        public bool IsSceneReady(int actor) => Seat(actor) >= 0 && loaded[Seat(actor)];
        bool Matches(int sender, string id, string key) => Seat(sender) >= 0 &&
            MatchId == id && ConfigurationKey == key && Phase != MatchStartPhase.Aborted;

        public bool AcceptConfiguration(int sender, string id, string key)
        {
            if (!Matches(sender, id, key) || Phase != MatchStartPhase.Configuring) return false;
            accepted[Seat(sender)] = true;
            if (accepted[0] && accepted[1]) { Phase = MatchStartPhase.Loading; Changed?.Invoke(); }
            return true;
        }

        public bool MarkSceneReady(int sender, string id, string key)
        {
            if (!Matches(sender, id, key) || (Phase != MatchStartPhase.Loading && Phase != MatchStartPhase.Ready)) return false;
            loaded[Seat(sender)] = true;
            if (loaded[0] && loaded[1] && Phase != MatchStartPhase.Ready)
            { Phase = MatchStartPhase.Ready; Changed?.Invoke(); }
            return true;
        }

        public void ReportLocalSceneReady()
        {
            if (loaded[Configuration.LocalSeat]) return;
            if (MarkSceneReady(LocalActor, MatchId, ConfigurationKey))
                LocalSceneReady?.Invoke(LocalActor, MatchId, ConfigurationKey);
        }

        public bool CommitStart(int sender, string id)
        {
            if (sender != AuthorityActor || id != MatchId || Phase != MatchStartPhase.Ready) return false;
            Phase = MatchStartPhase.Started;
            Changed?.Invoke();
            return true;
        }

        public void Tick(double now)
        {
            if (Phase != MatchStartPhase.Started && Phase != MatchStartPhase.Aborted && Phase != MatchStartPhase.Suspended && now >= deadline)
                Abort("경기 준비 시간이 초과되었습니다.");
        }

        internal bool Suspend()
        {
            if (Phase != MatchStartPhase.Started) return false;
            Phase = MatchStartPhase.Suspended; Changed?.Invoke(); return true;
        }

        internal bool Resume(int sender, string id)
        {
            if (Phase != MatchStartPhase.Suspended || sender != AuthorityActor || id != MatchId) return false;
            Phase = MatchStartPhase.Started; Changed?.Invoke(); return true;
        }

        public void ParticipantLeft(int actor)
        {
            if (Seat(actor) >= 0) Abort("상대방과의 연결이 종료되었습니다.");
        }

        public void Abort(string reason)
        {
            if (Phase == MatchStartPhase.Aborted) return;
            Phase = MatchStartPhase.Aborted; AbortReason = reason; Changed?.Invoke();
        }
    }
}
