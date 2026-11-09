using System.Text.Json;
using Assets.Scripts.Prack.BackSys;

namespace MagicOnline.GameApi;

public static class LevelProgression
{
    public static int? ReadLimit(string? json)
    {
        if (json == null) return null;
        using var doc = JsonDocument.Parse(json);
        static string Text(JsonElement row, string key) => row.TryGetProperty(key, out var v) ? v.ToString().Trim() : "";
        var rows = doc.RootElement.EnumerateArray().Where(r => Text(r, "ComCd") == "LvPlayMax" &&
            Text(r, "GrpCd") != "#" && Text(r, "IsUse") == "Y").ToArray();
        return rows.Length == 1 && int.TryParse(Text(rows[0], "ComVal"), out int limit) && limit > 0 ? limit : null;
    }

    public static void Advance(UserMainData user, int limit)
    {
        if (limit <= 0) return;
        if ((long)user.LvProc + 1 >= limit)
        {
            if (user.LvVel < int.MaxValue) user.LvVel++;
            user.LvProc = 0;
        }
        else user.LvProc++;
    }
}
