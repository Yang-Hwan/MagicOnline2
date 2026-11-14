using System;
using System.IO;
using System.Reflection;
using Assets.Scripts.Prack.BackSys.GameData;
using Assets.Scripts.Sight.Surface.Consist;
using UnityEditor;
using UnityEngine;

public static class GuildNotificationValidation
{
    [MenuItem("Tools/Magic Online/Validate Guild Notifications")]
    public static void Run()
    {
        int checks=0;
        var type=typeof(FriendSocial).GetNestedType("Snapshot",BindingFlags.NonPublic);
        var valid=typeof(FriendSocial).GetMethod("ValidGuildInvitation",BindingFlags.Static|BindingFlags.NonPublic);
        GuildInvitation Read(string json)=>(GuildInvitation)type.GetField("GuildInvite").GetValue(JsonUtility.FromJson(json,type));
        bool Valid(GuildInvitation invite)=>(bool)valid.Invoke(null,new object[]{invite});
        void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
        Check(!Valid(Read("{\"From\":null,\"Name\":null,\"Invite\":null,\"GuildInvite\":null}")),"server null invitation never displays a banner");
        Check(!Valid(Read("{\"From\":null,\"Invite\":null}")),"older response with missing field never displays a banner");
        Check(!Valid(Read("{\"GuildInvite\":{}}")),"empty nested object never displays a banner");
        Check(!Valid(new GuildInvitation{Id="bad",GuildId=Guid.NewGuid().ToString()}),"malformed invitation ID rejected");
        Check(!Valid(new GuildInvitation{Id=Guid.Empty.ToString(),GuildId=Guid.NewGuid().ToString()}),"zero invitation ID rejected");
        Check(!Valid(new GuildInvitation{Id=Guid.NewGuid().ToString()}),"missing guild ID rejected");
        string id=Guid.NewGuid().ToString(),guild=Guid.NewGuid().ToString();
        var actual=Read("{\"GuildInvite\":{\"Id\":\""+id+"\",\"GuildId\":\""+guild+"\",\"GuildName\":\"실제 초대\",\"Tag\":\"CUE\",\"InviterName\":\"길드장\"}}");
        Check(Valid(actual) && actual.Id==id && actual.GuildId==guild,"real invitation survives Unity JSON and remains actionable");
        var normalize=typeof(BottomMenuView).GetMethod("NormalizeGuildState",BindingFlags.Static|BindingFlags.NonPublic);
        var emptyState=JsonUtility.FromJson<GuildState>("{\"Guild\":null,\"Invitations\":[],\"Applications\":[]}");
        var normalized=(GuildState)normalize.Invoke(null,new object[]{emptyState});
        Check(normalized.Guild==null,"guildless server response opens search instead of an empty guild home");
        var member=JsonUtility.FromJson<GuildState>("{\"Guild\":{\"Id\":\""+guild+"\",\"MyRole\":\"Member\"},\"Invitations\":[],\"Applications\":[]}");
        Check(((GuildState)normalize.Invoke(null,new object[]{member})).Guild.Id==guild,"actual membership remains intact");
        Directory.CreateDirectory("Logs/Guilds");File.WriteAllText("Logs/Guilds/notifications.json","{\"Ok\":true,\"Checks\":"+checks+"}");
        Debug.Log("[GuildNotificationValidation] PASS "+checks+" checks");
    }
}
