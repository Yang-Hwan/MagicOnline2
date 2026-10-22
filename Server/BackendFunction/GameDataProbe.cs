using BackEnd;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace BackendFunction;

// Dedicated diagnostic tables only. No wallet/stat/history table can be selected.
public static class GameDataProbe
{
    // Row identifiers are opaque strings. Automatic date conversion loses their exact form.
    static JObject ParseStorageReply(string json)
    {
        using var text = new StringReader(json);
        using var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None };
        return JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
    }
    public static string ReadStorageRowId(string json, int index) => Id(ParseStorageReply(json)["rows"][index]);
    public static string ReadSingleStorageRowId(string json) => Id(SingleRow(ParseStorageReply(json)));
    public static string ReadProbePeerId(string json) => ParseStorageReply(json).Value<string>("OtherUserId");
    public static bool IsProbeRequest(string payload)
    {
        if (string.IsNullOrEmpty(payload) || payload.Length > 4096) return false;
        try { return JObject.Parse(payload ?? "")["Action"]?.Value<string>()?.StartsWith("Probe", StringComparison.Ordinal) == true; }
        catch { return false; }
    }

    static string Error(string code) => new JObject { ["Ok"] = false, ["Code"] = code, ["SettlementEnabled"] = false }.ToString();
    static JToken Value(JToken row, string key)
    {
        var value = row[key];
        return value is JObject wrapped ? wrapped["S"] ?? wrapped["N"] ?? value : value;
    }
    static long Number(JToken row, string key) => long.Parse(Value(row, key).ToString(), System.Globalization.CultureInfo.InvariantCulture);
    static string Id(JToken row) => Value(row, "inDate").ToString();
    static JObject Reply(string action, BackendReturnObject response) => new() {
        ["Action"] = action, ["Ok"] = response.IsSuccess(), ["Status"] = response.GetStatusCode(),
        ["SettlementEnabled"] = false
    };
    static JToken Row(BackendReturnObject response)
    {
        if (!response.IsSuccess()) throw new InvalidOperationException("ReadFailed");
        return SingleRow(ParseStorageReply(response.GetReturnValue()));
    }
    static JToken SingleRow(JObject reply)
    {
        if (reply["row"] is JObject single && single.Count > 0) return single;
        var rows = reply["rows"] as JArray;
        if (rows == null || rows.Count != 1) throw new InvalidOperationException("ExpectedExactlyOneRow");
        return rows[0];
    }
    static JToken Own(string table, string run, string slot)
    {
        var where = new Where(); where.Equal("RunId", run); where.Equal("Slot", slot);
        return Row(Backend.GameData.GetMyData(table, where));
    }
    static Param Change(long counter, long version, string requestId) => new() {
        { "Counter", counter }, { "Version", version }, { "LastRequestId", requestId }
    };
    static Where VersionCondition(string owner, string run, string slot, JToken row, long expected)
    {
        var where = new Where(); where.Equal("owner_inDate", owner);
        where.Equal("RunId", run); where.Equal("Slot", slot); where.Equal("Version", expected);
        return where;
    }
    static JObject Snapshot(JToken row) => new() { ["Counter"] = Number(row, "Counter"), ["Version"] = Number(row, "Version") };
    static string AtomicPairSuite(string table, string run, string caller, string other, string otherRow, JToken own, JToken peer)
    {
        var state = Own(table, run, "Audit");
        long av = Number(own, "Version"), bv = Number(peer, "Version"), sv = Number(state, "Version");
        string requestId = Guid.NewGuid().ToString("N");
        JObject Current() => new() {
            ["Self"] = Snapshot(Own(table, run, "Primary")),
            ["Peer"] = Snapshot(Row(Backend.GameData.GetV2(table, otherRow, other))),
            ["MatchState"] = Snapshot(Own(table, run, "Audit"))
        };
        BackendReturnObject Commit(long peerVersion, long stateVersion) => Backend.GameData.TransactionWriteV2(new[] {
            TransactionValue.SetUpdate(table, VersionCondition(caller, run, "Primary", own, av), Change(Number(own, "Counter") + 1, av + 1, requestId)),
            TransactionValue.SetUpdate(table, VersionCondition(other, run, "Primary", peer, peerVersion), Change(Number(peer, "Counter") + 1, bv + 1, requestId)),
            TransactionValue.SetUpdate(table, VersionCondition(caller, run, "Audit", state, stateVersion), Change(Number(state, "Counter") + 1, sv + 1, requestId))
        });
        var before = Current();
        // Preserve every value while isolating whether Where-based writes can target a peer.
        Param Unchanged(JToken row) => Change(Number(row, "Counter"), Number(row, "Version"), Value(row, "LastRequestId").ToString());
        var ownControl = Backend.GameData.TransactionWriteV2(new[] {
            TransactionValue.SetUpdate(table, VersionCondition(caller, run, "Primary", own, av), Unchanged(own)),
            TransactionValue.SetUpdate(table, VersionCondition(caller, run, "Audit", state, sv), Unchanged(state))
        });
        var peerControl = Backend.GameData.TransactionWriteV2(new[] {
            TransactionValue.SetUpdate(table, VersionCondition(other, run, "Primary", peer, bv), Unchanged(peer))
        });
        var badPeer = Commit(bv + 1, sv); var afterBadPeer = Current();
        var badState = Commit(bv, sv + 1); var afterBadState = Current();
        if (badPeer.IsSuccess() || badState.IsSuccess() || !JToken.DeepEquals(before, afterBadPeer) || !JToken.DeepEquals(before, afterBadState))
            return new JObject { ["Ok"] = false, ["Code"] = "AtomicConditionRollbackFailed", ["Before"] = before,
                ["AfterBadPeer"] = afterBadPeer, ["AfterBadState"] = afterBadState, ["SettlementEnabled"] = false }.ToString();
        var success = Commit(bv, sv); var afterSuccess = Current();
        var stale = Commit(bv, sv); var afterStale = Current();
        bool advanced = Number(afterSuccess["Self"], "Counter") == Number(own, "Counter") + 1 &&
            Number(afterSuccess["Peer"], "Counter") == Number(peer, "Counter") + 1 &&
            Number(afterSuccess["MatchState"], "Counter") == Number(state, "Counter") + 1 &&
            Number(afterSuccess["Self"], "Version") == av + 1 && Number(afterSuccess["Peer"], "Version") == bv + 1 && Number(afterSuccess["MatchState"], "Version") == sv + 1;
        return new JObject { ["Ok"] = success.IsSuccess() && advanced && !stale.IsSuccess() && JToken.DeepEquals(afterSuccess, afterStale),
            ["Code"] = "AtomicPairSuite", ["BadPeerStatus"] = badPeer.GetStatusCode(), ["BadStateStatus"] = badState.GetStatusCode(),
            ["OwnConditionControlStatus"] = ownControl.GetStatusCode(), ["PeerConditionControlStatus"] = peerControl.GetStatusCode(),
            ["CommitStatus"] = success.GetStatusCode(), ["CommitError"] = success.GetErrorCode(), ["CommitMessage"] = success.GetMessage(), ["StaleStatus"] = stale.GetStatusCode(), ["Before"] = before,
            ["AfterSuccess"] = afterSuccess, ["AfterStale"] = afterStale, ["SettlementEnabled"] = false }.ToString();
    }

    public static string Handle(string caller, string payload)
    {
        if (!ProbeSettings.Enabled) return Error("ProbeDisabled");
        if (!ProbeSettings.AllowedUserInDates.Contains(caller) || string.IsNullOrWhiteSpace(caller)) return Error("ProbeCallerDenied");
        try
        {
            if (payload == null || payload.Length > 4096) return Error("InvalidProbeRequest");
            var request = ParseStorageReply(payload);
            string[] fields = { "Action", "RunId", "Visibility", "OtherUserId", "OtherRowId", "ExpectedVersion", "RequestId" };
            if (request.Properties().Any(p => !fields.Contains(p.Name))) return Error("InvalidProbeRequest");
            string action = request.Value<string>("Action"), run = request.Value<string>("RunId");
            if (!Guid.TryParseExact(run, "N", out _)) return Error("InvalidProbeRunId");
            string visibility = request.Value<string>("Visibility");
            if (visibility is not ("Public" or "Private")) return Error("InvalidProbeVisibility");
            string table = visibility == "Public" ? ProbeSettings.PublicTable : ProbeSettings.PrivateTable;
            if (action == "ProbeSeed")
            {
                var results = new JArray();
                foreach (string slot in new[] { "Primary", "Audit" })
                {
                    var where = new Where(); where.Equal("RunId", run); where.Equal("Slot", slot);
                    var existing = Backend.GameData.GetMyData(table, where);
                    if (!existing.IsSuccess()) return Reply(action, existing).ToString();
                    if (ParseStorageReply(existing.GetReturnValue())["rows"] is not JArray rows || rows.Count != 0)
                        return Error("UseFreshRunId");
                    var insert = Backend.GameData.Insert(table, new Param {
                        { "RunId", run }, { "Slot", slot }, { "Counter", 0L }, { "Version", 0L }, { "LastRequestId", "" }
                    });
                    results.Add(Reply(action, insert));
                    if (!insert.IsSuccess()) return results.ToString();
                }
                var primary = Own(table, run, "Primary"); var audit = Own(table, run, "Audit");
                return new JObject { ["Ok"] = true, ["PrimaryRowId"] = Id(primary), ["AuditRowId"] = Id(audit), ["SettlementEnabled"] = false }.ToString();
            }
            var own = Own(table, run, "Primary");
            if (action == "ProbeReadSelf") return new JObject {
                ["Ok"] = true, ["RowId"] = Id(own), ["Counter"] = Number(own, "Counter"),
                ["Version"] = Number(own, "Version"), ["LastRequestId"] = Value(own, "LastRequestId"), ["SettlementEnabled"] = false
            }.ToString();
            if (action == "ProbeWriteSelf") return Reply(action, Backend.GameData.UpdateV2(table, Id(own), caller,
                Change(checked(Number(own, "Counter") + 1), checked(Number(own, "Version") + 1), "SelfWrite"))).ToString();
            if (action == "ProbeReadPeer" || action == "ProbeWritePair" || action == "ProbeAtomicPairSuite")
            {
                string other = request.Value<string>("OtherUserId"), otherRow = request.Value<string>("OtherRowId");
                if (other == caller || !ProbeSettings.AllowedUserInDates.Contains(other) || string.IsNullOrWhiteSpace(otherRow))
                    return Error("ProbePeerDenied");
                var read = Backend.GameData.GetV2(table, otherRow, other);
                if (!read.IsSuccess()) return Reply(action, read).ToString();
                var peer = Row(read);
                if (Value(peer, "RunId")?.ToString() != run || Value(peer, "Slot")?.ToString() != "Primary") return Error("ProbePeerRowMismatch");
                if (action == "ProbeAtomicPairSuite") return AtomicPairSuite(table, run, caller, other, otherRow, own, peer);
                if (action == "ProbeReadPeer") return new JObject { ["Ok"] = true, ["Counter"] = Number(peer, "Counter"), ["SettlementEnabled"] = false }.ToString();
                return Reply(action, Backend.GameData.TransactionWriteV2(new[] {
                    TransactionValue.SetUpdateV2(table, Id(own), caller, Change(Number(own, "Counter") + 1, Number(own, "Version") + 1, "PairWrite")),
                    TransactionValue.SetUpdateV2(table, otherRow, other, Change(Number(peer, "Counter") + 1, Number(peer, "Version") + 1, "PairWrite"))
                })).ToString();
            }
            if (action == "ProbeRollback")
            {
                long before = Number(own, "Counter");
                var audit = Own(table, run, "Audit");
                long auditBefore = Number(audit, "Counter");
                // Use a well-formed, future inDate and verify absence before the write.
                const string missingRow = "2099-12-31T23:59:59.999Z";
                var missing = Backend.GameData.GetV2(table, missingRow, caller);
                bool absent = missing.IsSuccess() && ParseStorageReply(missing.GetReturnValue())["row"] is JObject missingData && missingData.Count == 0;
                if (!absent)
                    return Error("MissingRowPreconditionNotConfirmed");
                var result = Backend.GameData.TransactionWriteV2(new[] {
                    TransactionValue.SetUpdateV2(table, Id(own), caller, Change(before + 1, Number(own, "Version") + 1, "RollbackTest")),
                    TransactionValue.SetUpdateV2(table, Id(audit), caller, Change(auditBefore + 1, Number(audit, "Version") + 1, "RollbackTest")),
                    TransactionValue.SetUpdateV2(table, missingRow, caller, Change(1, 1, "RollbackTest"))
                });
                var reply = Reply(action, result);
                reply["Before"] = before; reply["After"] = Number(Own(table, run, "Primary"), "Counter");
                reply["AuditBefore"] = auditBefore; reply["AuditAfter"] = Number(Own(table, run, "Audit"), "Counter");
                reply["RollbackObserved"] = !result.IsSuccess() && before == reply.Value<long>("After") && auditBefore == reply.Value<long>("AuditAfter");
                return reply.ToString();
            }
            if (action == "ProbeConditionalWrite")
            {
                long expected = request.Value<long>("ExpectedVersion"); string requestId = request.Value<string>("RequestId");
                if (expected < 0 || expected > 1000 || !Guid.TryParseExact(requestId, "N", out _)) return Error("InvalidProbeCondition");
                var where = new Where(); where.Equal("owner_inDate", caller); where.Equal("RunId", run);
                where.Equal("Slot", "Primary"); where.Equal("Version", expected);
                return Reply(action, Backend.GameData.TransactionWriteV2(new[] {
                    TransactionValue.SetUpdate(table, where, Change(expected + 1, expected + 1, requestId))
                })).ToString();
            }
            return Error("UnsupportedProbeAction");
        }
        catch { return Error("ProbeValidationOrReadFailed"); }
    }
}
