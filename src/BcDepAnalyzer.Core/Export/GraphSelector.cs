using System.Globalization;

namespace BcDepAnalyzer.Core.Export;

public sealed record GraphSelection(IReadOnlyList<TableRow> Nodes, IReadOnlyList<RelationRow> Edges);

public static class GraphSelector
{
    public static GraphSelection Select(
        IReadOnlyList<TableRow> tables,
        IReadOnlyList<RelationRow> relations,
        IReadOnlyList<string> defaultCategories,
        GraphMlOptions options)
    {
        var categories = (options.Categories.Count > 0 ? options.Categories : defaultCategories).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = tables
            .Where(t => categories.Contains(t.Category)
                && t.ObsoleteState != "Removed"
                && (options.Apps.Count == 0 || options.Apps.Any(a => MatchesApp(t, a))))
            .ToDictionary(t => t.TableId);

        var edges = relations
            .Where(r => (options.AllRelations || r.IsMigrationDependency)
                && r.TargetTableId is not null
                && r.TargetTableId != r.SourceTableId)
            .ToList();

        var nodeIds = options.Tables.Count == 0
            ? candidates.Keys.ToHashSet()
            : Neighborhood(tables, candidates, edges, options);

        return new GraphSelection(
            nodeIds.OrderBy(id => id).Select(id => candidates[id]).ToList(),
            edges
                .Where(e => nodeIds.Contains(e.SourceTableId) && nodeIds.Contains(e.TargetTableId!.Value))
                .OrderBy(e => e.RelationId)
                .ToList());
    }

    private static bool MatchesApp(TableRow table, string app) =>
        table.OriginApp.Equals(app, StringComparison.OrdinalIgnoreCase)
        || table.AppId.ToString("D").Equals(app, StringComparison.OrdinalIgnoreCase);

    private static HashSet<int> Neighborhood(
        IReadOnlyList<TableRow> tables,
        Dictionary<int, TableRow> candidates,
        List<RelationRow> edges,
        GraphMlOptions options)
    {
        var visited = new HashSet<int>();
        var frontier = new List<int>();

        foreach (var spec in options.Tables)
        {
            var match = int.TryParse(spec, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                ? tables.FirstOrDefault(t => t.TableId == id)
                : tables.FirstOrDefault(t => t.TableName.Equals(spec, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                throw new ConfigurationException($"Unknown table '{spec}'.");
            }

            if (!candidates.ContainsKey(match.TableId))
            {
                throw new ConfigurationException($"Table '{spec}' is excluded by the app / category filters.");
            }

            if (visited.Add(match.TableId))
            {
                frontier.Add(match.TableId);
            }
        }

        var neighbors = new Dictionary<int, List<int>>();
        foreach (var edge in edges)
        {
            var source = edge.SourceTableId;
            var target = edge.TargetTableId!.Value;
            if (!candidates.ContainsKey(source) || !candidates.ContainsKey(target))
            {
                continue;
            }

            if (!neighbors.TryGetValue(source, out var forward))
            {
                neighbors[source] = forward = [];
            }

            if (!neighbors.TryGetValue(target, out var backward))
            {
                neighbors[target] = backward = [];
            }

            forward.Add(target);
            backward.Add(source);
        }

        for (var hop = 0; hop < options.Depth && frontier.Count > 0; hop++)
        {
            var next = new List<int>();
            foreach (var id in frontier)
            {
                foreach (var neighbor in neighbors.GetValueOrDefault(id) ?? [])
                {
                    if (visited.Add(neighbor))
                    {
                        next.Add(neighbor);
                    }
                }
            }

            frontier = next;
        }

        return visited;
    }
}
