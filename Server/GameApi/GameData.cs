using System.Text.Json;
using Assets.Scripts.Prack.BackSys;
using Npgsql;

namespace MagicOnline.GameApi;

public sealed record ProfileEdit(string NickName, int AvatarIdx);
public sealed class GameData(Database db)
{
    public static UserMainData ReadUser(NpgsqlDataReader r) => new()
    {
        InDate = r.GetGuid(0).ToString(), NickName = r.GetString(1), LvVel = r.GetInt32(2), LvProc = r.GetInt32(3), AvatarIdx = r.GetInt32(4),
        Coin = r.GetInt64(5), Jem = r.GetInt32(6),
        Ball_4_Win = r.GetInt32(7), Ball_4_Lose = r.GetInt32(8), Ball_4_Draw = r.GetInt32(9),
        Ball_3_Win = r.GetInt32(10), Ball_3_Lose = r.GetInt32(11), Ball_3_Draw = r.GetInt32(12), Win = r.GetInt32(13), Lose = r.GetInt32(14)
    };
    public const string UserSql = "SELECT u.id,u.nickname,u.level,u.level_progress,u.avatar_idx,w.coin,w.jem,s.ball_4_win,s.ball_4_lose,s.ball_4_draw,s.ball_3_win,s.ball_3_lose,s.ball_3_draw,s.legacy_win,s.legacy_lose FROM users u JOIN wallets w ON w.user_id=u.id JOIN user_stats s ON s.user_id=u.id ";
    public async Task<UserMainData> Profile(Guid user)
    {
        await using var cmd = db.Source.CreateCommand(UserSql + "WHERE u.id=$1"); cmd.Parameters.AddWithValue(user);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) throw new ApiFault(404, "UserNotFound");
        return ReadUser(r);
    }
    public async Task EditProfile(Guid user, ProfileEdit edit)
    {
        if (string.IsNullOrWhiteSpace(edit.NickName) || edit.NickName.Trim().Length > 24 || edit.NickName.Any(char.IsControl) || edit.AvatarIdx is < 1 or > 100)
            throw new ApiFault(400, "InvalidProfile");
        await using var cmd = db.Source.CreateCommand("UPDATE users SET nickname=$2,avatar_idx=$3 WHERE id=$1");
        cmd.Parameters.AddWithValue(user); cmd.Parameters.AddWithValue(edit.NickName.Trim()); cmd.Parameters.AddWithValue(edit.AvatarIdx); await cmd.ExecuteNonQueryAsync();
    }
    public async Task<JsonElement> Chart(string name)
    {
        await using var cmd = db.Source.CreateCommand("SELECT rows::text FROM charts WHERE name=$1"); cmd.Parameters.AddWithValue(name);
        var rows = await cmd.ExecuteScalarAsync() as string ?? throw new ApiFault(404, "ChartNotFound");
        return JsonSerializer.Deserialize<JsonElement>(rows);
    }
}
