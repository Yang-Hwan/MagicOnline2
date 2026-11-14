#nullable disable
using System;
namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable] public sealed class GuildInfo
    {
        public string Id,Name,Tag,Description,Notice,JoinMode,MyRole,LeaderName;
        public int MemberCount,OnlineCount,Revision;
        public bool ApplicationPending;
    }
    [Serializable] public sealed class GuildState
    {
        public GuildInfo Guild;
        public GuildInvitation[] Invitations=Array.Empty<GuildInvitation>();
        public GuildApplication[] Applications=Array.Empty<GuildApplication>();
    }
    [Serializable] public sealed class GuildInvitation { public string Id,GuildId,GuildName,Tag,InviterName; }
    [Serializable] public sealed class GuildApplication { public string UserId,Name; public int Level; }
    [Serializable] public sealed class GuildSearch { public GuildInfo[] Rows=Array.Empty<GuildInfo>(); public bool HasMore; }
    [Serializable] public sealed class GuildMember { public string Id,Name,Role,Status,Portrait; public int Level,Avatar; }
    [Serializable] public sealed class GuildMembers { public GuildMember[] Rows=Array.Empty<GuildMember>(); }
    [Serializable] public sealed class GuildMessage { public long Id,SentAt; public string UserId,Name,Text,Role; }
    [Serializable] public sealed class GuildChat { public GuildMessage[] Rows=Array.Empty<GuildMessage>(); }
    [Serializable] public sealed class GuildReply { public string Message,GuildId; public bool Joined; }
    [Serializable] public sealed class GuildCreate { public string Name,Tag,Description,JoinMode,RequestId; }
    [Serializable] public sealed class GuildEdit { public string Description,Notice,JoinMode; public int Revision; }
    [Serializable] public sealed class GuildChoice { public bool Accept; }
    [Serializable] public sealed class GuildInviteInput { public string UserId; }
    [Serializable] public sealed class GuildRoleInput { public string Role; }
    [Serializable] public sealed class GuildMessageInput { public string RequestId,Text; }
}
