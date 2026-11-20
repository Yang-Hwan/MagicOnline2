using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Assets.Scripts.Exert.BackSys;
using Assets.Scripts.Exert.Network;
using Assets.Scripts.Prack.BackSys.ChartData;
using ExitGames.Client.Photon;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ArenaPopulationValidation
{
    sealed class Info:RoomInfo
    {
        public Info(string name,int players,bool open=true,bool visible=true,int? online=null,bool removed=false):base(name,new Hashtable{{GamePropertyKey.PlayerCount,(byte)players},{GamePropertyKey.MaxPlayers,(byte)2},{GamePropertyKey.IsOpen,open},{GamePropertyKey.IsVisible,visible}})
        {if(online.HasValue)CustomProperties[ArenaPopulation.OnlinePlayers]=online.Value;RemovedFromList=removed;}
    }
    static ArenaPopulationValidation(){EditorApplication.update+=()=>{if(!File.Exists("Logs/ArenaPopulation/run.request") || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Logs/ArenaPopulation/run.request");Run();};}
    public static void Run()
    {
        int checks=0;GameObject root=null;var savedHalls=BackendChart.skillMatchData;Directory.CreateDirectory("Logs/ArenaPopulation");
        void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        try{
            var waiting=new Info("waiting;1",1);var playing=new Info("playing;1",2,false,true,2);
            var counts=ArenaPopulation.Count(new[]{waiting,playing},new[]{1,2},true);
            Check(counts[1]==3,"one waiting player plus two playing players equals three");
            Check(counts[2]==0,"empty known arena reports zero");
            Check(!playing.IsOpen && playing.IsVisible,"counting a match does not make it joinable");
            Check(ArenaPopulation.Count(new[]{waiting,playing},new[]{1},false).Count==0,"unreceived lobby snapshot is unknown, not a false zero");
            Check(ArenaPopulation.Count(new[]{playing,new Info("playing;1",1,false,true,1)},new[]{1},true)[1]==1,"latest update for a room replaces earlier count");
            Check(ArenaPopulation.Count(new[]{playing,new Info("playing;1",0,false,true,0,true)},new[]{1},true)[1]==0,"removed room contributes no players");
            Check(ArenaPopulation.Count(new[]{new Info("held-slot;1",2,false,true,1)},new[]{1},true)[1]==1,"inactive reserved slot excluded by published online count");
            Check(ArenaPopulation.Count(new[]{new Info("reconnected;1",2,false,true,2)},new[]{1},true)[1]==2,"reconnected active player returns to count");
            Check(ArenaPopulation.Count(new[]{new Info("old-client;1",2,false)},new[]{1},true)[1]==2,"older visible rooms retain PlayerCount fallback");
            Check(ArenaPopulation.Count(new[]{new Info("friend-hidden;1",2,true,true),new Info("guild-private;1",2,true,true)},new[]{1},true)[1]==0,"private friend and guild rooms never leak into public arena count");
            Check(ArenaPopulation.Count(new[]{new Info("hidden;1",2,false,false)},new[]{1},true)[1]==0,"hidden rooms excluded");
            Check(ArenaPopulation.Count(new[]{new Info("bad",2),new Info("bad;abc",2),new Info("bad;0",2),new Info(";1",2)},new[]{1},true)[1]==0,"malformed room names ignored");
            Check(ArenaPopulation.Count(new[]{new Info("low;1",2,false,true,-1)},new[]{1},true)[1]==0,"negative published online count clamped");
            Check(ArenaPopulation.Count(new[]{new Info("high;1",2,false,true,9)},new[]{1},true)[1]==2,"published online count cannot exceed room population");
            var separated=ArenaPopulation.Count(new[]{waiting,playing,new Info("other;2",2,false)},new[]{1,2},true);Check(separated[1]==3 && separated[2]==2,"arena totals remain separate");
            BackendChart.skillMatchData=new List<SkillMatchData>{new SkillMatchData{HallIdx=1},new SkillMatchData{HallIdx=2}};
            root=new GameObject("Offline room callback fixture");root.SetActive(false);var network=root.AddComponent<PunNetwork>();
            Dictionary<int,int> published=null;network.OnNetworkRoomUpdate+=data=>published=new Dictionary<int,int>(data);
            network.GetRoomsInfo();Check(published.Count==0,"network initially publishes unknown counts");
            network.OnRoomListUpdate(new List<RoomInfo>{waiting,playing});Check(published[1]==3 && published[2]==0,"real room-list callback publishes waiting plus playing totals");
            network.OnRoomListUpdate(new List<RoomInfo>{new Info("waiting;1",2,true,true,2)});Check(published[1]==4,"incremental lobby callback does not double-count a room");
            network.OnRoomListUpdate(new List<RoomInfo>{new Info("playing;1",2,false,true,1)});Check(published[1]==3,"disconnect callback metadata reduces live population");
            network.OnRoomListUpdate(new List<RoomInfo>{new Info("playing;1",0,false,true,0,true)});Check(published[1]==2,"room removal clears completed match population");
            typeof(PunNetwork).GetMethod("ClearArenaPopulation",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(network,null);Check(published.Count==0,"connection reset clears stale room counts");
            network.OnRoomListUpdate(new List<RoomInfo>());Check(published[1]==0 && published[2]==0,"empty reconnect snapshot resets arenas to zero");
            File.WriteAllText("Logs/ArenaPopulation/result.json","{\"passed\":true,\"checks\":"+checks+",\"photonNetworkNotContacted\":true}");
        }catch(Exception e){File.WriteAllText("Logs/ArenaPopulation/failure.txt",e.ToString());Debug.LogException(e);}
        finally{BackendChart.skillMatchData=savedHalls;if(root)UnityEngine.Object.DestroyImmediate(root);}
    }
}
