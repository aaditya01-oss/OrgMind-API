namespace OrgMind.API.Services;

// The rules for ending old sign-ins, kept in one place so the API and the tests use the same ones.
public static class SessionRevocation
{
    // Tokens made before this feature have no "tv" claim. They count as version 0.
    public static int ReadVersion(string? claim) => int.TryParse(claim, out var v) ? v : 0;

    // The token is still good only if the person exists and the counter has not moved on.
    public static bool IsStillValid(int? currentVersion, int tokenVersion) =>
        currentVersion is not null && currentVersion == tokenVersion;
}