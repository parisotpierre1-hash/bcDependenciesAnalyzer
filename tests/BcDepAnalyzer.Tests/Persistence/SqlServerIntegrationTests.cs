using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Export;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Persistence;
using Microsoft.Data.SqlClient;
using static BcDepAnalyzer.Tests.Analysis.ModelTestHelper;

namespace BcDepAnalyzer.Tests.Persistence;

public sealed class SqlServerIntegrationTests
{
    // Runs only when BCDEP_TEST_CONNECTION points at a disposable test database.
    private static string? ConnectionString => Environment.GetEnvironmentVariable("BCDEP_TEST_CONNECTION");

    private static AnalysisResult BuildResult()
    {
        var result = ModelBuilder.Build(
            [
                Package("base.app", BaseAppId, "Base",
                [
                    Table(18, "Customer", ["No."], ns: "Microsoft.Sales", fields: [Field(1, "No."), Field(30, "Bill-to", relation: "Customer"), Field(31, "Bank", relation: "\"Customer Bank Account\".Code")]),
                    Table(287, "Customer Bank Account", ["Customer No.", "Code"], fields: [Field(1, "Customer No.", relation: "Customer"), Field(2, "Code")]),
                    Table(36, "Sales Header", ["No."], fields: [Field(1, "No."), Field(2, "Sell-to", relation: "Customer"), Field(3, "Missing", relation: "\"Nope\"")]),
                ]),
            ],
            Categorizer(),
            Included);

        DependencyAnalyzer.Analyze(result);
        result.AnalysisDate = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        result.ToolVersion = "test";
        result.InputFiles = ["base.app"];
        result.RulesFile = "rules.json";
        return result;
    }

    private static long Count(string sql)
    {
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    [Fact]
    public void Initialize_is_idempotent_and_save_replaces_the_stored_model()
    {
        if (ConnectionString is null)
        {
            return;
        }

        var initializer = new DatabaseInitializer(ConnectionString);
        initializer.Initialize();
        initializer.Initialize();

        var repository = new AnalysisRepository(ConnectionString);
        var result = BuildResult();
        repository.Save(result);
        repository.Save(result);

        Assert.Equal(1, Count("SELECT COUNT(*) FROM dbo.App"));
        Assert.Equal(3, Count("SELECT COUNT(*) FROM dbo.BcTable"));
        Assert.Equal(result.Tables.Sum(t => t.Fields.Count), Count("SELECT COUNT(*) FROM dbo.BcField"));
        Assert.Equal(result.Relations.Count, Count("SELECT COUNT(*) FROM dbo.Relation"));
        Assert.Equal(result.Issues.Count, Count("SELECT COUNT(*) FROM dbo.AnalysisIssue"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM dbo.DependencyGroup"));
        Assert.Equal(2, Count("SELECT COUNT(*) FROM dbo.DependencyGroupMember"));
        Assert.Equal(result.DeferredFields.Count, Count("SELECT COUNT(*) FROM dbo.DeferredField"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM dbo.AnalysisInfo"));

        Assert.Equal(3, Count("SELECT COUNT(*) FROM dbo.vw_MigrationOrder"));
        Assert.Equal(result.Relations.Count, Count("SELECT COUNT(*) FROM dbo.vw_Relations"));
        Assert.Equal(result.Tables.Sum(t => t.Fields.Count), Count("SELECT COUNT(*) FROM dbo.vw_TableFields"));
        Assert.Equal(result.DeferredFields.Count, Count("SELECT COUNT(*) FROM dbo.vw_DeferredFields"));
    }

    [Fact]
    public void Failed_save_leaves_the_previous_model_untouched()
    {
        if (ConnectionString is null)
        {
            return;
        }

        new DatabaseInitializer(ConnectionString).Initialize();
        var repository = new AnalysisRepository(ConnectionString);
        repository.Save(BuildResult());

        var broken = BuildResult();
        broken.Relations.Add(new RelationModel
        {
            RelationId = 9999,
            SourceTableId = 18,
            SourceFieldId = 12345,
            RelationType = RelationType.TableRelation,
            RelationText = "x",
        });

        Assert.Throws<DatabaseException>(() => repository.Save(broken));
        Assert.Equal(3, Count("SELECT COUNT(*) FROM dbo.BcTable"));
    }

    [Fact]
    public void Csv_export_writes_the_documented_files_and_columns()
    {
        if (ConnectionString is null)
        {
            return;
        }

        new DatabaseInitializer(ConnectionString).Initialize();
        new AnalysisRepository(ConnectionString).Save(BuildResult());

        var folder = Path.Combine(Path.GetTempPath(), $"bcdep-export-{Guid.NewGuid():N}");
        try
        {
            CsvExporter.Export(new AnalysisReader(ConnectionString), folder, ';');

            string Header(string file) => File.ReadLines(Path.Combine(folder, file)).First().TrimStart('\uFEFF');

            Assert.Equal("AppId;AppName;Publisher;Version", Header("Apps.csv"));
            Assert.Equal("TableId;TableName;Namespace;OriginApp;TableType;Category;ObsoleteState;Level;DependencyGroupId;IsUnresolvable", Header("Tables.csv"));
            Assert.Equal("TableId;FieldId;FieldName;DataType;Length;FieldClass;IsPrimaryKey;ObsoleteState;OriginApp;SourceObject", Header("Fields.csv"));
            Assert.Equal(
                "SourceTableId;SourceTable;SourceField;TargetTableId;TargetTable;TargetField;RelationType;FormulaType;Conditional;Strength;IsMigrationDependency;ExclusionReason;IsDeferred;RelationText",
                Header("Relationships.csv"));
            Assert.Equal("DependencyGroupId;TableId;TableName;IsUnresolvable", Header("DependencyGroups.csv"));
            Assert.Equal("Sequence;Level;TableId;TableName;Category;DependencyGroupId", Header("MigrationOrder.csv"));
            Assert.Equal("TableId;TableName;FieldId;FieldName;TargetTable;Reason;DependencyGroupId", Header("DeferredFields.csv"));
            Assert.Equal("Severity;IssueType;App;Object;Message", Header("Issues.csv"));

            Assert.Equal(3, File.ReadLines(Path.Combine(folder, "MigrationOrder.csv")).Count() - 1);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Batches_are_split_on_go_lines()
    {
        var batches = DatabaseInitializer.SplitBatches("A\r\nGO\r\nB\r\nC\r\n go \r\n\r\nGO\r\nD").ToList();

        Assert.Equal(["A", "B\nC", "D"], batches);
    }
}
