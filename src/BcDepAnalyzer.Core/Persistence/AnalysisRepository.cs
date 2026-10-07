using System.Data;
using Microsoft.Data.SqlClient;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Persistence;

public sealed class AnalysisRepository(string connectionString)
{
    private static readonly string[] DeleteOrder =
    [
        "DeferredField", "DependencyGroupMember", "DependencyGroup", "PrimaryKeyField", "Relation",
        "BcField", "TableExtension", "BcTable", "AppDependency", "App", "AnalysisIssue", "AnalysisInfo",
    ];

    // Replaces the whole stored model in one transaction; issues are numbered in list order.
    public void Save(AnalysisResult result)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                foreach (var table in DeleteOrder)
                {
                    using var delete = connection.CreateCommand();
                    delete.Transaction = transaction;
                    delete.CommandText = $"DELETE FROM dbo.{table}";
                    delete.ExecuteNonQuery();
                }

                Bulk(connection, transaction, "App", Apps(result));
                Bulk(connection, transaction, "AppDependency", AppDependencies(result));
                Bulk(connection, transaction, "BcTable", Tables(result));
                Bulk(connection, transaction, "TableExtension", TableExtensions(result));
                Bulk(connection, transaction, "BcField", Fields(result));
                Bulk(connection, transaction, "PrimaryKeyField", PrimaryKeys(result));
                Bulk(connection, transaction, "Relation", Relations(result));
                Bulk(connection, transaction, "DependencyGroup", Groups(result));
                Bulk(connection, transaction, "DependencyGroupMember", GroupMembers(result));
                Bulk(connection, transaction, "DeferredField", DeferredFields(result));
                Bulk(connection, transaction, "AnalysisIssue", Issues(result));
                Bulk(connection, transaction, "AnalysisInfo", Info(result));

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (SqlException ex)
        {
            throw new DatabaseException($"Saving the analysis failed: {ex.Message}", ex);
        }
    }

    private static void Bulk(SqlConnection connection, SqlTransaction transaction, string table, DataTable data)
    {
        if (data.Rows.Count == 0)
        {
            return;
        }

        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints, transaction)
        {
            DestinationTableName = $"dbo.{table}",
            BulkCopyTimeout = 0,
        };

        foreach (DataColumn column in data.Columns)
        {
            bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        bulk.WriteToServer(data);
    }

    private static DataTable NewTable(params (string Name, Type Type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
        {
            table.Columns.Add(name, type);
        }

        return table;
    }

    private static object V(object? value) => value ?? DBNull.Value;

    private static DataTable Apps(AnalysisResult result)
    {
        var table = NewTable(("AppId", typeof(Guid)), ("Name", typeof(string)), ("Publisher", typeof(string)), ("Version", typeof(string)), ("FilePath", typeof(string)));
        foreach (var app in result.Apps)
        {
            table.Rows.Add(app.AppId, app.Name, app.Publisher, app.Version, app.FilePath);
        }

        return table;
    }

    private static DataTable AppDependencies(AnalysisResult result)
    {
        var table = NewTable(
            ("AppId", typeof(Guid)), ("DependencyAppId", typeof(Guid)), ("Name", typeof(string)),
            ("Publisher", typeof(string)), ("MinVersion", typeof(string)), ("IsMissing", typeof(bool)));
        var known = result.Apps.Select(a => a.AppId).ToHashSet();

        foreach (var app in result.Apps)
        {
            foreach (var dependency in app.Dependencies.DistinctBy(d => d.AppId))
            {
                table.Rows.Add(app.AppId, dependency.AppId, dependency.Name, dependency.Publisher, dependency.MinVersion, !known.Contains(dependency.AppId));
            }
        }

        return table;
    }

    private static DataTable Tables(AnalysisResult result)
    {
        var table = NewTable(
            ("TableId", typeof(int)), ("Name", typeof(string)), ("Namespace", typeof(string)), ("AppId", typeof(Guid)),
            ("TableType", typeof(string)), ("ObsoleteState", typeof(string)), ("DataPerCompany", typeof(bool)), ("Category", typeof(string)),
            ("Level", typeof(int)), ("MigrationSequence", typeof(int)), ("DependencyGroupId", typeof(int)), ("IsUnresolvable", typeof(bool)));

        foreach (var t in result.Tables.OrderBy(t => t.Id))
        {
            table.Rows.Add(
                t.Id, t.Name, V(t.Namespace), t.AppId, t.TableType, t.ObsoleteState.ToString(), t.DataPerCompany, t.Category.ToString(),
                V(t.Level), V(t.MigrationSequence), V(t.DependencyGroupId), t.IsUnresolvable);
        }

        return table;
    }

    private static DataTable TableExtensions(AnalysisResult result)
    {
        var table = NewTable(
            ("AppId", typeof(Guid)), ("ExtensionId", typeof(int)), ("Name", typeof(string)),
            ("ExtendedTableName", typeof(string)), ("ExtendedTableId", typeof(int)), ("IsOrphan", typeof(bool)));

        foreach (var e in result.TableExtensions)
        {
            table.Rows.Add(e.AppId, e.Id, e.Name, e.ExtendedTableName, V(e.ExtendedTableId), e.IsOrphan);
        }

        return table;
    }

    private static DataTable Fields(AnalysisResult result)
    {
        var table = NewTable(
            ("TableId", typeof(int)), ("FieldId", typeof(int)), ("Name", typeof(string)), ("DataType", typeof(string)), ("Length", typeof(int)),
            ("FieldClass", typeof(string)), ("ObsoleteState", typeof(string)), ("AppId", typeof(Guid)), ("SourceObjectType", typeof(string)),
            ("SourceObjectId", typeof(int)), ("SourceObjectName", typeof(string)), ("TableRelationText", typeof(string)),
            ("ValidateTableRelation", typeof(bool)), ("CalcFormulaText", typeof(string)), ("IsPrimaryKey", typeof(bool)));

        foreach (var t in result.Tables.OrderBy(t => t.Id))
        {
            foreach (var f in t.Fields)
            {
                table.Rows.Add(
                    f.TableId, f.Id, f.Name, f.DataType, V(f.Length), f.FieldClass.ToString(), f.ObsoleteState.ToString(), f.AppId,
                    f.SourceObjectType, f.SourceObjectId, f.SourceObjectName, V(f.TableRelationText), f.ValidateTableRelation,
                    V(f.CalcFormulaText), f.IsPrimaryKey);
            }
        }

        return table;
    }

    private static DataTable PrimaryKeys(AnalysisResult result)
    {
        var table = NewTable(("TableId", typeof(int)), ("Position", typeof(int)), ("FieldId", typeof(int)));
        foreach (var t in result.Tables.OrderBy(t => t.Id))
        {
            for (var i = 0; i < t.PrimaryKeyFieldIds.Count; i++)
            {
                table.Rows.Add(t.Id, i + 1, t.PrimaryKeyFieldIds[i]);
            }
        }

        return table;
    }

    private static DataTable Relations(AnalysisResult result)
    {
        var table = NewTable(
            ("RelationId", typeof(int)), ("SourceTableId", typeof(int)), ("SourceFieldId", typeof(int)), ("RelationType", typeof(string)),
            ("FormulaType", typeof(string)), ("TargetTableName", typeof(string)), ("TargetTableId", typeof(int)), ("TargetFieldName", typeof(string)),
            ("IsConditional", typeof(bool)), ("BranchIndex", typeof(int)), ("ConditionText", typeof(string)), ("WhereText", typeof(string)),
            ("RelationText", typeof(string)), ("Strength", typeof(string)), ("IsMigrationDependency", typeof(bool)),
            ("ExclusionReason", typeof(string)), ("IsDeferred", typeof(bool)));

        foreach (var r in result.Relations.OrderBy(r => r.RelationId))
        {
            table.Rows.Add(
                r.RelationId, r.SourceTableId, r.SourceFieldId, r.RelationType.ToString(), V(r.FormulaType), V(r.TargetTableName),
                V(r.TargetTableId), V(r.TargetFieldName), r.IsConditional, r.BranchIndex, V(r.ConditionText), V(r.WhereText),
                r.RelationText, V(r.Strength?.ToString()), r.IsMigrationDependency, V(r.ExclusionReason?.ToString()), r.IsDeferred);
        }

        return table;
    }

    private static DataTable Groups(AnalysisResult result)
    {
        var table = NewTable(("DependencyGroupId", typeof(int)), ("TableCount", typeof(int)), ("IsUnresolvable", typeof(bool)));
        foreach (var g in result.DependencyGroups.OrderBy(g => g.Id))
        {
            table.Rows.Add(g.Id, g.TableIds.Count, g.IsUnresolvable);
        }

        return table;
    }

    private static DataTable GroupMembers(AnalysisResult result)
    {
        var table = NewTable(("DependencyGroupId", typeof(int)), ("TableId", typeof(int)));
        foreach (var g in result.DependencyGroups.OrderBy(g => g.Id))
        {
            foreach (var tableId in g.TableIds)
            {
                table.Rows.Add(g.Id, tableId);
            }
        }

        return table;
    }

    private static DataTable DeferredFields(AnalysisResult result)
    {
        var table = NewTable(
            ("TableId", typeof(int)), ("FieldId", typeof(int)), ("RelationId", typeof(int)), ("Reason", typeof(string)), ("DependencyGroupId", typeof(int)));
        foreach (var d in result.DeferredFields.OrderBy(d => d.TableId).ThenBy(d => d.FieldId))
        {
            table.Rows.Add(d.TableId, d.FieldId, d.RelationId, d.Reason.ToString(), V(d.DependencyGroupId));
        }

        return table;
    }

    private static DataTable Issues(AnalysisResult result)
    {
        var table = NewTable(
            ("IssueId", typeof(int)), ("Severity", typeof(string)), ("IssueType", typeof(string)),
            ("AppName", typeof(string)), ("ObjectName", typeof(string)), ("Message", typeof(string)));

        var id = 1;
        foreach (var issue in result.Issues)
        {
            table.Rows.Add(id++, issue.Severity.ToString(), issue.Type.ToString(), V(issue.AppName), V(issue.ObjectName), issue.Message);
        }

        return table;
    }

    private static DataTable Info(AnalysisResult result)
    {
        var table = NewTable(
            ("Id", typeof(int)), ("AnalysisDate", typeof(DateTime)), ("ToolVersion", typeof(string)),
            ("InputFiles", typeof(string)), ("RulesFile", typeof(string)), ("IncludedCategories", typeof(string)));
        table.Rows.Add(
            1, result.AnalysisDate, result.ToolVersion, string.Join('\n', result.InputFiles), result.RulesFile,
            string.Join(',', result.IncludedCategories));
        return table;
    }
}
