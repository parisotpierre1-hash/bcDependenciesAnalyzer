namespace BcDepAnalyzer.Core.Parsing;

internal readonly record struct AlToken(AlTokenKind Kind, string Text, int Start, int End);
