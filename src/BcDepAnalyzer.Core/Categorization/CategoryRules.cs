namespace BcDepAnalyzer.Core.Categorization;

public sealed record CategoryRules(IReadOnlyList<NameRule> NameRules, IReadOnlyList<CategoryOverride> Overrides)
{
    public static CategoryRules Empty { get; } = new([], []);
}
