using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        bool roomPopulationReady;
        float nextPopulationPublish;
        void PublishArenaPopulation()
        {
            if(!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || IsFriendly || IsGuildWar || !ArenaPopulation.TryHall(PhotonNetwork.CurrentRoom.Name,out _))return;
            var room=PhotonNetwork.CurrentRoom;
            // Visibility is for population only. IsOpen remains false once a match starts.
            if(!room.IsVisible)room.IsVisible=true;
            int online=room.Players.Values.Count(p=>!p.IsInactive);
            if(room.CustomProperties[ArenaPopulation.OnlinePlayers] is int previous && previous==online)return;
            room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable{{ArenaPopulation.OnlinePlayers,online}});
        }
        void TickArenaPopulation()
        {
            if(Time.realtimeSinceStartup<nextPopulationPublish)return;
            nextPopulationPublish=Time.realtimeSinceStartup+2;
            PublishArenaPopulation();
        }
        void ClearArenaPopulation()
        {
            roomPopulationReady=false;roomsLocal.Clear();rooms.Clear();skillRoom.Clear();
            CallRoomUpdate(new System.Collections.Generic.Dictionary<int,int>());
        }
    }
}
