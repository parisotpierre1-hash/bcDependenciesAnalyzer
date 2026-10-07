namespace BcDepAnalyzer.Core.Export;

public sealed record AppRow(Guid AppId, string AppName, string Publisher, string Version);

public sealed record TableRow(
    int TableId,
    string TableName,
    string? Namespace,
    Guid AppId,
    string OriginApp,
    string TableType,
    string Category,
    string ObsoleteState,
    int? Level,
    int? DependencyGroupId,
    bool IsUnresolvable);

public sealed record FieldRow(
    int TableId,
    int FieldId,
    string FieldName,
    string DataType,
    int? Length,
    string FieldClass,
    bool IsPrimaryKey,
    string ObsoleteState,
    string OriginAppName,
    string SourceObjectType,
    int SourceObjectId,
    string SourceObjectName);

public sealed record RelationRow(
    int RelationId,
    int SourceTableId,
    string SourceTableName,
    string SourceFieldName,
    int? TargetTableId,
    string? TargetTableName,
    string? TargetFieldName,
    string RelationType,
    string? FormulaType,
    bool IsConditional,
    string? Strength,
    bool IsMigrationDependency,
    string? ExclusionReason,
    bool IsDeferred,
    string RelationText);

public sealed record DependencyGroupRow(int DependencyGroupId, int TableId, string TableName, bool IsUnresolvable);

public sealed record MigrationOrderRow(int MigrationSequence, int Level, int TableId, string TableName, string Category, int? DependencyGroupId);

public sealed record DeferredFieldRow(int TableId, string TableName, int FieldId, string FieldName, string? TargetTableName, string Reason, int? DependencyGroupId);

public sealed record IssueRow(string Severity, string IssueType, string? AppName, string? ObjectName, string Message);

public sealed record AnalysisInfoRow(string IncludedCategories);
