CREATE TABLE dbo.AnalysisInfo (
    Id int NOT NULL CONSTRAINT PK_AnalysisInfo PRIMARY KEY,
    AnalysisDate datetime2 NOT NULL,
    ToolVersion nvarchar(50) NOT NULL,
    InputFiles nvarchar(max) NOT NULL,
    RulesFile nvarchar(400) NOT NULL,
    IncludedCategories nvarchar(400) NOT NULL
);
GO

CREATE TABLE dbo.App (
    AppId uniqueidentifier NOT NULL CONSTRAINT PK_App PRIMARY KEY,
    Name nvarchar(250) NOT NULL,
    Publisher nvarchar(250) NOT NULL,
    Version nvarchar(50) NOT NULL,
    FilePath nvarchar(400) NOT NULL
);
GO

CREATE TABLE dbo.AppDependency (
    AppId uniqueidentifier NOT NULL,
    DependencyAppId uniqueidentifier NOT NULL,
    Name nvarchar(250) NOT NULL,
    Publisher nvarchar(250) NOT NULL,
    MinVersion nvarchar(50) NOT NULL,
    IsMissing bit NOT NULL,
    CONSTRAINT PK_AppDependency PRIMARY KEY (AppId, DependencyAppId),
    CONSTRAINT FK_AppDependency_App FOREIGN KEY (AppId) REFERENCES dbo.App (AppId)
);
GO

CREATE TABLE dbo.BcTable (
    TableId int NOT NULL CONSTRAINT PK_BcTable PRIMARY KEY,
    Name nvarchar(250) NOT NULL,
    Namespace nvarchar(250) NULL,
    AppId uniqueidentifier NOT NULL,
    TableType nvarchar(30) NOT NULL,
    ObsoleteState nvarchar(30) NOT NULL,
    DataPerCompany bit NOT NULL,
    Category nvarchar(30) NOT NULL,
    [Level] int NULL,
    MigrationSequence int NULL,
    DependencyGroupId int NULL,
    IsUnresolvable bit NOT NULL,
    CONSTRAINT FK_BcTable_App FOREIGN KEY (AppId) REFERENCES dbo.App (AppId)
);
GO

CREATE TABLE dbo.TableExtension (
    AppId uniqueidentifier NOT NULL,
    ExtensionId int NOT NULL,
    Name nvarchar(250) NOT NULL,
    ExtendedTableName nvarchar(400) NOT NULL,
    ExtendedTableId int NULL,
    IsOrphan bit NOT NULL,
    CONSTRAINT PK_TableExtension PRIMARY KEY (AppId, ExtensionId),
    CONSTRAINT FK_TableExtension_App FOREIGN KEY (AppId) REFERENCES dbo.App (AppId)
);
GO

CREATE TABLE dbo.BcField (
    TableId int NOT NULL,
    FieldId int NOT NULL,
    Name nvarchar(250) NOT NULL,
    DataType nvarchar(250) NOT NULL,
    Length int NULL,
    FieldClass nvarchar(30) NOT NULL,
    ObsoleteState nvarchar(30) NOT NULL,
    AppId uniqueidentifier NOT NULL,
    SourceObjectType nvarchar(30) NOT NULL,
    SourceObjectId int NOT NULL,
    SourceObjectName nvarchar(250) NOT NULL,
    TableRelationText nvarchar(max) NULL,
    ValidateTableRelation bit NOT NULL,
    CalcFormulaText nvarchar(max) NULL,
    IsPrimaryKey bit NOT NULL,
    CONSTRAINT PK_BcField PRIMARY KEY (TableId, FieldId),
    CONSTRAINT FK_BcField_BcTable FOREIGN KEY (TableId) REFERENCES dbo.BcTable (TableId),
    CONSTRAINT FK_BcField_App FOREIGN KEY (AppId) REFERENCES dbo.App (AppId)
);
GO

CREATE TABLE dbo.PrimaryKeyField (
    TableId int NOT NULL,
    Position int NOT NULL,
    FieldId int NOT NULL,
    CONSTRAINT PK_PrimaryKeyField PRIMARY KEY (TableId, Position),
    CONSTRAINT FK_PrimaryKeyField_BcField FOREIGN KEY (TableId, FieldId) REFERENCES dbo.BcField (TableId, FieldId)
);
GO

CREATE TABLE dbo.Relation (
    RelationId int NOT NULL CONSTRAINT PK_Relation PRIMARY KEY,
    SourceTableId int NOT NULL,
    SourceFieldId int NOT NULL,
    RelationType nvarchar(30) NOT NULL,
    FormulaType nvarchar(30) NULL,
    TargetTableName nvarchar(400) NULL,
    TargetTableId int NULL,
    TargetFieldName nvarchar(250) NULL,
    IsConditional bit NOT NULL,
    BranchIndex int NOT NULL,
    ConditionText nvarchar(max) NULL,
    WhereText nvarchar(max) NULL,
    RelationText nvarchar(max) NOT NULL,
    Strength nvarchar(30) NULL,
    IsMigrationDependency bit NOT NULL,
    ExclusionReason nvarchar(30) NULL,
    IsDeferred bit NOT NULL,
    CONSTRAINT FK_Relation_BcField FOREIGN KEY (SourceTableId, SourceFieldId) REFERENCES dbo.BcField (TableId, FieldId)
);
GO

CREATE INDEX IX_Relation_TargetTableId ON dbo.Relation (TargetTableId);
GO

CREATE TABLE dbo.DependencyGroup (
    DependencyGroupId int NOT NULL CONSTRAINT PK_DependencyGroup PRIMARY KEY,
    TableCount int NOT NULL,
    IsUnresolvable bit NOT NULL
);
GO

CREATE TABLE dbo.DependencyGroupMember (
    DependencyGroupId int NOT NULL,
    TableId int NOT NULL,
    CONSTRAINT PK_DependencyGroupMember PRIMARY KEY (DependencyGroupId, TableId),
    CONSTRAINT FK_DependencyGroupMember_Group FOREIGN KEY (DependencyGroupId) REFERENCES dbo.DependencyGroup (DependencyGroupId),
    CONSTRAINT FK_DependencyGroupMember_BcTable FOREIGN KEY (TableId) REFERENCES dbo.BcTable (TableId)
);
GO

CREATE TABLE dbo.DeferredField (
    TableId int NOT NULL,
    FieldId int NOT NULL,
    RelationId int NOT NULL,
    Reason nvarchar(30) NOT NULL,
    DependencyGroupId int NULL,
    CONSTRAINT PK_DeferredField PRIMARY KEY (TableId, FieldId),
    CONSTRAINT FK_DeferredField_BcField FOREIGN KEY (TableId, FieldId) REFERENCES dbo.BcField (TableId, FieldId),
    CONSTRAINT FK_DeferredField_Relation FOREIGN KEY (RelationId) REFERENCES dbo.Relation (RelationId)
);
GO

CREATE TABLE dbo.AnalysisIssue (
    IssueId int NOT NULL CONSTRAINT PK_AnalysisIssue PRIMARY KEY,
    Severity nvarchar(30) NOT NULL,
    IssueType nvarchar(50) NOT NULL,
    AppName nvarchar(250) NULL,
    ObjectName nvarchar(400) NULL,
    Message nvarchar(max) NOT NULL
);
GO
