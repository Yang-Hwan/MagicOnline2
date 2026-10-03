using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Prack.BackSys
{

    [System.Serializable]
    public class UserMainData
    {
        public string NickName;
        public string InDate;
        public int LvVel;
        public int LvProc;
        public long Coin;
        public int Jem;
        public int Win;
        public int Lose;
        public int AvatarIdx;

        public override string ToString()
        {
            string result = string.Empty;
            result += $"NickName : {NickName}, ";
            result += $"Coin : {Coin}, ";
            result += $"Jem : {Jem}, ";
            result += $"Win : {Win}, ";
            result += $"Lose : {Lose}, ";
            result += $"InDate : {InDate}  ";
            return result;
        }
        public void Reset()
        {
            NickName = "";
            InDate = "";
            LvVel = 1;
            LvProc = 0;
            Coin = 20;
            Jem = 0;
            Win = 0;
            Lose = 0;
            AvatarIdx = 1;
        }
    }

}
