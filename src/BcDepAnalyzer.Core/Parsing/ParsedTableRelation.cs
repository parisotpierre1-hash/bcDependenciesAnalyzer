namespace BcDepAnalyzer.Core.Parsing;

public sealed record ParsedTableRelation(bool IsConditional, IReadOnlyList<ParsedBranch> Branches);
