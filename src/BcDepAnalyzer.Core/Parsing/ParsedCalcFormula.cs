namespace BcDepAnalyzer.Core.Parsing;

public sealed record ParsedCalcFormula(string FormulaType, IReadOnlyList<string> NameSegments, string? WhereText);
