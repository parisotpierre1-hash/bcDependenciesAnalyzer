using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Categorization;

public static class TableCategoryParser
{
    public static IReadOnlyList<TableCategory> Parse(IEnumerable<string> names)
    {
        var result = new List<TableCategory>();

        foreach (var raw in names.SelectMany(n => n.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
        {
            if (!Enum.TryParse<TableCategory>(raw, ignoreCase: true, out var category) || !Enum.IsDefined(category))
            {
                var valid = string.Join(", ", Enum.GetNames<TableCategory>());
                throw new ConfigurationException($"Unknown table category '{raw}'. Valid values: {valid}.");
            }

            if (!result.Contains(category))
            {
                result.Add(category);
            }
        }

        return result;
    }
}
