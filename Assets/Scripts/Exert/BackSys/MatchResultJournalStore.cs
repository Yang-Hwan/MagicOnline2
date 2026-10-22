using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Journal = Assets.Scripts.Exert.BackSys.BackendMatchHistory.Journal;
using PendingWrite = Assets.Scripts.Exert.BackSys.BackendMatchHistory.PendingWrite;

namespace Assets.Scripts.Exert.BackSys
{
    public static class MatchResultJournalStore
    {
        // No initializer: a JSON object missing Writes must not become an empty queue.
        [Serializable] sealed class Payload
        {
            public int Version;
            public long Generation;
            public List<PendingWrite> Writes;
            public string Checksum;
        }
        [Serializable] sealed class Content
        { public int Version; public long Generation; public List<PendingWrite> Writes; }

        static string Digest(Journal journal)
        {
            var json = JsonUtility.ToJson(new Content { Version = journal.Version, Generation = journal.Generation, Writes = journal.Writes });
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "");
        }

        static Journal Read(string path)
        {
            var value = JsonUtility.FromJson<Payload>(File.ReadAllText(path));
            if (value == null) throw new InvalidDataException("Empty result journal.");
            var journal = new Journal { Version = value.Version, Generation = value.Generation, Writes = value.Writes, Checksum = value.Checksum };
            Validate(journal);
            if (journal.Version == 0)
            {
                if (journal.Generation != 0 || !string.IsNullOrEmpty(journal.Checksum))
                    throw new InvalidDataException("Invalid legacy result journal.");
            }
            else if (!string.Equals(journal.Checksum, Digest(journal), StringComparison.Ordinal))
                throw new InvalidDataException("Result journal checksum mismatch.");
            return journal;
        }

        static void Validate(Journal journal)
        {
            if (journal != null && journal.Version > 1)
                throw new NotSupportedException("A newer client is required to read this result journal.");
            if (journal == null || journal.Writes == null || journal.Generation < 0 || journal.Version < 0)
                throw new InvalidDataException("Invalid result journal envelope.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var write in journal.Writes)
            {
                if (write == null || !Guid.TryParseExact(write.MatchId, "N", out _) || !ids.Add(write.MatchId))
                    throw new InvalidDataException("Invalid or duplicate match ID in result journal.");
                if (write.Surrender) continue;
                var report = write.Report;
                if (report == null || report.ScoreA < 0 || report.ScoreB < 0 || report.ScoreA > 100000 || report.ScoreB > 100000 ||
                    report.WinnerSeat != (report.ScoreA == report.ScoreB ? -1 : report.ScoreA > report.ScoreB ? 0 : 1))
                    throw new InvalidDataException("Invalid report in result journal.");
            }
        }

        public static Journal Load(string path, out string recovery)
        {
            recovery = "";
            Journal newest = null;
            string selected = null;
            var corrupt = new List<string>();
            // Generation, not timestamp, prevents an old backup resurrecting cleared requests.
            foreach (var candidate in new[] { path, path + ".tmp", path + ".bak", path + ".bak.tmp" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    var journal = Read(candidate);
                    if (newest == null || journal.Generation > newest.Generation)
                    { newest = journal; selected = candidate; }
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidDataException || e is FormatException)
                { corrupt.Add(candidate); }
                // Access/locking errors are retried; never label an unreadable file as corrupt.
            }
            foreach (var candidate in corrupt)
                File.Move(candidate, candidate + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
            if (newest == null)
            {
                newest = new Journal();
                if (corrupt.Count == 0) return newest;
                recovery = "결과 재전송 파일이 손상되어 별도 보관했습니다. 복구 가능한 백업이 없어 이전 결과는 확인이 필요합니다.";
            }
            else if (selected != path || corrupt.Count > 0)
                recovery = "결과 재전송 파일을 정상 저장본에서 복구했습니다.";
            if (!string.IsNullOrEmpty(recovery)) Save(path, newest);
            return newest;
        }

        public static void Save(string path, Journal journal)
        {
            Validate(journal);
            journal.Version = 1;
            journal.Generation = checked(journal.Generation + 1);
            journal.Checksum = Digest(journal);
            var json = JsonUtility.ToJson(journal);
            WriteDurable(path + ".tmp", json);
            Publish(path + ".tmp", path);
            // The backup contains the latest committed queue, including newly queued results.
            WriteDurable(path + ".bak.tmp", json);
            Publish(path + ".bak.tmp", path + ".bak");
        }

        static void WriteDurable(string path, string json)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                { writer.Write(json); writer.Flush(); }
                stream.Flush(true);
            }
        }

        static void Publish(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
    }
}
