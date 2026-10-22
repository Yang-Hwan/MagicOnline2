using System.Text.Json;

namespace MagicOnline.MatchSettlement;

// Safe preparation endpoint. This does not implement or advertise live settlement.
public static class FunctionRequestGate
{
    const int MaxRequestChars = 4096;

    public static string Error(string code) => JsonSerializer.Serialize(new { Ok = false, Code = code });

    public static string Handle(string sdkCaller, string payload)
    {
        if (string.IsNullOrWhiteSpace(sdkCaller)) return Error("Unauthenticated");
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaxRequestChars) return Error("InvalidRequest");
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 8 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Error("InvalidRequest");
            string action = null;
            foreach (var field in document.RootElement.EnumerateObject())
            {
                // Reject caller/coin/rule overrides and duplicate action fields.
                if (field.Name != "Action" || action != null || field.Value.ValueKind != JsonValueKind.String)
                    return Error("InvalidRequest");
                action = field.Value.GetString();
            }
            if (action == "Health") return JsonSerializer.Serialize(new {
                Ok = true, Code = "ReadyForIntegration", ApiVersion = 1,
                StorageConnected = false, SettlementEnabled = false
            });
            if (action is "Register" or "Ready" or "Heartbeat" or "ConfirmResult" or "Surrender" or "EvaluateDisconnect")
                return Error("SettlementStorageNotConfigured");
            return Error("UnsupportedAction");
        }
        catch (JsonException) { return Error("InvalidRequest"); }
    }
}
