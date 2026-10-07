namespace BcDepAnalyzer.Core.Model;

public sealed record Issue(IssueSeverity Severity, IssueType Type, string? AppName, string? ObjectName, string Message)
{
    public int IssueId { get; init; }
}
