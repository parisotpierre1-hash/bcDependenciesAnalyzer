CREATE VIEW dbo.vw_MigrationOrder AS
SELECT
    t.MigrationSequence,
    t.[Level],
    t.TableId,
    t.Name AS TableName,
    t.Category,
    t.DependencyGroupId,
    a.Name AS AppName
FROM dbo.BcTable AS t
JOIN dbo.App AS a ON a.AppId = t.AppId
WHERE t.MigrationSequence IS NOT NULL;
GO

CREATE VIEW dbo.vw_DeferredFields AS
SELECT
    d.TableId,
    t.Name AS TableName,
    d.FieldId,
    f.Name AS FieldName,
    r.TargetTableName,
    d.Reason,
    d.DependencyGroupId
FROM dbo.DeferredField AS d
JOIN dbo.BcTable AS t ON t.TableId = d.TableId
JOIN dbo.BcField AS f ON f.TableId = d.TableId AND f.FieldId = d.FieldId
JOIN dbo.Relation AS r ON r.RelationId = d.RelationId;
GO

CREATE VIEW dbo.vw_TableFields AS
SELECT
    f.TableId,
    t.Name AS TableName,
    f.FieldId,
    f.Name AS FieldName,
    f.DataType,
    f.Length,
    f.FieldClass,
    f.IsPrimaryKey,
    f.ObsoleteState,
    a.Name AS OriginAppName,
    f.SourceObjectType,
    f.SourceObjectId,
    f.SourceObjectName
FROM dbo.BcField AS f
JOIN dbo.BcTable AS t ON t.TableId = f.TableId
JOIN dbo.App AS a ON a.AppId = f.AppId;
GO

CREATE VIEW dbo.vw_Relations AS
SELECT
    r.RelationId,
    r.SourceTableId,
    s.Name AS SourceTableName,
    f.Name AS SourceFieldName,
    r.TargetTableId,
    COALESCE(tt.Name, r.TargetTableName) AS TargetTableName,
    r.TargetFieldName,
    r.RelationType,
    r.FormulaType,
    r.IsConditional,
    r.Strength,
    r.IsMigrationDependency,
    r.ExclusionReason,
    r.IsDeferred,
    r.RelationText
FROM dbo.Relation AS r
JOIN dbo.BcTable AS s ON s.TableId = r.SourceTableId
JOIN dbo.BcField AS f ON f.TableId = r.SourceTableId AND f.FieldId = r.SourceFieldId
LEFT JOIN dbo.BcTable AS tt ON tt.TableId = r.TargetTableId;
GO
