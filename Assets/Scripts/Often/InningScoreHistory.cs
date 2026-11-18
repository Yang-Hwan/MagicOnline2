using System;
using System.Collections.Generic;

namespace Assets.Scripts.Often
{
    [Serializable] public sealed class InningScoreSnapshot
    {
        public int active, visit0, visit1, baseline0, baseline1;
        public bool finished;
        public int[] row0, row1;
    }

    // Each player's nth visit shares column n, regardless of who breaks first.
    // Signed per-visit deltas keep foul deductions consistent with the displayed total.
    public sealed class InningScoreHistory
    {
        readonly List<int>[] rows={new List<int>(),new List<int>()};
        readonly int[] baseline=new int[2];
        public int Active {get;private set;}
        public bool Finished {get;private set;}
        public int Revision {get;private set;}
        public int Columns => Math.Max(rows[0].Count,rows[1].Count);
        public int Count(int seat)=>rows[seat].Count;
        public int? Score(int seat,int column)=>column>=0 && column<rows[seat].Count ? rows[seat][column] : (int?)null;
        public int Total(int seat){int total=baseline[seat];foreach(int score in rows[seat])total+=score;return total;}
        public void Reset(int firstSeat,int score0=0,int score1=0)
        {
            rows[0].Clear();rows[1].Clear();baseline[0]=score0;baseline[1]=score1;
            Active=-1;Finished=false;Activate(firstSeat);Revision++;
        }
        public void Activate(int seat)
        {
            if(seat<0 || seat>1 || Finished || Active==seat)return;
            rows[seat].Add(0);Active=seat;Revision++;
        }
        public void SetTotals(int score0,int score1)
        {
            if(Active<0)Reset(0);
            int[] totals={score0,score1};
            for(int seat=0;seat<2;seat++)
            {
                int delta=totals[seat]-Total(seat);if(delta==0)continue;
                if(rows[seat].Count==0)rows[seat].Add(0);
                rows[seat][rows[seat].Count-1]+=delta;Revision++;
            }
        }
        public void Finish(){if(!Finished){Finished=true;Revision++;}}
        public InningScoreSnapshot Capture()=>new InningScoreSnapshot {active=Active,finished=Finished,
            visit0=rows[0].Count,visit1=rows[1].Count,baseline0=baseline[0],baseline1=baseline[1],
            row0=rows[0].ToArray(),row1=rows[1].ToArray()};
        public static bool Valid(InningScoreSnapshot snapshot,int seat,int score0,int score1)
        {
            if(snapshot==null || snapshot.active!=seat || snapshot.finished || snapshot.row0==null || snapshot.row1==null ||
                snapshot.row0.Length>65536 || snapshot.row1.Length>65536 || snapshot.visit0!=snapshot.row0.Length ||
                snapshot.visit1!=snapshot.row1.Length || Math.Abs(snapshot.visit0-snapshot.visit1)>1 ||
                (seat==0?snapshot.visit0:snapshot.visit1)==0 || snapshot.baseline0<0 || snapshot.baseline1<0)return false;
            long a=snapshot.baseline0,b=snapshot.baseline1;
            foreach(int score in snapshot.row0)a+=score;foreach(int score in snapshot.row1)b+=score;
            return a==score0 && b==score1;
        }
        public void Restore(InningScoreSnapshot snapshot)
        {
            rows[0].Clear();rows[0].AddRange(snapshot.row0);rows[1].Clear();rows[1].AddRange(snapshot.row1);
            baseline[0]=snapshot.baseline0;baseline[1]=snapshot.baseline1;Active=snapshot.active;Finished=snapshot.finished;Revision++;
        }
    }
}
