namespace BcDepAnalyzer.Core.Export;

public static class CsvExporter
{
    public static IReadOnlyList<string> Export(AnalysisReader reader, string folder, char delimiter)
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();

        void Write(string file, string[] header, IEnumerable<IReadOnlyList<object?>> rows)
        {
            var path = Path.Combine(folder, file);
            CsvWriter.Write(path, delimiter, header, rows);
            written.Add(path);
        }

        Write(
            "Apps.csv",
            ["AppId", "AppName", "Publisher", "Version"],
            reader.Apps().Select(a => (IReadOnlyList<object?>)[a.AppId, a.AppName, a.Publisher, a.Version]));

        Write(
            "Tables.csv",
            ["TableId", "TableName", "Namespace", "OriginApp", "TableType", "Category", "ObsoleteState", "Level", "DependencyGroupId", "IsUnresolvable"],
            reader.Tables().Select(t => (IReadOnlyList<object?>)
                [t.TableId, t.TableName, t.Namespace, t.OriginApp, t.TableType, t.Category, t.ObsoleteState, t.Level, t.DependencyGroupId, t.IsUnresolvable]));

        Write(
            "Fields.csv",
            ["TableId", "FieldId", "FieldName", "DataType", "Length", "FieldClass", "IsPrimaryKey", "ObsoleteState", "OriginApp", "SourceObject"],
            reader.Fields().Select(f => (IReadOnlyList<object?>)
                [f.TableId, f.FieldId, f.FieldName, f.DataType, f.Length, f.FieldClass, f.IsPrimaryKey, f.ObsoleteState, f.OriginAppName,
                 $"{f.SourceObjectType} {f.SourceObjectId} {f.SourceObjectName}"]));

        Write(
            "Relationships.csv",
            ["SourceTableId", "SourceTable", "SourceField", "TargetTableId", "TargetTable", "TargetField", "RelationType", "FormulaType",
             "Conditional", "Strength", "IsMigrationDependency", "ExclusionReason", "IsDeferred", "RelationText"],
            reader.Relations().Select(r => (IReadOnlyList<object?>)
                [r.SourceTableId, r.SourceTableName, r.SourceFieldName, r.TargetTableId, r.TargetTableName, r.TargetFieldName, r.RelationType,
                 r.FormulaType, r.IsConditional, r.Strength, r.IsMigrationDependency, r.ExclusionReason, r.IsDeferred, r.RelationText]));

        Write(
            "DependencyGroups.csv",
            ["DependencyGroupId", "TableId", "TableName", "IsUnresolvable"],
            reader.DependencyGroups().Select(g => (IReadOnlyList<object?>)[g.DependencyGroupId, g.TableId, g.TableName, g.IsUnresolvable]));

        Write(
            "MigrationOrder.csv",
            ["Sequence", "Level", "TableId", "TableName", "Category", "DependencyGroupId"],
            reader.MigrationOrder().Select(m => (IReadOnlyList<object?>)[m.MigrationSequence, m.Level, m.TableId, m.TableName, m.Category, m.DependencyGroupId]));

        Write(
            "DeferredFields.csv",
            ["TableId", "TableName", "FieldId", "FieldName", "TargetTable", "Reason", "DependencyGroupId"],
            reader.DeferredFields().Select(d => (IReadOnlyList<object?>)[d.TableId, d.TableName, d.FieldId, d.FieldName, d.TargetTableName, d.Reason, d.DependencyGroupId]));

        Write(
            "Issues.csv",
            ["Severity", "IssueType", "App", "Object", "Message"],
            reader.Issues().Select(i => (IReadOnlyList<object?>)[i.Severity, i.IssueType, i.AppName, i.ObjectName, i.Message]));

        return written;
    }
}
