using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Tests.Analysis;

internal static class GraphTestHelper
{
    public static TableModel Table(int id, string name, TableCategory category = TableCategory.Data, ObsoleteState obsolete = ObsoleteState.No) =>
        new()
        {
            Id = id,
            Name = name,
            AppId = Guid.Empty,
            TableType = "Normal",
            ObsoleteState = obsolete,
            Category = category,
        };

    public static RelationModel Dependency(int relationId, int sourceTable, int sourceField, int targetTable, RelationStrength strength) =>
        new()
        {
            RelationId = relationId,
            SourceTableId = sourceTable,
            SourceFieldId = sourceField,
            RelationType = RelationType.TableRelation,
            TargetTableId = targetTable,
            RelationText = "x",
            Strength = strength,
            IsMigrationDependency = true,
        };

    public static RelationModel SelfReference(int relationId, int table, int field, RelationStrength strength) =>
        new()
        {
            RelationId = relationId,
            SourceTableId = table,
            SourceFieldId = field,
            RelationType = RelationType.TableRelation,
            TargetTableId = table,
            RelationText = "x",
            Strength = strength,
            ExclusionReason = ExclusionReason.SelfReference,
        };

    public static AnalysisResult Result(IEnumerable<TableModel> tables, IEnumerable<RelationModel> relations)
    {
        var result = new AnalysisResult { IncludedCategories = [TableCategory.Setup, TableCategory.Data] };
        result.Tables.AddRange(tables);
        result.Relations.AddRange(relations);
        return result;
    }

    public static string Summarize(AnalysisResult result)
    {
        var tables = result.Tables.OrderBy(t => t.Id).Select(t => $"T{t.Id}:L{t.Level}:S{t.MigrationSequence}:G{t.DependencyGroupId}:U{t.IsUnresolvable}");
        var groups = result.DependencyGroups.OrderBy(g => g.Id).Select(g => $"G{g.Id}:{string.Join('+', g.TableIds)}:U{g.IsUnresolvable}");
        var deferred = result.DeferredFields.OrderBy(d => d.TableId).ThenBy(d => d.FieldId).Select(d => $"D{d.TableId}.{d.FieldId}:{d.Reason}:{d.DependencyGroupId}");
        var issues = result.Issues.Select(i => $"{i.Type}:{i.ObjectName}:{i.Message}").OrderBy(s => s, StringComparer.Ordinal);
        return string.Join('\n', tables.Concat(groups).Concat(deferred).Concat(issues));
    }
}
