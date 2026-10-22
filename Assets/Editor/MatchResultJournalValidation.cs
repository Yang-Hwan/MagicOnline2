#if UNITY_EDITOR
using System;
using System.IO;
using Assets.Scripts.Exert.BackSys;
using UnityEditor;
using UnityEngine;
using Journal = Assets.Scripts.Exert.BackSys.BackendMatchHistory.Journal;
using PendingWrite = Assets.Scripts.Exert.BackSys.BackendMatchHistory.PendingWrite;
using ResultBody = Assets.Scripts.Exert.BackSys.BackendMatchHistory.ResultBody;

public static class MatchResultJournalValidation
{
    static void Require(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("Journal validation failed: " + name); }
    static Journal Queued() => new Journal { Writes = { new PendingWrite {
        MatchId = "0123456789abcdef0123456789abcdef", Report = new ResultBody { ScoreA = 2, ScoreB = 0, WinnerSeat = 0 }
    } } };

    [MenuItem("Tools/Local Game API/Validate result journal recovery")]
    public static void Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "MagicOnline2-Journal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int passed = 0;
        void Check(string name, Action<string> action)
        {
            string folder = Path.Combine(directory, name); Directory.CreateDirectory(folder);
            action(Path.Combine(folder, "results.json")); passed++;
        }
        try
        {
            Check("legacy", path => {
                File.WriteAllText(path, "{\"Writes\":[{\"MatchId\":\"0123456789abcdef0123456789abcdef\",\"Surrender\":true}]}");
                var value = MatchResultJournalStore.Load(path, out _);
                Require(value.Writes.Count == 1 && value.Writes[0].Surrender, "legacy queue");
            });
            Check("backup", path => {
                MatchResultJournalStore.Save(path, Queued());
                Require(File.ReadAllText(path) == File.ReadAllText(path + ".bak"), "latest backup");
                File.WriteAllText(path, "{broken");
                var value = MatchResultJournalStore.Load(path, out var recovery);
                Require(value.Writes.Count == 1 && value.Writes[0].Report.ScoreA == 2 && recovery.Length > 0, "backup report recovery");
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.corrupt-*").Length == 1, "quarantined primary");
            });
            Check("interrupted", path => {
                var value = Queued(); MatchResultJournalStore.Save(path, value);
                value.Writes.Add(new PendingWrite { MatchId = Guid.NewGuid().ToString("N"), Surrender = true });
                MatchResultJournalStore.Save(path + ".next", value);
                File.Copy(path + ".next", path + ".tmp");
                Require(MatchResultJournalStore.Load(path, out _).Writes.Count == 2, "newer temporary commit");
            });
            Check("cleared", path => {
                var value = Queued(); MatchResultJournalStore.Save(path, value);
                string stale = File.ReadAllText(path);
                value.Writes.Clear(); MatchResultJournalStore.Save(path, value);
                File.WriteAllText(path + ".bak", stale);
                Require(MatchResultJournalStore.Load(path, out _).Writes.Count == 0, "do not resurrect old result");
            });
            Check("all-corrupt", path => {
                foreach (var file in new[] { path, path + ".bak", path + ".tmp", path + ".bak.tmp" }) File.WriteAllText(file, "{");
                var value = MatchResultJournalStore.Load(path, out var recovery);
                Require(value.Writes.Count == 0 && recovery.Contains("확인"), "total corruption requires review");
                Require(Directory.GetFiles(Path.GetDirectoryName(path), "*.corrupt-*").Length == 4, "preserve all corrupt copies");
            });
            Check("invalid-id", path => {
                MatchResultJournalStore.Save(path, Queued());
                var bad = Queued(); bad.Writes[0].MatchId = "invalid"; File.WriteAllText(path, JsonUtility.ToJson(bad));
                Require(MatchResultJournalStore.Load(path, out _).Writes[0].MatchId.Length == 32, "invalid ID fallback");
            });
            Check("missing-list", path => {
                MatchResultJournalStore.Save(path, Queued()); File.WriteAllText(path, "{}");
                Require(MatchResultJournalStore.Load(path, out _).Writes.Count == 1, "missing list is not empty queue");
            });
            Check("invalid-report", path => {
                MatchResultJournalStore.Save(path, Queued());
                var bad = Queued(); bad.Writes[0].Report.WinnerSeat = 1; File.WriteAllText(path, JsonUtility.ToJson(bad));
                Require(MatchResultJournalStore.Load(path, out _).Writes[0].Report.WinnerSeat == 0, "score consistency fallback");
            });
            Check("backup-only", path => {
                MatchResultJournalStore.Save(path, Queued()); File.Delete(path);
                Require(MatchResultJournalStore.Load(path, out _).Writes.Count == 1 && File.Exists(path), "recreate primary");
            });
            Check("locked", path => {
                MatchResultJournalStore.Save(path, Queued()); bool locked = false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                { try { MatchResultJournalStore.Load(path, out _); } catch (IOException) { locked = true; } }
                Require(locked && Directory.GetFiles(Path.GetDirectoryName(path), "*.corrupt-*").Length == 0, "retry IO without quarantine");
            });
            Check("future-version", path => {
                File.WriteAllText(path, "{\"Version\":2,\"Generation\":1,\"Writes\":[]}"); bool rejected = false;
                try { MatchResultJournalStore.Load(path, out _); } catch (NotSupportedException) { rejected = true; }
                Require(rejected && File.Exists(path), "preserve future schema");
            });
            Check("checksum", path => {
                MatchResultJournalStore.Save(path, Queued());
                File.WriteAllText(path, File.ReadAllText(path).Replace("\"ScoreA\":2", "\"ScoreA\":3"));
                Require(MatchResultJournalStore.Load(path, out _).Writes[0].Report.ScoreA == 2, "detect syntactically valid damage");
            });
            Directory.CreateDirectory("UserSettings");
            File.WriteAllText("UserSettings/MatchResultJournalValidation.json", "{\"Ok\":true,\"Checks\":" + passed + "}");
            Debug.Log("[MatchResultJournalValidation] PASS: " + passed + " recovery checks.");
        }
        finally { Directory.Delete(directory, true); }
    }
}
#endif
