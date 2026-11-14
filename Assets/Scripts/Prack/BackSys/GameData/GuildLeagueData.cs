#nullable disable
using System;
namespace Assets.Scripts.Prack.BackSys.GameData
{
    [Serializable] public sealed class GuildLeagueState
    {
        public int Season,Week,DaysLeft,TodayGames,DailyLimit=5,MyGames,MyPoints,MyRank;
        public long StartsAt,EndsAt;
        public GuildLeagueRank Own;
        public GuildLeagueRank[] Rankings=Array.Empty<GuildLeagueRank>();
        public GuildLeagueContribution[] Contributions=Array.Empty<GuildLeagueContribution>();
        public GuildLeagueResult[] Recent=Array.Empty<GuildLeagueResult>();
        public GuildLeagueReward[] Rewards=Array.Empty<GuildLeagueReward>();
    }
    [Serializable] public sealed class GuildLeagueRank{public string GuildId,Name,Tag,Tier;public int Rank,Points,Wins,Losses,Draws;}
    [Serializable] public sealed class GuildLeagueContribution{public string UserId,Name;public int Rank,Games,Points;}
    [Serializable] public sealed class GuildLeagueResult{public string MatchId,Opponent,Result;public int Delta;}
    [Serializable] public sealed class GuildLeagueReward{public int Season,Gem;public string Tier,Emblem;public long Coin;public bool Claimed;}
    [Serializable] public sealed class GuildWarTicket{public string MatchId,OpponentUserId,State;public int HallId;}
    [Serializable] public sealed class GuildWarQueueInput{public int HallId;}
}
