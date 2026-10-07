using Dapper;
using Microsoft.Data.SqlClient;
using BcDepAnalyzer.Core.Persistence;

namespace BcDepAnalyzer.Core.Export;

public sealed class AnalysisReader(string connectionString)
{
    public IReadOnlyList<AppRow> Apps() =>
        Query<AppRow>("SELECT AppId, Name AS AppName, Publisher, Version FROM dbo.App ORDER BY Name, AppId");

    public IReadOnlyList<TableRow> Tables() =>
        Query<TableRow>("""
            SELECT t.TableId, t.Name AS TableName, t.Namespace, t.AppId, a.Name AS OriginApp, t.TableType, t.Category,
                   t.ObsoleteState, t.[Level] AS [Level], t.DependencyGroupId, t.IsUnresolvable
            FROM dbo.BcTable AS t
            JOIN dbo.App AS a ON a.AppId = t.AppId
            ORDER BY t.TableId
            """);

    public IReadOnlyList<FieldRow> Fields() =>
        Query<FieldRow>("""
            SELECT TableId, FieldId, FieldName, DataType, Length, FieldClass, IsPrimaryKey, ObsoleteState,
                   OriginAppName, SourceObjectType, SourceObjectId, SourceObjectName
            FROM dbo.vw_TableFields
            ORDER BY TableId, FieldId
            """);

    public IReadOnlyList<RelationRow> Relations() =>
        Query<RelationRow>("""
            SELECT RelationId, SourceTableId, SourceTableName, SourceFieldName, TargetTableId, TargetTableName, TargetFieldName,
                   RelationType, FormulaType, IsConditional, Strength, IsMigrationDependency, ExclusionReason, IsDeferred, RelationText
            FROM dbo.vw_Relations
            ORDER BY RelationId
            """);

    public IReadOnlyList<DependencyGroupRow> DependencyGroups() =>
        Query<DependencyGroupRow>("""
            SELECT m.DependencyGroupId, m.TableId, t.Name AS TableName, g.IsUnresolvable
            FROM dbo.DependencyGroupMember AS m
            JOIN dbo.DependencyGroup AS g ON g.DependencyGroupId = m.DependencyGroupId
            JOIN dbo.BcTable AS t ON t.TableId = m.TableId
            ORDER BY m.DependencyGroupId, m.TableId
            """);

    public IReadOnlyList<MigrationOrderRow> MigrationOrder() =>
        Query<MigrationOrderRow>("""
            SELECT MigrationSequence, [Level] AS [Level], TableId, TableName, Category, DependencyGroupId
            FROM dbo.vw_MigrationOrder
            ORDER BY MigrationSequence
            """);

    public IReadOnlyList<DeferredFieldRow> DeferredFields() =>
        Query<DeferredFieldRow>("""
            SELECT TableId, TableName, FieldId, FieldName, TargetTableName, Reason, DependencyGroupId
            FROM dbo.vw_DeferredFields
            ORDER BY TableId, FieldId
            """);

    public IReadOnlyList<IssueRow> Issues() =>
        Query<IssueRow>("SELECT Severity, IssueType, AppName, ObjectName, Message FROM dbo.AnalysisIssue ORDER BY IssueId");

    public AnalysisInfoRow Info()
    {
        var rows = Query<AnalysisInfoRow>("SELECT IncludedCategories FROM dbo.AnalysisInfo");
        return rows.Count == 1 ? rows[0] : throw new DatabaseException("No analysis is stored yet. Run 'analyzer analyze' first.");
    }

    private IReadOnlyList<T> Query<T>(string sql)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            return connection.Query<T>(sql, commandTimeout: 120).AsList();
        }
        catch (SqlException ex)
        {
            throw new DatabaseException($"Reading the analysis failed: {ex.Message}", ex);
        }
    }
}
