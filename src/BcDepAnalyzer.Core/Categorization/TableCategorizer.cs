using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Categorization;

public sealed class TableCategorizer
{
    private const int SystemTableMinId = 2_000_000_000;

    private readonly Dictionary<int, TableCategory> _overrides;
    private readonly IReadOnlyList<NameRule> _nameRules;

    public TableCategorizer(CategoryRules rules)
    {
        _nameRules = rules.NameRules;
        _overrides = new Dictionary<int, TableCategory>();
        foreach (var rule in rules.Overrides)
        {
            _overrides[rule.TableId] = rule.Category;
        }
    }

    public TableCategory Categorize(int tableId, string tableName, string? tableType)
    {
        if (_overrides.TryGetValue(tableId, out var overridden))
        {
            return overridden;
        }

        if (tableId >= SystemTableMinId)
        {
            return TableCategory.System;
        }

        if (string.Equals(tableType, "Temporary", StringComparison.OrdinalIgnoreCase))
        {
            return TableCategory.Temporary;
        }

        if (!string.IsNullOrEmpty(tableType) && !string.Equals(tableType, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            return TableCategory.External;
        }

        foreach (var rule in _nameRules)
        {
            if (WildcardMatch(rule.Pattern, tableName))
            {
                return rule.Category;
            }
        }

        return TableCategory.Data;
    }

    // '*' is the only wildcard; matching is case-insensitive and covers the full name.
    internal static bool WildcardMatch(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (p < pattern.Length && char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t]))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
