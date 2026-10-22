namespace BackendFunction;

public static class ProbeSettings
{
    // Enable only for a temporary diagnostic deployment and two dedicated test accounts.
    public static readonly bool Enabled = true;
    public static readonly string[] AllowedUserInDates = {
        "2026-09-18T09:01:47.894Z", // existing Unity test account user02
        "2026-09-18T09:01:33.676Z"  // existing Unity test account user04
    };
    public const string PublicTable = "FunctionProbePublic";
    public const string PrivateTable = "FunctionProbePrivate";
}
