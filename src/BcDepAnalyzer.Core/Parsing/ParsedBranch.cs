namespace BcDepAnalyzer.Core.Parsing;

public sealed record ParsedBranch(IReadOnlyList<string> NameSegments, string? ConditionText, string? WhereText);
