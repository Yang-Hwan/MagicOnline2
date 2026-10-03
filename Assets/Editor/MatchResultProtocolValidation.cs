using System;
using Assets.Scripts.Often;
using Assets.Scripts.Prack.BackSys.ChartData;
using UnityEngine;

public static class MatchResultProtocolValidation
{
    static void Require(bool ok, string reason) { if (!ok) throw new Exception("Result protocol: " + reason); }
    public static void Run(int ballCount)
    {
        var contacts = new MatchContactRecord(ballCount, 0, 3);
        if (ballCount == 4)
        {
            Require(contacts.Resolve() == MatchShotOutcome.Foul, "no reds must foul");
            contacts.Ball(2); Require(contacts.Resolve() == MatchShotOutcome.Miss, "one red must miss");
            contacts.Ball(3); Require(contacts.Resolve() == MatchShotOutcome.Point, "both reds must score");
            contacts.Ball(1); Require(contacts.Resolve() == MatchShotOutcome.Foul, "late opponent contact must override point");
        }
        else
        {
            contacts.Ball(1); contacts.Ball(2);
            contacts.Cushion(); contacts.Cushion(); contacts.Cushion();
            Require(contacts.Resolve() == MatchShotOutcome.Miss, "late cushions rescued missed three-ball shot");
            contacts = new MatchContactRecord(3, 1, 3);
            contacts.Ball(0); contacts.Cushion(); contacts.Cushion(); contacts.Cushion(); contacts.Ball(2);
            Require(contacts.Resolve() == MatchShotOutcome.Point, "three-cushion point rejected");
        }
        var starts = new MatchStartSession[2]; var shots = new MatchShotSession[2]; var results = new MatchResultSession[2];
        var hall = new SkillMatchData { HallIdx = 1, MatchBall = ballCount, TargetHit = 2,
            MatchCushion = ballCount == 3 ? 3 : 0, MatchTotMin = 1 };
        var state = new MatchBallSnapshot { matchId = "result-test", frame = 0, settled = true, balls = new MatchBallState[ballCount] };
        for (int i = 0; i < ballCount; i++) state.balls[i].rotation = Quaternion.identity;
        for (int i = 0; i < 2; i++)
        {
            starts[i] = new MatchStartSession(state.matchId, "rules", MatchConfiguration.Online(hall, "A", "B", 0, i, 10), 11, 22, 0);
            starts[i].AcceptConfiguration(11, state.matchId, "rules"); starts[i].AcceptConfiguration(22, state.matchId, "rules");
            starts[i].MarkSceneReady(11, state.matchId, "rules"); starts[i].MarkSceneReady(22, state.matchId, "rules");
            starts[i].CommitStart(11, state.matchId);
            shots[i] = new MatchShotSession(starts[i]); results[i] = new MatchResultSession(starts[i], shots[i]);
            Require(!results[i].Apply(11, results[i].Create(MatchResultKind.Begin, MatchShotOutcome.Miss, 100)), "clock began without layout");
            shots[i].ApplySnapshot(11, state);
        }
        void ApplyBoth(MatchResultMessage message)
        {
            message = JsonUtility.FromJson<MatchResultMessage>(JsonUtility.ToJson(message));
            Require(!results[1].Apply(22, message), "guest authored result");
            foreach (var result in results)
            {
                Require(result.Apply(11, message), "valid result rejected");
                Require(!result.Apply(11, message), "duplicate result accepted");
            }
            Require(results[0].Score0 == results[1].Score0 && results[0].Score1 == results[1].Score1 &&
                results[0].Winner == results[1].Winner && shots[0].TurnSeat == shots[1].TurnSeat, "replicas diverged");
        }
        var rematch = new MatchRematchSession(starts[0], results[0]);
        Require(!rematch.Vote(11, state.matchId, 99), "rematch accepted before finish");
        ApplyBoth(results[0].Create(MatchResultKind.Begin, MatchShotOutcome.Miss, 100));
        Require(results[0].CanShoot(109) && !results[0].CanShoot(110), "turn deadline boundary wrong");
        Require(!results[0].Apply(11, results[0].Create(MatchResultKind.TurnTimeout, MatchShotOutcome.Miss, 109)), "early timeout accepted");
        ApplyBoth(results[0].Due(110)); ApplyBoth(results[0].Due(120));
        var stale = new MatchShotCommand { matchId = state.matchId, shotId = 1, seat = 0, power = .5f };
        Require(!shots[0].CanApprove(11, stale), "old turn input accepted after two idle timeouts");
        void Shot(MatchShotOutcome outcome, double now)
        {
            var shot = new MatchShotCommand { matchId = state.matchId, shotId = shots[0].ShotId + 1,
                seat = shots[0].TurnSeat, turnRevision = shots[0].TurnRevision, power = .5f };
            foreach (var replica in shots) Require(replica.CommitShot(11, shot), "shot rejected");
            var message = results[0].Create(MatchResultKind.Shot, outcome, now);
            Require(!results[0].Apply(11, message), "point awarded before terminal snapshot");
            Require(results[0].Due(161) == null, "match clock ended a moving shot");
            state.shotId = shot.shotId; state.frame = 0;
            foreach (var replica in shots) Require(replica.ApplySnapshot(11, state), "final state rejected");
            ApplyBoth(message);
        }
        Shot(MatchShotOutcome.Point, 121);
        Require(results[0].Score0 == 1 && shots[0].TurnSeat == 0, "point did not retain turn");
        Shot(ballCount == 4 ? MatchShotOutcome.Foul : MatchShotOutcome.Miss, 122);
        Require(results[0].Score0 == (ballCount == 4 ? 0 : 1) && shots[0].TurnSeat == 1, "miss/foul transition wrong");
        if (ballCount == 4)
        {
            Shot(MatchShotOutcome.Foul, 123); Require(results[0].Score1 == 0, "negative score allowed");
            Shot(MatchShotOutcome.Miss, 124);
        }
        Shot(MatchShotOutcome.Point, 125); Shot(MatchShotOutcome.Point, 126);
        Require(results[0].Finished && results[1].Finished && results[0].Winner == 1 &&
            !results[0].CanShoot(127), "target win failed");
        Require(!rematch.Vote(99, state.matchId, 127) && !rematch.Vote(11, "old", 127), "invalid rematch vote accepted");
        Require(rematch.Vote(11, state.matchId, 127) && !rematch.Both &&
            !rematch.Vote(11, state.matchId, 128), "single/duplicate vote started rematch");
        Require(rematch.Vote(22, state.matchId, 128) && rematch.Both, "mutual rematch consent missing");
        var nextId = Guid.NewGuid().ToString("N");
        var next = new MatchStartSession(nextId, "next-rules", MatchConfiguration.Online(hall, "A", "B", 1, 0, 10), 11, 22, 200);
        var nextShots = new MatchShotSession(next); var nextResult = new MatchResultSession(next, nextShots);
        Require(!next.AcceptConfiguration(22, state.matchId, "rules") &&
            !next.CommitStart(11, nextId), "old identity or missing readiness started rematch");
        next.AcceptConfiguration(11, nextId, "next-rules"); next.AcceptConfiguration(22, nextId, "next-rules");
        next.MarkSceneReady(11, nextId, "next-rules"); next.MarkSceneReady(22, nextId, "next-rules");
        Require(next.CommitStart(11, nextId), "new rematch bootstrap failed");
        var opening = state.Copy(); opening.matchId = nextId; opening.shotId = 0; opening.frame = 0;
        Require(nextShots.ApplySnapshot(11, opening) && nextShots.TurnSeat == 1 && nextShots.ShotId == 0 &&
            nextResult.Score0 == 0 && nextResult.Score1 == 0 && !nextResult.Initialized && !nextResult.Finished,
            "rematch retained old scores, shot number or first seat");
        Require(!nextShots.ApplySnapshot(11, state) &&
            !nextResult.Apply(11, results[0].Create(MatchResultKind.Shot, MatchShotOutcome.Point, 201)), "old state/result altered rematch");
        var expired = new MatchRematchSession(starts[1], results[1]);
        Require(expired.Vote(22, state.matchId, 127), "local guest vote failed");
        Require(!expired.Vote(11, state.matchId, 157) && expired.Closed && !expired.Both, "expired vote revived rematch");
        rematch.Close(); Require(!rematch.Both, "closed rematch retained authorization");
        Require(!results[0].Apply(11, results[0].Create(MatchResultKind.MatchTimeout, MatchShotOutcome.Miss, 200)), "finished result overwritten");
    }
}
