using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Npgsql;

namespace MagicOnline.GameApi;

public sealed class Database(NpgsqlDataSource source)
{
    public NpgsqlDataSource Source => source;
    public static readonly JsonSerializerOptions Json = new() { IncludeFields = true, PropertyNameCaseInsensitive = true };

    public static NpgsqlCommand Command(NpgsqlConnection connection, string sql, params object[] values)
    {
        var cmd = new NpgsqlCommand(sql, connection);
        foreach (var value in values) cmd.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
        return cmd;
    }

    public async Task Migrate()
    {
        await using var c = await source.OpenConnectionAsync();
        await using var tx = await c.BeginTransactionAsync();
        // Serialize migration instances and verify previously applied scripts are immutable.
        await using (var cmd = Command(c, "SELECT pg_advisory_xact_lock(687451003); CREATE TABLE IF NOT EXISTS schema_migrations(name text PRIMARY KEY, checksum text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now());"))
            await cmd.ExecuteNonQueryAsync();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Migrations"), "*.sql").Order())
        {
            string sql = await File.ReadAllTextAsync(file), name = Path.GetFileName(file);
            string checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            await using var lookup = Command(c, "SELECT checksum FROM schema_migrations WHERE name=$1", name);
            var previous = await lookup.ExecuteScalarAsync();
            if (previous != null)
            {
                if ((string)previous != checksum) throw new InvalidOperationException($"Applied migration changed: {name}");
                continue;
            }
            await using (var apply = Command(c, sql)) await apply.ExecuteNonQueryAsync();
            await using var record = Command(c, "INSERT INTO schema_migrations(name,checksum) VALUES($1,$2)", name, checksum);
            await record.ExecuteNonQueryAsync();
        }
        foreach (var name in new[] { "CommonCode", "SkillMatch", "Attendance" })
        {
            using var csv = new TextFieldParser(Path.Combine(AppContext.BaseDirectory, "Seed", name + ".csv"));
            csv.SetDelimiters(","); csv.HasFieldsEnclosedInQuotes = true;
            var header = csv.ReadFields() ?? throw new InvalidDataException(name);
            var rows = new List<Dictionary<string, object>>();
            var keys = new HashSet<string>();
            while (!csv.EndOfData)
            {
                var values = csv.ReadFields()!;
                if (values.Length != header.Length) throw new InvalidDataException(name + ": invalid CSV columns");
                var row = new Dictionary<string, object>();
                for (int i = 0; i < header.Length; i++)
                    row[header[i]] = long.TryParse(values[i], out long n) ? n : values[i];
                string key = name == "SkillMatch" ? values[0] : name == "Attendance" ? values[0] : values[0] + "/" + values[1];
                if (!keys.Add(key)) throw new InvalidDataException(name + ": duplicate key");
                rows.Add(row);
            }
            if (rows.Count == 0) throw new InvalidDataException(name + ": empty seed");
            // Initial seed only. Never overwrite an administrator's later chart changes.
            await using var seed = Command(c, "INSERT INTO charts(name,rows) VALUES($1,$2::jsonb) ON CONFLICT DO NOTHING", name, JsonSerializer.Serialize(rows));
            await seed.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
}
