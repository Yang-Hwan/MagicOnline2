using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Prack.BackSys.ChartData
{
    [System.Serializable]
    public class SkillMatchData
    {

        public int HallIdx;             // 대결코드
        public int MatchBall;            // 대전종류
        public int TargetHit;           // 목표갯수
        public long PrizeCoin;          // 기본입장료
        public long MaxCoin;            // 
        public string HallName;         // 경기장명
        public int MatchTotMin;         // 대전시간 
        public int MatchCushion;         // 대전쿠션
        public int FinishMission;         // 마무리미션

        public override string ToString()
        {
            string result = string.Empty;
            result += $"HallIdx : {HallIdx}, ";
            result += $"MatchBall : {MatchBall}, ";
            result += $"TargetHit : {TargetHit}, ";
            result += $"PrizeCoin : {PrizeCoin}, ";
            result += $"MaxCoin : {MaxCoin}, ";
            result += $"HallName : {HallName}, ";
            result += $"MatchTotMin : {MatchTotMin} ";
            result += $"MatchCushion : {MatchCushion} ";
            result += $"FinishMission : {FinishMission} ";

            return result;
        }
    }



}
