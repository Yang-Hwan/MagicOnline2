using Amazon.Lambda.Core;
using BackEnd;
using MagicOnline.MatchSettlement;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace BackendFunction;

// Entry signature and class capitalization follow the downloaded official template.
public sealed class BFunc
{
    public Stream Function(Stream stream, ILambdaContext context)
    {
        try
        {
            Backend.Initialize(ref stream);
            // Initialize supplies request credentials but does not populate UserInDate.
            // Resolve identity through the authenticated server API, never request fields.
            var userInfo = Backend.BMember.GetUserInfo();
            if (!userInfo.IsSuccess())
                return Backend.StringToStream(FunctionRequestGate.Error("CallerVerificationFailed"));
            var row = userInfo.GetReturnValuetoJSON()["row"];
            var caller = row["inDate"].ToString();
            var payload = Backend.Content?.ToJson();
            var reply = GameDataProbe.IsProbeRequest(payload)
                ? GameDataProbe.Handle(caller, payload)
                : FunctionRequestGate.Handle(caller, payload);
            return Backend.StringToStream(reply);
        }
        catch
        {
            // SDK errors may include tokens/request headers. Do not echo or log them.
            return Backend.StringToStream(FunctionRequestGate.Error("FunctionInitializationFailed"));
        }
    }
}
