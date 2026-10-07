using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Categorization;

public sealed record NameRule(string Pattern, TableCategory Category);
