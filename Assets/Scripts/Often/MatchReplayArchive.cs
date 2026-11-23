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
        const int MaxFrames = 120000;
        readonly List<MatchBallState[]> frames = new List<MatchBallState[]>();
        readonly List<float> times=new List<float>();
        public MatchReplayPresentation Presentation {get;private set;}
        MatchReplayRecorder recorder;
        public string FailureReason {get;private set;}="";
        MatchShotCommand shot;
        Quaternion cueRotation;
        float step = .02f;
        bool complete, overflow, recordingStarted;
        int ballCount;
        public bool Ready => recordingStarted && complete && !overflow && frames.Count > 1;
        public void Reset()
        {
            if(recorder)recorder.Stop();recorder=null;Presentation=null;times.Clear();FailureReason="";
            frames.Clear(); shot = null; cueRotation = Quaternion.identity; step = .02f;
            complete = overflow = recordingStarted = false; ballCount = 0;
        }
        public void Begin(MatchShotCommand command, Quaternion rotation)
        {
            if(Presentation!=null){if(command!=null&&shot==null){shot=command.Copy();cueRotation=rotation;}RecordEvent("shot",command==null?"":JsonUtility.ToJson(command));return;}
            if (command == null || (shot != null && !complete)) { overflow = true; return; }
            if (!recordingStarted) { frames.Clear(); recordingStarted = true; }
            shot = command.Copy(); cueRotation = rotation; step = Mathf.Clamp(Time.fixedDeltaTime, .001f, .1f); complete = false;
        }
        public void Sample(MatchBallState[] balls)
        {
            if(Presentation!=null)return;
            if (shot == null || complete || overflow || balls == null || balls.Length == 0) return;
            if (ballCount == 0) ballCount = balls.Length;
            if (balls.Length != ballCount || frames.Count >= MaxFrames) { overflow = true; return; }
            frames.Add((MatchBallState[])balls.Clone());
        }
        public void DiscardIncomplete() { if(Presentation!=null){RecordEvent("connection","연결 중단·복구 대기");return;}if (!complete) overflow = true; }
        public void Finish(MatchBallState[] balls)
        { if(Presentation!=null)return;Sample(balls); complete = shot != null; }

        public void StartTimeline(MonoBehaviour owner,Func<MatchBallState[]> capture,Func<bool> finished,int localSeat=0,string matchId="",Func<Camera> camera=null)
        {
            if(Presentation!=null)return;
            Presentation=new MatchReplayPresentation{localSeat=Mathf.Clamp(localSeat,0,1),matchId=matchId??""};step=1f/30f;recordingStarted=true;
            try{recorder=MatchReplayRecorder.Install(owner,this,capture,finished,camera);}catch(Exception error){Failed(error);}
        }
        internal void TimelineFrame(float time,MatchBallState[] balls)
        {
            if(overflow||balls==null||balls.Length<3)return;
            if(frames.Count>=MaxFrames)throw new InvalidDataException("시합 기록의 최대 길이를 초과했습니다.");
            if(ballCount==0)ballCount=balls.Length;if(ballCount!=balls.Length)throw new InvalidDataException("Replay ball count changed");
            if(times.Count>0&&time<=times[times.Count-1])throw new InvalidDataException("Replay clock did not advance");
            frames.Add((MatchBallState[])balls.Clone());times.Add(time);
        }
        internal void TimelineComplete(){complete=true;}
        internal void Failed(Exception error){overflow=true;FailureReason=error.Message;Debug.LogError("시합 리플레이 기록 실패: "+error.Message);}
        public void RecordEvent(string kind,string value)
        {
            if(Presentation==null)return;float time=times.Count>0?times[times.Count-1]:0;
            Presentation.events.Add(new MatchReplayPresentation.Event{time=time,kind=kind,value=value??""});
        }
        public void Seal(){if(recorder)recorder.Seal();}
        public void StopPresentation(){if(recorder)recorder.Stop();}

        // Full matches use v5 lossless motion compression plus event presentation.
        // Standalone shot archives retain the legacy v3 layout.
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
            Seal();
            if (!Ready || slot < 0 || slot >= 20) return false;
            {
                string path = Path.Combine(directory, $"slot{slot + 1:00}.bin");
                string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    Directory.CreateDirectory(directory);FailureReason="";
                    using (var writer = new BinaryWriter(File.Create(temp)))
                    {
                        writer.Write(0x4D525031); writer.Write(Presentation!=null?5:3); writer.Write(ballCount);
                        writer.Write(Mathf.Clamp(shot!=null?shot.seat:0, 0, ballCount - 1)); writer.Write(step); writer.Write(frames.Count);
                        writer.Write(shot!=null);
                        if(shot!=null){writer.Write(shot.power); writer.Write(shot.followThrough);
                            var point = Vector2.ClampMagnitude(shot.contact * 400f, 370f);
                            writer.Write(point.x); writer.Write(point.y);Rotation(writer, cueRotation);}
                        if(Presentation!=null)MatchReplayMotionCodec.Write(writer,times,frames,ballCount);
                        else for (int i = 0; i < frames.Count; i++)
                        {
                            writer.Write(Presentation!=null?times[i]:i*step);
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
                        if(Presentation!=null)Presentation.Write(writer);
                    }
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                    return true;
                }
                catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is InvalidDataException){FailureReason=error.Message;return false;}
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
        }
        static void Vector(BinaryWriter w, Vector3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        static void Rotation(BinaryWriter w, Quaternion q) { w.Write(q.x); w.Write(q.y); w.Write(q.z); w.Write(q.w); }
    }
}
