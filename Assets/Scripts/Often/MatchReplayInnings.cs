using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Sight.Surface.Pavilion;
using UnityEngine;
namespace Assets.Scripts.Often
{
    [Serializable] public sealed class ReplayInningEvent
    {
        public int frame;public InningScoreSnapshot scores;public string name0,name1;
    }
    public sealed class MatchReplayInnings
    {
        public sealed class Visit {public int seat,column,frame;public float time;}
        public readonly List<Visit> Visits=new List<Visit>();
        public readonly InningScoreHistory Scores=new InningScoreHistory();
        public readonly string[] Names={"선수 1","선수 2"};
        public bool Available=>Visits.Count>0;
        public MatchReplayInnings(MatchReplayPresentation data)
        {
            Names[data.localSeat]="나";Names[1-data.localSeat]="상대";
            int[] counts={0,0};
            foreach(var e in data.events){
                if(e.kind!="inning-score")continue;
                var record=JsonUtility.FromJson<ReplayInningEvent>(e.value);var s=record?.scores;
                if(s==null||s.row0==null||s.row1==null||s.active<0||s.active>1||s.row0.Length>65536||s.row1.Length>65536||record.frame<0||record.frame>=data.frames.Count)continue;
                int seat=s.active;int count=seat==0?s.row0.Length:s.row1.Length;
                if(count>counts[seat])Visits.Add(new Visit{seat=seat,column=count-1,frame=record.frame,time=e.time});
                counts[0]=s.row0.Length;counts[1]=s.row1.Length;Scores.Restore(s);
                if(!string.IsNullOrEmpty(record.name0))Names[0]=record.name0;if(!string.IsNullOrEmpty(record.name1))Names[1]=record.name1;
            }
            if(!Available)BuildLegacyIndex(data);
            Scores.Finish();
        }
        void BuildLegacyIndex(MatchReplayPresentation data)
        {
            var parts=new Dictionary<string,string>();int hud=-1;string previous=null;bool started=false;
            foreach(var e in data.events){
                if(e.kind.StartsWith("visual-part:",StringComparison.Ordinal))parts[e.kind.Substring(12)]=e.value;
                else if(e.kind=="visual-node"&&hud<0){var node=JsonUtility.FromJson<ReplayVisualNode>(e.value);if(node.kind=="hud"||node.kind=="hud-art")hud=node.id;}
                else if(e.kind=="visual-state"&&hud>=0){
                    var state=JsonUtility.FromJson<ReplayVisualState>(e.value);if(state.id!=hud||string.IsNullOrEmpty(state.geometry)||state.geometry==previous)continue;previous=state.geometry;
                    string json=state.geometry.StartsWith("@part:",StringComparison.Ordinal)?parts.TryGetValue(state.geometry,out var part)?part:null:state.geometry;if(json==null)continue;
                    var visual=JsonUtility.FromJson<MatchHudView.ReplayVisual>(json);if(visual==null||visual.side<0||visual.side>1||visual.counters==null||visual.counters.Length!=2)continue;
                    int seat=visual.side==0?data.localSeat:1-data.localSeat;
                    if(!started){Scores.Reset(seat);started=true;Visits.Add(new Visit{seat=seat,column=0,frame=state.frame,time=e.time});}
                    else if(Scores.Active!=seat){Scores.Activate(seat);Visits.Add(new Visit{seat=seat,column=Scores.Count(seat)-1,frame=state.frame,time=e.time});}
                    Scores.SetTotals(visual.counters[data.localSeat==0?0:1].Actual,visual.counters[data.localSeat==0?1:0].Actual);
                }
            }
        }
        public Visit Find(int seat,int column)=>Visits.Find(v=>v.seat==seat&&v.column==column);
        public Visit At(int frame){Visit found=null;foreach(var visit in Visits){if(visit.frame>frame)break;found=visit;}return found;}
    }
}
