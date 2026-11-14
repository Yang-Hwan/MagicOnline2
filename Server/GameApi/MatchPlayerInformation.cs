using System.Security.Claims;
using Assets.Scripts.Prack.BackSys.GameData;

namespace MagicOnline.GameApi;

public sealed class MatchPlayerInformation(Database db, MatchEquipment equipment, TimeProvider clock)
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/matches/{id}/players/{seat:int}", (string id, int seat, ClaimsPrincipal user, MatchPlayerInformation service) =>
            service.Read(id, seat, Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!))).RequireAuthorization();
        app.MapGet("/api/guilds/{guild:guid}/members/{target:guid}/profile",(Guid guild,Guid target,ClaimsPrincipal user,MatchPlayerInformation service)=>
            service.ReadGuild(guild,target,Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!))).RequireAuthorization();
    }

    public async Task<MatchPlayerInfo> Read(string id, int seat, Guid caller)
    {
        if (seat is < 0 or > 1) throw new ApiFault(400, "InvalidSeat");
        var snapshot = await equipment.Read(id, caller); // Never expose arbitrary users or their private inventories.
        var gear = snapshot.Players[seat];
        return await Profile(gear);
    }
    public async Task<MatchPlayerInfo> ReadGuild(Guid guild,Guid target,Guid caller)
    {
        await using(var q=db.Source.CreateCommand("SELECT 1 FROM guild_members a JOIN guild_members b ON b.guild_id=a.guild_id JOIN guilds g ON g.id=a.guild_id WHERE a.user_id=$1 AND b.user_id=$2 AND g.id=$3 AND g.deleted_at IS NULL"))
        {
            q.Parameters.AddWithValue(caller);q.Parameters.AddWithValue(target);q.Parameters.AddWithValue(guild);
            if(await q.ExecuteScalarAsync()==null)throw new ApiFault(403,"같은 길드원만 선수정보를 조회할 수 있습니다.");
        }
        return await Profile(await equipment.CurrentProfile(target));
    }
    async Task<MatchPlayerInfo> Profile(MatchEquipmentProfile gear)
    {
        var today = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(9)).Date;
        var start = new DateTimeOffset(today, TimeSpan.FromHours(9)).ToUniversalTime();
        await using var cmd = db.Source.CreateCommand("""
            WITH outcomes AS (
                SELECT r.match_id,r.settled_at,
                    CASE WHEN m.user_a=$1 THEN r.data->>'ResultA' ELSE r.data->>'ResultB' END AS result,
                    COALESCE((CASE WHEN m.user_a=$1 THEN r.data->>'RewardA' ELSE r.data->>'RewardB' END)::numeric,0) AS reward
                FROM settlement_receipts r JOIN matches m ON m.id=r.match_id WHERE m.user_a=$1 OR m.user_b=$1
            ), counted AS (SELECT * FROM outcomes WHERE result IN ('Win','Lose','Draw')),
            today AS (
                SELECT result,row_number() OVER(ORDER BY settled_at DESC,match_id DESC) AS n
                FROM counted WHERE settled_at >= $2 AND settled_at < $3
            )
            SELECT u.nickname,u.level,s.ball_4_win,s.ball_4_lose,s.ball_4_draw,s.ball_3_win,s.ball_3_lose,s.ball_3_draw,s.legacy_win,s.legacy_lose,
                (SELECT LEAST(COALESCE(sum(GREATEST(reward,0)),0),9223372036854775807)::bigint FROM counted WHERE result='Win'),
                (SELECT count(*) FROM today WHERE result='Win' AND n < COALESCE((SELECT min(n) FROM today WHERE result<>'Win'),9223372036854775807)),
                COALESCE(p.name,'기본 큐대'),COALESCE(p.description,''),COALESCE(p.thumbnail,'')
            FROM users u JOIN user_stats s ON s.user_id=u.id LEFT JOIN shop_products p ON p.id=$4 AND p.kind='Cue'
            WHERE u.id=$1 AND u.deleted_at IS NULL
            """);
        cmd.Parameters.AddWithValue(Guid.Parse(gear.UserId)); cmd.Parameters.AddWithValue(start);
        cmd.Parameters.AddWithValue(start.AddDays(1)); cmd.Parameters.AddWithValue(gear.CueId);
        await using var row = await cmd.ExecuteReaderAsync();
        if (!await row.ReadAsync()) throw new ApiFault(404, "PlayerNotFound");
        var four = new PlayerRecord { Wins=row.GetInt32(2), Losses=row.GetInt32(3), Draws=row.GetInt32(4) };
        var three = new PlayerRecord { Wins=row.GetInt32(5), Losses=row.GetInt32(6), Draws=row.GetInt32(7) };
        return new MatchPlayerInfo {
            UserId=gear.UserId, Nickname=row.GetString(0), Level=row.GetInt32(1), FourBall=four, ThreeBall=three,
            Total=new PlayerRecord { Wins=four.Wins+three.Wins+row.GetInt32(8), Losses=four.Losses+three.Losses+row.GetInt32(9), Draws=four.Draws+three.Draws },
            TotalPrize=row.GetInt64(10), TodayStreak=checked((int)row.GetInt64(11)),
            CueName=row.GetString(12), CueDescription=row.GetString(13), CueThumbnail=row.GetString(14), Equipment=gear
        };
    }
}
