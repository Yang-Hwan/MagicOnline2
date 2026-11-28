using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Often;
using Assets.TutorialInfo.Scripts.TableSet06.Excert.Match;
using Assets.TutorialInfo.Scripts.TableSet06.Sight.Surface;
using UnityEngine;

namespace Assets.TutorialInfo.Scripts.TableSet06.Sight.Vital
{
    public partial class PhysicsMng
    {
        public bool ComputerOpponentEnabled { get; private set; } = PracticeSceneFlow.Configuration.HasComputerOpponent;
        public float ComputerSkill { get; set; } = .8f;
        readonly System.Random computerAccuracyRandom=new System.Random();
        public bool IsComputerTurn => ComputerOpponentEnabled && IsLocalPractice && !IsReplayBrowser && !IsStage && PoolPlayer.turnId == 1;
        public bool ComputerIsThinking { get; private set; }
        public int ComputerShotsPlayed { get; private set; }
        public int ComputerCandidatesEvaluated { get; private set; }
        public int ComputerCandidatesCompleted { get; private set; }
        public float ComputerBestScore { get; private set; }
        public float ComputerSearchSeconds { get; private set; }
        public int ComputerFreeFlightSteps { get; private set; }
        public bool ComputerSearchTimedOut { get; private set; }
        public MatchShotCommand LastComputerShot { get; private set; }

        Coroutine computerRoutine;
        ShotPrediction computerPrediction;
        bool CanComputerAim => isActiveAndEnabled && IsComputerTurn && PoolCoach.Instance.isMatchTimePlay &&
            PoolCoach.Instance.calculateTime && !PoolLogic.gameState.gameIsComplete && !PoolCoach.Instance.PracticeTimedOut &&
            !inMove && !scatterPending && !recordingShot && !shotController.IsShotAnimating && !IsPracticeReplay &&
            !IsBallPlacement && !replayLoadMode && !replaySavePanelOpen && !PracticeToolsVisible && Time.timeScale > 0;

        public void SetComputerOpponentEnabled(bool enabled)
        {
            if (!IsLocalPractice || IsReplayBrowser || IsStage || inMove || shotController.IsShotAnimating ||
                IsPracticeReplay || IsBallPlacement || replayLoadMode) return;
            StopComputerOpponent();
            ComputerOpponentEnabled=enabled;
            if (PoolPlayer.players!=null && PoolPlayer.players.Length>1)
                PoolPlayer.players[1].name=enabled ? "컴퓨터" : "Player 2";
            var panel=FindAnyObjectByType<PnlMatch>();
            if(panel)panel.RefreshComputerOpponent();
            if(!PoolLogic.gameState.gameIsComplete)ShotCtrl.canControl=!IsComputerTurn;
            PoolCoach.Instance.SetMatchInfo(enabled ? "컴퓨터 대전 · 상대 차례에는 자동으로 샷합니다" : "직접 연습 · 양쪽 선수를 조작할 수 있습니다");
        }

        void UpdateComputerOpponent()
        {
            if(ComputerIsThinking && !CanComputerAim)StopComputerOpponent();
            if(CanComputerAim && !ComputerIsThinking)
                computerRoutine=StartCoroutine(PlayComputerTurn());
        }
        void StopComputerOpponent()
        {
            if(computerRoutine!=null){StopCoroutine(computerRoutine);computerRoutine=null;}
            computerPrediction?.Dispose();computerPrediction=null;ComputerIsThinking=false;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void LogComputerSearch()
        {
            Debug.Log($"[Computer AI] balls={ballcs.Length} completed={ComputerCandidatesCompleted} score={ComputerBestScore:F1} seconds={ComputerSearchSeconds:F2} freeSteps={ComputerFreeFlightSteps} timeout={ComputerSearchTimedOut}",this);
        }

        IEnumerator PlayComputerTurn()
        {
            ComputerIsThinking=true;
            int version=simulationVersion, undo=undoVersion;
            var positions=System.Array.ConvertAll(ballcs,b=>b.body.position);
            bool Valid()
            {
                if(!CanComputerAim || version!=simulationVersion || undo!=undoVersion)return false;
                for(int i=0;i<ballcs.Length;i++)if((ballcs[i].body.position-positions[i]).sqrMagnitude>.000001f)return false;
                return true;
            }
            try
            {
                PoolCoach.Instance.SetMatchInfo("컴퓨터가 샷을 생각하고 있습니다…");
                shotController.HideLiveAimPrediction();
                // Yield before doing work so the thinking indication is displayed first.
                yield return null;
                if(!Valid())yield break;
                computerPrediction=new ShotPrediction(this);
                var candidates=ComputerShotPlanner.Candidates(ballcs,1,ComputerShotPlanner.PlayingArea(this),PoolCoach.Instance.Configuration.CushionCount);
                // Verify a very gentle fallback too, so rejecting thin routes cannot select
                // an unverified candidate when the search budget expires.
                candidates.Insert(0,new MatchShotCommand{seat=1,yaw=0,power=.001f,followThrough=ShotCtrl.DefaultFollowThrough});
                MatchShotCommand best=null;
                float bestScore=float.NegativeInfinity;
                float started=Time.realtimeSinceStartup, deadline=started+18f;
                long frameStart=System.Diagnostics.Stopwatch.GetTimestamp();
                double FrameMs() => (System.Diagnostics.Stopwatch.GetTimestamp()-frameStart)*1000.0/System.Diagnostics.Stopwatch.Frequency;
                double FrameBudget() => System.Math.Max(10,System.Math.Min(28,Time.unscaledDeltaTime*650));
                int coarse=0,iteration=0,refinedGroups=0;
                var refinements=new Queue<MatchShotCommand>();
                var tested=new HashSet<string>();
                ComputerCandidatesEvaluated=ComputerCandidatesCompleted=ComputerFreeFlightSteps=0;
                ComputerBestScore=float.NegativeInfinity;ComputerSearchTimedOut=false;
                while(coarse<candidates.Count || refinements.Count>0)
                {
                    // Survey different routes before refining a miss. Never consume
                    // the entire turn on neighbours of the first weak candidate.
                    bool refine=refinements.Count>0 && (++iteration%3==0 || coarse>=candidates.Count);
                    var candidate=refine ? refinements.Dequeue() : candidates[coarse++];
                    string key=$"{candidate.yaw:F3}/{candidate.power:F3}/{candidate.contact.x:F2}/{candidate.contact.y:F2}";
                    if(!tested.Add(key))continue;
                    if(!Valid())yield break;
                    computerPrediction.Begin(1,shotController.BuildOnlineImpulse(candidate),24f,80f,
                        caromCushions:PoolCoach.Instance.Configuration.CushionCount);
                    bool interrupted=false;
                    while(!computerPrediction.Advance(System.Math.Max(.1,FrameBudget()-FrameMs()),2048))
                    {
                        yield return null;frameStart=System.Diagnostics.Stopwatch.GetTimestamp();
                        if(!Valid())yield break;
                        if(Time.realtimeSinceStartup>=deadline && ComputerCandidatesCompleted>0)
                        {interrupted=true;ComputerSearchTimedOut=true;break;}
                    }
                    if(interrupted)break; // An unfinished path cannot beat a verified shot.
                    float score=ComputerShotPlanner.Score(computerPrediction,ballcs,1,candidate.power,PoolCoach.Instance.Configuration.CushionCount);
                    ComputerCandidatesEvaluated++;ComputerCandidatesCompleted++;
                    ComputerFreeFlightSteps+=computerPrediction.FreeFlightSteps;
                    if(score>bestScore){bestScore=score;best=candidate;}
                    if(bestScore>=10000)break;
                    if(ComputerCandidatesCompleted%6==0 && bestScore>100 && refinedGroups<8)
                    {
                        foreach(var neighbour in ComputerShotPlanner.Refine(best))refinements.Enqueue(neighbour);
                        refinedGroups++;
                    }
                    if(Time.realtimeSinceStartup>=deadline){ComputerSearchTimedOut=true;break;}
                    if(FrameMs()>=FrameBudget()){yield return null;frameStart=System.Diagnostics.Stopwatch.GetTimestamp();}
                }
                ComputerBestScore=bestScore;
                if(best==null)yield break; // Never execute a rejected or unfinished route.
                var inaccurate=ComputerShotPlanner.ApplyAccuracy(best,computerAccuracyRandom,ComputerSkill);
                if(!ReferenceEquals(inaccurate,best)) {
                    computerPrediction.Begin(1,shotController.BuildOnlineImpulse(inaccurate),24f,80f,
                        caromCushions:PoolCoach.Instance.Configuration.CushionCount);
                    while(!computerPrediction.Advance(FrameBudget(),2048)) {
                        yield return null;if(!Valid())yield break;
                    }
                    // Execution error must not reintroduce a forbidden thin hit.
                    if(computerPrediction.MinimumObjectThickness>=ComputerShotPlanner.MinimumObjectThickness)best=inaccurate;
                }
                LastComputerShot=best;
                ComputerSearchSeconds=Time.realtimeSinceStartup-started;
                LogComputerSearch();
                computerPrediction.Dispose();computerPrediction=null;
                if(!Valid())yield break;
                shotController.PrepareComputerStroke(best);
                float readyAt=Time.realtimeSinceStartup+.55f;
                PoolCoach.Instance.SetMatchInfo("컴퓨터가 샷합니다");
                while(Time.realtimeSinceStartup<readyAt){yield return null;if(!Valid())yield break;}
                shotController.PlayComputerStroke();
            }
            finally
            {
                computerPrediction?.Dispose();computerPrediction=null;
                computerRoutine=null;ComputerIsThinking=false;
            }
        }
    }
}
