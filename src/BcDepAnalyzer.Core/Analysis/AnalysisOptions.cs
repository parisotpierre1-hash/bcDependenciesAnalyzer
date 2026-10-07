using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Analysis;

public sealed record AnalysisOptions(
    IReadOnlyList<string> AppFiles,
    string RulesFile,
    IReadOnlyList<TableCategory> IncludedCategories,
    string ToolVersion);
