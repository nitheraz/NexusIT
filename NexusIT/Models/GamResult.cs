namespace NexusIT.App.Models;

public enum GamIssueType
{
    None,
    GamNotFound,
    ProjectMissing,
    OAuthTokenMissing,
    OAuthTokenEmpty,
    Timeout,
    Unknown
}

public class GamResult
{
    public bool Success { get; set; }

    public int ExitCode { get; set; }

    public string? Output { get; set; }

    public string? Error { get; set; }

    public GamIssueType IssueType { get; set; } = GamIssueType.None;

    public string CombinedOutput
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Error))
                return Output ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Output))
                return Error;

            return Output + Environment.NewLine + Error;
        }
    }
}