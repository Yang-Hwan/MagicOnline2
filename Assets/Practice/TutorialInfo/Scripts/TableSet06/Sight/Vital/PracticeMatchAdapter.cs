using System;
using System.Collections;
using Assets.Scripts.Often;
using Assets.Scripts.Exert.Network;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public sealed class PracticeMatchAdapter : MonoBehaviour
    {
        const float FrameInterval = .05f;
        MatchStartSession session;
        MatchShotSession shots;
        MatchResultSession results;
        MatchContactRecord contacts;
        public MatchReplayArchive ReplayArchive { get; } = new MatchReplayArchive();
        public bool CanSaveMatchReplay => results != null && results.Finished && ReplayArchive.Ready && !archived;
        string archiveMessage;
        bool archived;
        public int RecordedBallContacts => contacts?.BallContacts ?? 0;
        Func<MatchResultMessage, bool> publishResult;
        Func<double> clock;
        Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch panel;
        PhysicsMng physics;
        Func<MatchShotCommand, bool> request;
        Func<MatchBallSnapshot, bool> publish;
        bool initialized, pending;
        int trailCueId;
        string lastStatus;
        int frame;
        float nextFrame, sampleStarted, pendingAt, lastReceived;
        MatchBallState[] from, target;
        public bool IsAuthority => session != null && session.LocalActor == session.AuthorityActor;
        public bool CanSimulate => initialized && IsAuthority && session.Phase == MatchStartPhase.Started &&
            shots != null && shots.Phase == MatchShotPhase.Moving;
        public bool CanSubmit => initialized && session.Phase == MatchStartPhase.Started && shots != null &&
            shots.Phase == MatchShotPhase.Ready && shots.TurnSeat == session.Configuration.LocalSeat && !pending &&
            results != null && results.CanShoot(clock());

        IEnumerator Start()
        {
            session = PracticeSceneFlow.OnlineSession;
            if (session == null) yield break;
            session.Changed += ShowStatus;
            yield return null;
            physics = GetComponent<PhysicsMng>();
            if (!physics || physics.ballcs.Length != session.Configuration.BallCount ||
                PoolCoach.Instance.Configuration != session.Configuration)
            { session.Abort("경기장 설정을 확인할 수 없습니다."); yield break; }
            foreach (var ball in physics.ballcs)
            {
                ball.body.linearVelocity = Vector3.zero;
                ball.body.angularVelocity = Vector3.zero;
                ball.body.Sleep();
            }
            physics.shotController.CuePutAside();
            physics.OnBallAllStop += OnStopped;
            physics.OnBallHitBall += OnContact;
            physics.OnBallHitBoard += OnCushion;
            panel = FindAnyObjectByType<Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface.PnlMatch>();
            initialized = true;
            var transport = FindAnyObjectByType<PracticeMatchSetupTransport>();
            if (transport && transport.Session == session)
            {
                BindChannel(transport.Shots, transport.RequestShot, transport.PublishBallState);
                BindResults(transport.Results, transport.PublishResult, () => Photon.Pun.PhotonNetwork.Time);
            }
            session.ReportLocalSceneReady();
            ShowStatus();
        }

        // Also permits an in-process channel for scene integration tests.
        public void BindChannel(MatchShotSession protocol, Func<MatchShotCommand, bool> sendRequest,
            Func<MatchBallSnapshot, bool> sendState)
        {
            if (!initialized || shots != null || protocol == null || sendRequest == null || sendState == null)
                throw new InvalidOperationException("An initialized scene and one complete channel are required.");
            shots = protocol; request = sendRequest; publish = sendState;
            shots.ShotCommitted += OnShot;
            shots.SnapshotApplied += OnSnapshot;
            if (!IsAuthority) foreach (var ball in physics.ballcs) ball.body.isKinematic = true;
            ShowStatus();
        }

        public bool Submit(MatchShotCommand stroke)
        {
            if (!CanSubmit || stroke == null) return false;
            var command = stroke.Copy();
            command.matchId = session.MatchId; command.shotId = shots.ShotId + 1; command.seat = shots.TurnSeat;
            command.turnRevision = shots.TurnRevision;
            pending = true; pendingAt = Time.unscaledTime; ShotCtrl.canControl = false;
            if (request(command)) return true;
            pending = false;
            session.Abort("타격 요청을 승인받지 못했습니다.");
            return false;
        }

        void Trace(string detail)
        {
            if (PracticeMatchTestRoute.Enabled && session != null)
                Debug.Log($"[PracticeMatchTest] match={session.MatchId} local={session.Configuration.LocalSeat} {detail}");
        }

        static string StateDigest(MatchBallState[] balls)
        {
            var text = new System.Text.StringBuilder();
            foreach (var b in balls)
            {
                float[] values = { b.position.x, b.position.y, b.position.z,
                    b.rotation.x, b.rotation.y, b.rotation.z, b.rotation.w,
                    b.velocity.x, b.velocity.y, b.velocity.z, b.angularVelocity.x, b.angularVelocity.y, b.angularVelocity.z };
                foreach (float value in values) text.Append(value.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            }
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }

        void OnShot(MatchShotCommand command)
        {
            Trace($"event=ShotCommitted shot={command.shotId} seat={command.seat}");
            pending = false; frame = 0; target = null;
            lastReceived = Time.unscaledTime;
            ShotCtrl.canControl = false;
            PoolPlayer.SetTurn(command.seat);
            contacts = new MatchContactRecord(session.Configuration.BallCount, command.seat, session.Configuration.CushionCount);
            physics.shotController.CueReadyShot();
            var pivot = physics.shotController.cuePivot;
            physics.shotController.ShowOnlineStroke(command.power, command.followThrough);
            trailCueId = command.seat;
            physics.BeginOnlineTrail(trailCueId);
            ReplayArchive.Begin(command, Quaternion.Inverse(pivot.parent.rotation) * Quaternion.Euler(0, command.yaw, 0));
            ReplayArchive.Sample(Capture()); archived = false; archiveMessage = "";
            if (IsAuthority) physics.StartApprovedShot(command).Forget();
            physics.shotController.CuePutAside();
            ShowStatus();
        }

        MatchBallState[] Capture()
        {
            var balls = new MatchBallState[physics.ballcs.Length];
            for (int i = 0; i < balls.Length; i++)
            {
                var body = physics.ballcs[i].body;
                balls[i] = new MatchBallState { position = body.position, rotation = body.rotation,
                    velocity = body.linearVelocity, angularVelocity = body.angularVelocity };
            }
            return balls;
        }

        void Publish(bool settled)
        {
            var state = new MatchBallSnapshot { matchId = session.MatchId, shotId = shots.ShotId,
                frame = frame++, settled = settled, balls = Capture() };
            if (!publish(state)) session.Abort("공 상태 동기화에 실패했습니다.");
        }

        void OnStopped(string _)
        {
            if (!CanSimulate) return;
            Publish(true);
            if (results != null && session.Phase == MatchStartPhase.Started)
                SendResult(results.Create(MatchResultKind.Shot, contacts.Resolve(), clock()));
            ShowStatus();
        }

        public void BindResults(MatchResultSession protocol, Func<MatchResultMessage, bool> send, Func<double> now)
        {
            if (shots == null || results != null || protocol == null || send == null || now == null)
                throw new InvalidOperationException("A shot channel and one result channel are required.");
            results = protocol; publishResult = send; clock = now;
            results.Applied += OnResult;
            StartClock();
        }

        void StartClock()
        {
            if (results != null && !results.Initialized && IsAuthority && shots.Phase == MatchShotPhase.Ready &&
                session.Phase == MatchStartPhase.Started)
                SendResult(results.Create(MatchResultKind.Begin, MatchShotOutcome.Miss, clock()));
        }

        void SendResult(MatchResultMessage result)
        {
            if (!publishResult(result)) session.Abort("경기 판정 동기화에 실패했습니다.");
        }

        void OnContact(BallC ball, BallC other, bool moving)
        {
            if (CanSimulate && moving && ball.id == shots.TurnSeat) contacts?.Ball(other.id);
        }

        void OnCushion(BallC ball, bool moving, CushionDir dir)
        {
            if (CanSimulate && moving && ball.id == shots.TurnSeat) contacts?.Cushion();
        }

        void OnResult(MatchResultMessage result)
        {
            Trace($"event=Result sequence={results.Sequence} kind={result.kind} shot={shots.ShotId} score={results.Score0}:{results.Score1} turn={shots.TurnSeat} finished={results.Finished}");
            pending = false;
            PoolPlayer.ApplyOnlineScores(results.Score0, results.Score1, results.Winner);
            PoolPlayer.SetTurn(shots.TurnSeat);
            PoolLogic.Instance.ShotEnded();
            PoolLogic.gameState.gameIsComplete = results.Finished;
            if (panel) panel.ApplyOnlineResult(results, shots.TurnSeat);
            if (results.Finished)
            {
                var view = FindAnyObjectByType<Assets.Scripts.Sight.Surface.Pavilion.PopMatchFinish>();
                var transport = FindAnyObjectByType<PracticeMatchSetupTransport>();
                if (view) view.ConfigureOnlineActions(
                    () => { if (transport && transport.Session == session) transport.RequestRematch(); },
                    () => transport && transport.Session == session && transport.CanRequestRematch,
                    () => !transport || transport.Session == null ? "재대전 연결 없음" :
                        transport.Session == session ? transport.RematchStatus :
                        transport.Session.Phase == MatchStartPhase.Aborted ? transport.Session.AbortReason : "새 대전 준비 중",
                    OpenMatchReplaySaveDialog, () => CanSaveMatchReplay,
                    () => archiveMessage);
            }
            if (!results.Finished && CanSubmit) physics.shotController.CueReadyShot();
            else physics.shotController.CuePutAside();
            ShowStatus();
            if (result.kind == MatchResultKind.Shot)
                PoolCoach.Instance.SetMatchInfo(result.outcome == MatchShotOutcome.Point ? "득점 +1" :
                    result.outcome == MatchShotOutcome.Foul ? "파울 · 감점 (최저 0점)" : "득점 없음 · 차례 교체");
        }

        void OnSnapshot(MatchBallSnapshot state)
        {
            if (PracticeMatchTestRoute.Enabled && state.settled)
                Trace($"event=Settled shot={state.shotId} frame={state.frame} digest={StateDigest(state.balls)}");
            lastReceived = Time.unscaledTime;
            if (state.settled && state.shotId == shots.ShotId && state.shotId > 0)
                ReplayArchive.Finish(state.balls);
            if (!IsAuthority)
            {
                from = Capture(); target = state.balls; sampleStarted = Time.unscaledTime;
                if (state.settled) { Display(1); target = null; }
            }
            if (state.shotId == 0)
            {
                PoolPlayer.SetTurn(shots.TurnSeat);
                physics.shotController.CueReadyShot();
                if (!CanSubmit) physics.shotController.CuePutAside();
            }
            ShowStatus();
        }

        void Display(float alpha)
        {
            for (int i = 0; i < target.Length; i++)
            {
                var body = physics.ballcs[i].body;
                var position = Vector3.Lerp(from[i].position, target[i].position, alpha);
                var rotation = Quaternion.Slerp(from[i].rotation, target[i].rotation, alpha);
                body.position = position; body.rotation = rotation;
                body.transform.SetPositionAndRotation(position, rotation);
            }
            if (shots.ShotId > 0) physics.DrawCueLinePath(physics.ballcs[trailCueId].body.position);
        }

        void FixedUpdate()
        {
            if (initialized && shots != null && session.Phase == MatchStartPhase.Started && shots.Phase == MatchShotPhase.Moving)
                ReplayArchive.Sample(Capture());
        }

        string MatchReplayTitle()
        {
            string left = Assets.Scripts.Exert.Match.PoolPlayer.players != null && Assets.Scripts.Exert.Match.PoolPlayer.players.Length > 0
                ? Assets.Scripts.Exert.Match.PoolPlayer.players[0]?.name : null;
            string right = Assets.Scripts.Exert.Match.PoolPlayer.players != null && Assets.Scripts.Exert.Match.PoolPlayer.players.Length > 1
                ? Assets.Scripts.Exert.Match.PoolPlayer.players[1]?.name : null;
            return $"{(string.IsNullOrWhiteSpace(left) ? "플레이어 1" : left)} vs {(string.IsNullOrWhiteSpace(right) ? "플레이어 2" : right)}";
        }

        public void OpenMatchReplaySaveDialog() => OpenMatchReplaySaveDialogForPanel(null);

        public void OpenMatchReplaySaveDialogForPanel(GameObject resultPanel)
        {
            if (!CanSaveMatchReplay) return;
            var finish = FindAnyObjectByType<Assets.Scripts.Sight.Surface.Pavilion.PopMatchFinish>();
            // PnlOutcome.OnDisable navigates away from the result page, so leave it active
            // underneath the save modal. PopMatchFinish has no such navigation side effect.
            var panelToHide = resultPanel ? null : finish ? finish.gameObject : null;
            System.Action closed = () =>
            {
                if (panelToHide) { panelToHide.SetActive(true); panelToHide.transform.SetAsLastSibling(); }
            };
            if (physics.OpenMatchReplaySavePanel(SaveOnlineReplay, closed, MatchReplayTitle()) && panelToHide)
                panelToHide.SetActive(false);
        }

        void SaveOnlineReplay(int slot, string directory, string title)
        {
            if (!CanSaveMatchReplay) return;
            try
            {
                if (!ReplayArchive.SaveToSlot(directory, slot))
                { physics.SetReplayPanelMessage("저장에 실패했습니다 · 저장 공간을 확인하세요"); return; }
                archived = true;
                if (string.IsNullOrWhiteSpace(title)) title = MatchReplayTitle();
                bool titled = physics.SetReplaySlotTitle(slot, title);
                archiveMessage = $"{slot + 1:00}번 저장 완료 · 리플레이-시합에서 확인" + (titled ? "" : " (제목 저장 실패)");
                physics.CloseMatchReplaySavePanel();
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            { physics.SetReplayPanelMessage("저장 실패 · 저장 공간과 권한을 확인하세요"); }
        }

        void Update()
        {
            if (session == null) return;
            session.Tick(Time.realtimeSinceStartupAsDouble);
            // Keep the point-selection popup's temporary input lock while it is open.
            if (!CanSubmit) ShotCtrl.canControl = false;
            if (!initialized || shots == null || session.Phase != MatchStartPhase.Started) return;
            StartClock();
            if (PracticeMatchTestRoute.Enabled && Input.GetKeyDown(KeyCode.F8) && CanSubmit && ShotCtrl.canControl &&
                !physics.shotController.IsShotAnimating)
            {
                var testStroke = physics.shotController.CaptureOnlineStroke();
                testStroke.power = .25f;
                Trace("event=TestShotRequested");
                Submit(testStroke);
            }
            if (results != null && results.Initialized)
            {
                if (IsAuthority)
                {
                    var due = results.Due(clock());
                    if (due != null) SendResult(due);
                }
                if (panel) panel.UpdateOnlineClock(results, shots.TurnSeat, shots.Phase == MatchShotPhase.Ready, clock());
            }
            if (pending && Time.unscaledTime - pendingAt > 5)
            { session.Abort("타격 승인 시간이 초과되었습니다."); return; }
            if (CanSimulate && physics.inMove && Time.unscaledTime >= nextFrame)
            { nextFrame = Time.unscaledTime + FrameInterval; Publish(false); }
            if (!IsAuthority && shots.Phase == MatchShotPhase.Moving)
            {
                if (Time.unscaledTime - lastReceived > 10)
                { session.Abort("공 상태 수신이 중단되었습니다."); return; }
                if (target != null) Display(Mathf.Clamp01((Time.unscaledTime - sampleStarted) / FrameInterval));
            }
        }

        void ShowStatus()
        {
            if (!initialized || !isActiveAndEnabled) return;
            if (session.Phase == MatchStartPhase.Started && shots != null && IsAuthority &&
                shots.Phase == MatchShotPhase.AwaitingInitialState)
            {
                // Only authority chooses the opening layout.
                for (int i = 0; i < physics.ballcs.Length; i++)
                {
                    var body = physics.ballcs[i].body;
                    var position = new Vector3(Mathf.Lerp(physics.min_x, physics.max_x, i % 2 == 0 ? .25f : .75f),
                        physics.resetPos[i].y, Mathf.Lerp(physics.min_z, physics.max_z, i < 2 ? .25f : .75f));
                    body.position = position; body.transform.position = position;
                }
                Physics.SyncTransforms();
                Publish(true);
            }
            ShotCtrl.canControl = CanSubmit;
            if (session.Phase != MatchStartPhase.Started)
            {
                physics.shotController.CuePutAside();
                if (session.Phase == MatchStartPhase.Suspended || session.Phase == MatchStartPhase.Aborted)
                { physics.FreezeOnlineMotion(); ReplayArchive.DiscardIncomplete(); target = null; }
            }
            string status = session.Phase == MatchStartPhase.Aborted ? session.AbortReason :
                session.Phase == MatchStartPhase.Suspended ? "연결 복구 중 · 최대 30초 대기" :
                results != null && results.Finished ? (results.Winner == -2 ? "무승부" : "경기 종료") :
                shots == null || shots.Phase == MatchShotPhase.AwaitingInitialState ? "상대방 준비 중" :
                shots.Phase == MatchShotPhase.Moving ? "타격 진행 중" :
                shots.Phase == MatchShotPhase.AwaitingResult ? "경기 결과 확인 중" :
                CanSubmit ? "내 차례" : "상대방 차례";
            if (status != lastStatus)
            {
                lastStatus = status;
                PoolCoach.Instance.SetMatchInfo(status);
                if (panel) panel.SetOnlineStatus(status);
                Trace($"event=Status phase={session.Phase}");
            }
        }

        void OnApplicationQuit()
        {
            // Quit callbacks run before scene objects are destroyed. Do not update
            // cue/UI objects when the transport later aborts during teardown.
            initialized = false;
            ShotCtrl.canControl = false;
            if (session != null) session.Changed -= ShowStatus;
        }

        void OnDestroy()
        {
            if (physics) physics.OnBallAllStop -= OnStopped;
            if (physics) { physics.OnBallHitBall -= OnContact; physics.OnBallHitBoard -= OnCushion; }
            if (results != null) results.Applied -= OnResult;
            if (shots != null) { shots.ShotCommitted -= OnShot; shots.SnapshotApplied -= OnSnapshot; }
            if (session == null) return;
            session.Changed -= ShowStatus;
            session.Abort("경기장이 종료되었습니다.");
        }
    }
}
