using Assets.Scripts.Prack.BackSys.ChartData;
using Assets.Scripts.Prack.BackSys.GameData;
namespace Assets.Scripts.Exert.Network
{
    public partial class PunNetwork
    {
        static MatchRulesContract GuildWarRules(SkillMatchData hall)
        {
            var rules=MatchRulesContract.FromHall(hall,MatchRulesContract.Pavilion);
            rules.CoinPolicy=1;rules.EntryCoin=rules.MaxCoin=0;return rules;
        }
    }
}
