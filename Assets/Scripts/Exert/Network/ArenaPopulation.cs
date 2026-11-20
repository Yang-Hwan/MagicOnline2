using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Realtime;

namespace Assets.Scripts.Exert.Network
{
    public static class ArenaPopulation
    {
        public const string OnlinePlayers="arenaOnline";
        public static bool TryHall(string name,out int hall)
        {
            hall=0;
            if(string.IsNullOrEmpty(name) || name.StartsWith("friend-",StringComparison.Ordinal) || name.StartsWith("guild-",StringComparison.Ordinal))return false;
            var parts=name.Split(';');
            return parts.Length==2 && parts[0].Length>0 && int.TryParse(parts[1],out hall) && hall>0;
        }
        public static Dictionary<int,int> Count(IEnumerable<RoomInfo> rooms,IEnumerable<int> halls,bool ready)
        {
            var counts=new Dictionary<int,int>();if(!ready)return counts;
            foreach(int hall in halls??Enumerable.Empty<int>())if(hall>0)counts[hall]=0;
            // Closed/full rooms still contribute; one latest entry per room prevents double counting.
            foreach(var room in rooms.Where(r=>r!=null).GroupBy(r=>r.Name).Select(g=>g.Last())){
                if(room.RemovedFromList || !room.IsVisible || !TryHall(room.Name,out int hall))continue;
                int online=room.CustomProperties[OnlinePlayers] is int value?Math.Max(0,Math.Min(room.PlayerCount,value)):room.PlayerCount;
                counts.TryGetValue(hall,out int previous);counts[hall]=previous+online;
            }
            return counts;
        }
    }
}
