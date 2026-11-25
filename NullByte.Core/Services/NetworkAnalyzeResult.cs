namespace NullByte.Core.Services;

public sealed record NetworkAnalyzeResult(
    bool Succeeded,
    string FailureMessage,
    string Hostname,
    bool HasRootAccess,
    bool HasBackdoor,
    int RequiredHackingLevel,
    double Money,
    double MaximumMoney,
    double Security,
    double MinimumSecurity,
    double Growth,
    double UsedRam,
    double MaximumRam)
{
    public static NetworkAnalyzeResult Failed(string msg) => new(false, msg, "", false, false, 0, 0, 0, 0, 0, 0, 0, 0);
}
