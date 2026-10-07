using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Analysis;

public static class DependencyAnalyzer
{
    public static void Analyze(AnalysisResult result)
    {
        Reset(result);

        var included = result.IncludedCategories.ToHashSet();
        var nodes = result.Tables
            .Where(t => included.Contains(t.Category) && t.ObsoleteState != ObsoleteState.Removed)
            .OrderBy(t => t.Id)
            .ToList();
        var indexById = new Dictionary<int, int>(nodes.Count);
        for (var i = 0; i < nodes.Count; i++)
        {
            indexById[nodes[i].Id] = i;
        }

        var relations = result.Relations.OrderBy(r => r.RelationId).ToList();
        var deferredKeys = new HashSet<(int, int)>();

        HandleSelfReferences(result, relations, deferredKeys);

        var edges = relations
            .Where(r => r.IsMigrationDependency
                && r.TargetTableId is int target
                && indexById.ContainsKey(r.SourceTableId)
                && indexById.ContainsKey(target))
            .ToList();

        var groupOfNode = BuildDependencyGroups(result, nodes, indexById, edges);
        DeferGroupRelations(result, indexById, edges, groupOfNode, deferredKeys);

        var remainingEdges = edges.Where(e => !e.IsDeferred).ToList();
        var adjacency = BuildAdjacency(nodes.Count, indexById, remainingEdges);
        var components = StronglyConnectedComponents.Find(adjacency);

        MarkUnresolvable(result, nodes, groupOfNode, components);
        AssignLevels(nodes, adjacency, components);
        AssignMigrationSequence(nodes);
    }

    private static void Reset(AnalysisResult result)
    {
        result.DependencyGroups.Clear();
        result.DeferredFields.Clear();

        foreach (var table in result.Tables)
        {
            table.Level = null;
            table.MigrationSequence = null;
            table.DependencyGroupId = null;
            table.IsUnresolvable = false;
        }

        foreach (var relation in result.Relations)
        {
            relation.IsDeferred = false;
        }
    }

    private static void HandleSelfReferences(AnalysisResult result, List<RelationModel> relations, HashSet<(int, int)> deferredKeys)
    {
        var tablesById = result.Tables.ToDictionary(t => t.Id);

        foreach (var relation in relations.Where(r => r.RelationType == RelationType.TableRelation && r.ExclusionReason == ExclusionReason.SelfReference))
        {
            if (relation.Strength == RelationStrength.Soft)
            {
                relation.IsDeferred = true;
                if (deferredKeys.Add((relation.SourceTableId, relation.SourceFieldId)))
                {
                    result.DeferredFields.Add(new DeferredFieldModel(
                        relation.SourceTableId, relation.SourceFieldId, relation.RelationId, DeferralReason.SelfReference, null));
                }
            }
            else
            {
                tablesById.TryGetValue(relation.SourceTableId, out var table);
                var field = table?.Fields.FirstOrDefault(f => f.Id == relation.SourceFieldId);
                var tableName = table?.Name ?? relation.SourceTableId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var fieldName = field?.Name ?? relation.SourceFieldId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                result.Issues.Add(new Issue(
                    IssueSeverity.Warning,
                    IssueType.HardSelfReference,
                    null,
                    tableName,
                    $"Primary key field '{fieldName}' of table '{tableName}' refers to the table itself."));
            }
        }
    }

    private static int[] BuildDependencyGroups(
        AnalysisResult result,
        List<TableModel> nodes,
        Dictionary<int, int> indexById,
        List<RelationModel> edges)
    {
        var components = StronglyConnectedComponents.Find(BuildAdjacency(nodes.Count, indexById, edges));
        var groupOfNode = new int[nodes.Count];

        var nextGroupId = 1;
        foreach (var component in components.Where(c => c.Length > 1).OrderBy(c => c[0]))
        {
            var groupId = nextGroupId++;
            foreach (var node in component)
            {
                groupOfNode[node] = groupId;
                nodes[node].DependencyGroupId = groupId;
            }

            result.DependencyGroups.Add(new DependencyGroupModel
            {
                Id = groupId,
                TableIds = component.Select(n => nodes[n].Id).ToList(),
            });
        }

        return groupOfNode;
    }

    private static void DeferGroupRelations(
        AnalysisResult result,
        Dictionary<int, int> indexById,
        List<RelationModel> edges,
        int[] groupOfNode,
        HashSet<(int, int)> deferredKeys)
    {
        foreach (var edge in edges)
        {
            var source = indexById[edge.SourceTableId];
            var target = indexById[edge.TargetTableId!.Value];
            var groupId = groupOfNode[source];

            if (groupId == 0 || groupId != groupOfNode[target] || edge.Strength != RelationStrength.Soft)
            {
                continue;
            }

            edge.IsDeferred = true;
            if (deferredKeys.Add((edge.SourceTableId, edge.SourceFieldId)))
            {
                result.DeferredFields.Add(new DeferredFieldModel(
                    edge.SourceTableId, edge.SourceFieldId, edge.RelationId, DeferralReason.DependencyGroup, groupId));
            }
        }
    }

    private static void MarkUnresolvable(AnalysisResult result, List<TableModel> nodes, int[] groupOfNode, List<int[]> components)
    {
        var unresolvableByGroup = new SortedDictionary<int, List<TableModel>>();

        foreach (var component in components.Where(c => c.Length > 1))
        {
            foreach (var node in component)
            {
                nodes[node].IsUnresolvable = true;
                var groupId = groupOfNode[node];
                if (!unresolvableByGroup.TryGetValue(groupId, out var list))
                {
                    unresolvableByGroup[groupId] = list = [];
                }

                list.Add(nodes[node]);
            }
        }

        foreach (var (groupId, tables) in unresolvableByGroup)
        {
            result.DependencyGroups.First(g => g.Id == groupId).IsUnresolvable = true;

            var names = string.Join(", ", tables.OrderBy(t => t.Id).Select(t => $"{t.Name} ({t.Id})"));
            result.Issues.Add(new Issue(
                IssueSeverity.Warning,
                IssueType.UnresolvableDependencyGroup,
                null,
                $"Dependency group {groupId}",
                $"Tables still depend on each other through primary key relations and cannot be ordered automatically: {names}."));
        }
    }

    private static void AssignLevels(List<TableModel> nodes, int[][] adjacency, List<int[]> components)
    {
        var componentOfNode = new int[nodes.Count];
        for (var c = 0; c < components.Count; c++)
        {
            foreach (var node in components[c])
            {
                componentOfNode[node] = c;
            }
        }

        var componentLevel = new int[components.Count];
        for (var c = 0; c < components.Count; c++)
        {
            var level = 0;
            foreach (var node in components[c])
            {
                foreach (var target in adjacency[node])
                {
                    var targetComponent = componentOfNode[target];
                    if (targetComponent != c)
                    {
                        level = Math.Max(level, componentLevel[targetComponent] + 1);
                    }
                }
            }

            componentLevel[c] = level;

            if (components[c].Length == 1)
            {
                nodes[components[c][0]].Level = level;
            }
        }
    }

    private static void AssignMigrationSequence(List<TableModel> nodes)
    {
        var sequence = 1;
        foreach (var table in nodes.Where(t => t.Level is not null).OrderBy(t => t.Level).ThenBy(t => t.Id))
        {
            table.MigrationSequence = sequence++;
        }
    }

    private static int[][] BuildAdjacency(int nodeCount, Dictionary<int, int> indexById, List<RelationModel> edges)
    {
        var sets = new SortedSet<int>?[nodeCount];
        foreach (var edge in edges)
        {
            var source = indexById[edge.SourceTableId];
            var target = indexById[edge.TargetTableId!.Value];
            if (source == target)
            {
                continue;
            }

            (sets[source] ??= []).Add(target);
        }

        return sets.Select(s => s is null ? [] : s.ToArray()).ToArray();
    }
}
