using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Often
{
    // Records the received presentation at a fixed step, independently of live physics.
    // No network replay playback can alter a live match.
    public sealed class MatchReplayArchive
    {
        const int MaxFrames = 60000;
        readonly List<MatchBallState[]> frames = new List<MatchBallState[]>();
        MatchShotCommand shot;
        Quaternion cueRotation;
        float step = .02f;
        bool complete, overflow, recordingStarted;
        int ballCount;
        public bool Ready => recordingStarted && complete && !overflow && frames.Count > 1;
        public void Reset()
        {
            frames.Clear(); shot = null; cueRotation = Quaternion.identity; step = .02f;
            complete = overflow = recordingStarted = false; ballCount = 0;
        }
        public void Begin(MatchShotCommand command, Quaternion rotation)
        {
            if (command == null || (shot != null && !complete)) { overflow = true; return; }
            if (!recordingStarted) { frames.Clear(); recordingStarted = true; }
            shot = command.Copy(); cueRotation = rotation; step = Mathf.Clamp(Time.fixedDeltaTime, .001f, .1f); complete = false;
        }
        public void Sample(MatchBallState[] balls)
        {
            if (shot == null || complete || overflow || balls == null || balls.Length == 0) return;
            if (ballCount == 0) ballCount = balls.Length;
            if (balls.Length != ballCount || frames.Count >= MaxFrames) { overflow = true; return; }
            frames.Add((MatchBallState[])balls.Clone());
        }
        public void DiscardIncomplete() { if (!complete) overflow = true; }
        public void Finish(MatchBallState[] balls)
        { Sample(balls); complete = shot != null; }

        // Stores the full match as one version-3 replay, compatible with the
        // existing deterministic frame reader and playback controls.
        public int SaveToEmptySlot(string directory)
        {
            if (!Ready) return -1;
            Directory.CreateDirectory(directory);
            for (int slot = 0; slot < 20; slot++)
            {
                string path = Path.Combine(directory, $"slot{slot + 1:00}.bin");
                if (File.Exists(path)) continue;
                if (SaveToSlot(directory, slot)) return slot;
            }
            return -1;
        }

        public bool SaveToSlot(string directory, int slot)
        {
            if (!Ready || slot < 0 || slot >= 20) return false;
            Directory.CreateDirectory(directory);
            {
                string path = Path.Combine(directory, $"slot{slot + 1:00}.bin");
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var writer = new BinaryWriter(File.Create(temp)))
                    {
                        writer.Write(0x4D525031); writer.Write(3); writer.Write(ballCount);
                        writer.Write(Mathf.Clamp(shot.seat, 0, ballCount - 1)); writer.Write(step); writer.Write(frames.Count);
                        writer.Write(true); writer.Write(shot.power); writer.Write(shot.followThrough);
                        var point = Vector2.ClampMagnitude(shot.contact * 400f, 370f);
                        writer.Write(point.x); writer.Write(point.y);
                        Rotation(writer, cueRotation);
                        for (int i = 0; i < frames.Count; i++)
                        {
                            writer.Write(i * step);
                            foreach (var ball in frames[i])
                            {
                                Vector(writer, ball.position); Rotation(writer, ball.rotation);
                                Vector(writer, ball.velocity); Vector(writer, ball.angularVelocity);
                            }
                            // Online archives preserve ball motion; the local cue animation
                            // is intentionally absent from authoritative network snapshots.
                            writer.Write(false); writer.Write(false);
                            Vector(writer, Vector3.zero); Rotation(writer, Quaternion.identity);
                            Vector(writer, Vector3.zero); Vector(writer, Vector3.zero);
                        }
                    }
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                    return true;
                }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        static void Vector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static void Rotation(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
    }
}
