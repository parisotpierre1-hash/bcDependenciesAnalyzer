using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Analysis;

internal readonly record struct TableLookup(TableModel? Table, string? FieldName, string? Error);

internal sealed class TableNameResolver
{
    private readonly Dictionary<string, List<TableModel>> _bySimpleName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TableModel> _byQualifiedName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TableModel> _tables = [];

    public TableNameResolver(IEnumerable<TableModel> tables)
    {
        foreach (var table in tables)
        {
            _tables.Add(table);
            if (!_bySimpleName.TryGetValue(table.Name, out var list))
            {
                _bySimpleName[table.Name] = list = [];
            }

            list.Add(table);

            if (!string.IsNullOrEmpty(table.Namespace))
            {
                _byQualifiedName.TryAdd($"{table.Namespace}.{table.Name}", table);
            }
        }
    }

    // Segments are the dotted parts of "[Namespace.]Table[.Field]"; at most one trailing segment is a field name.
    public TableLookup Resolve(IReadOnlyList<string> segments)
    {
        var count = segments.Count;
        var lastError = "table not found";

        for (var tableSegments = count; tableSegments >= Math.Max(1, count - 1); tableSegments--)
        {
            var lookup = tableSegments == 1
                ? LookupSimple(segments[0])
                : LookupQualified(string.Join('.', segments.Take(tableSegments)));

            if (lookup.Table is not null)
            {
                var field = tableSegments < count ? segments[tableSegments] : null;
                return new TableLookup(lookup.Table, field, null);
            }

            if (lookup.Error == "ambiguous name")
            {
                lastError = lookup.Error;
            }
        }

        return new TableLookup(null, null, lastError);
    }

    public TableLookup ResolveTableName(string fullName, Guid? appId = null)
    {
        if (appId is not null)
        {
            var inApp = _tables
                .Where(t => t.AppId == appId && (t.Name.Equals(fullName, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(t.Namespace) && $"{t.Namespace}.{t.Name}".Equals(fullName, StringComparison.OrdinalIgnoreCase))))
                .Take(2)
                .ToList();
            if (inApp.Count == 1)
            {
                return new TableLookup(inApp[0], null, null);
            }
        }

        var qualified = LookupQualified(fullName);
        return qualified.Table is not null ? qualified : LookupSimple(fullName);
    }

    private TableLookup LookupSimple(string name)
    {
        if (!_bySimpleName.TryGetValue(name, out var list))
        {
            return new TableLookup(null, null, "table not found");
        }

        return list.Count == 1
            ? new TableLookup(list[0], null, null)
            : new TableLookup(null, null, "ambiguous name");
    }

    private TableLookup LookupQualified(string name) =>
        _byQualifiedName.TryGetValue(name, out var table)
            ? new TableLookup(table, null, null)
            : new TableLookup(null, null, "table not found");
}
