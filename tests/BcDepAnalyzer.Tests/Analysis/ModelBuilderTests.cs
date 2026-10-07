using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;
using static BcDepAnalyzer.Tests.Analysis.ModelTestHelper;

namespace BcDepAnalyzer.Tests.Analysis;

public sealed class ModelBuilderTests
{
    private static AnalysisResult Build(params LoadedPackage[] packages) =>
        ModelBuilder.Build(packages, Categorizer(), Included);

    private static RelationModel RelationOf(AnalysisResult result, int tableId, int fieldId, int branch = 0, RelationType type = RelationType.TableRelation) =>
        result.Relations.Single(r => r.SourceTableId == tableId && r.SourceFieldId == fieldId && r.BranchIndex == branch && r.RelationType == type);

    [Fact]
    public void Table_extension_fields_are_merged_with_their_origin()
    {
        var result = Build(
            Package("a.app", BaseAppId, "Base", [Table(18, "Customer", ["No."], fields: [Field(1, "No."), Field(2, "Name", "Text", 100)])]),
            Package("b.app", ExtAppId, "Ext", extensions: [Extension(50000, "CustomerExt", "Customer", Field(50000, "Store Type", length: 10))]));

        var customer = Assert.Single(result.Tables);
        Assert.Equal([1, 2, 50000], customer.Fields.Select(f => f.Id));
        var added = customer.Fields.Single(f => f.Id == 50000);
        Assert.Equal(ExtAppId, added.AppId);
        Assert.Equal("TableExtension", added.SourceObjectType);
        Assert.Equal(50000, added.SourceObjectId);
        Assert.Equal("CustomerExt", added.SourceObjectName);
        Assert.Equal(BaseAppId, customer.Fields[0].AppId);
        Assert.Equal("Table", customer.Fields[0].SourceObjectType);
        Assert.False(Assert.Single(result.TableExtensions).IsOrphan);
    }

    [Fact]
    public void Orphan_extension_is_reported_and_not_merged()
    {
        var result = Build(
            Package("b.app", ExtAppId, "Ext",
                [Table(100, "Other", fields: [Field(1, "Code")])],
                [Extension(50000, "GhostExt", "Missing Table", Field(50000, "X"))]));

        var extension = Assert.Single(result.TableExtensions);
        Assert.True(extension.IsOrphan);
        Assert.Null(extension.ExtendedTableId);
        Assert.Single(Assert.Single(result.Tables).Fields);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.OrphanTableExtension, issue.Type);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Extension_can_target_a_namespace_qualified_table()
    {
        var result = Build(
            Package("a.app", BaseAppId, "Base", [Table(18, "Customer", ns: "Microsoft.Sales", fields: [Field(1, "No.")])]),
            Package("b.app", ExtAppId, "Ext", extensions: [Extension(50000, "CustomerExt", "Microsoft.Sales.Customer", Field(50000, "X"))]));

        Assert.Equal(2, Assert.Single(result.Tables).Fields.Count);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Primary_key_follows_the_key_definition_in_order()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
            [Table(37, "Sales Line", ["Document No.", "Line No."], fields: [Field(1, "Line No.", "Integer", null), Field(3, "Document No."), Field(5, "Type")])]));

        var table = Assert.Single(result.Tables);
        Assert.Equal([3, 1], table.PrimaryKeyFieldIds);
        Assert.Equal([true, true, false], table.Fields.Select(f => f.IsPrimaryKey));
    }

    [Fact]
    public void Primary_key_falls_back_to_first_declared_field()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
            [Table(10, "Country", fields: [Field(5, "Code"), Field(1, "Name", "Text", 50)])]));

        var table = Assert.Single(result.Tables);
        Assert.Equal([5], table.PrimaryKeyFieldIds);
        Assert.True(table.Fields.Single(f => f.Id == 5).IsPrimaryKey);
    }

    [Fact]
    public void Duplicate_app_id_is_an_error()
    {
        var result = Build(
            Package("a.app", BaseAppId, "Base"),
            Package("b.app", BaseAppId, "Base copy"));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.DuplicateAppId, issue.Type);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Single(result.Apps);
    }

    [Fact]
    public void Duplicate_table_id_is_an_error()
    {
        var result = Build(
            Package("a.app", BaseAppId, "Base", [Table(18, "Customer", fields: [Field(1, "No.")])]),
            Package("b.app", ExtAppId, "Ext", [Table(18, "Other", fields: [Field(1, "Code")])]));

        Assert.Contains(result.Issues, i => i.Type == IssueType.DuplicateTableId && i.Severity == IssueSeverity.Error);
    }

    [Fact]
    public void Missing_declared_dependency_is_a_warning()
    {
        var missing = new ManifestDependency(Guid.Parse("99999999-0000-0000-0000-000000000000"), "System", "Microsoft", "23.0.0.0");
        var result = Build(Package("a.app", BaseAppId, "Base", dependencies: [missing]));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.MissingDependency, issue.Type);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Duplicate_field_id_across_extensions_keeps_first_and_warns()
    {
        var result = Build(
            Package("a.app", BaseAppId, "Base", [Table(18, "Customer", fields: [Field(1, "No.")])]),
            Package("b.app", ExtAppId, "Ext", extensions: [Extension(1, "ClashExt", "Customer", Field(1, "Clash"))]));

        var customer = Assert.Single(result.Tables);
        Assert.Equal("No.", Assert.Single(customer.Fields).Name);
        Assert.Contains(result.Issues, i => i.Type == IssueType.DuplicateFieldId);
    }

    [Fact]
    public void Relation_target_with_field_is_split_and_resolved()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(18, "Customer", fields: [Field(1, "No."), Field(30, "Bank", relation: "\"Customer Bank Account\".Code WHERE (\"Customer No.\" = FIELD(\"No.\"))")]),
            Table(287, "Customer Bank Account", fields: [Field(1, "Code")]),
        ]));

        var relation = RelationOf(result, 18, 30);
        Assert.Equal(287, relation.TargetTableId);
        Assert.Equal("Customer Bank Account", relation.TargetTableName);
        Assert.Equal("Code", relation.TargetFieldName);
        Assert.Equal("\"Customer No.\" = FIELD(\"No.\")", relation.WhereText);
        Assert.Equal(RelationStrength.Soft, relation.Strength);
        Assert.True(relation.IsMigrationDependency);
    }

    [Fact]
    public void Namespace_qualified_relation_target_is_resolved()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(18, "Customer", ns: "Microsoft.Sales", fields: [Field(1, "No.")]),
            Table(36, "Header", fields: [Field(1, "No."), Field(2, "Cust", relation: "Microsoft.Sales.Customer")]),
        ]));

        Assert.Equal(18, RelationOf(result, 36, 2).TargetTableId);
    }

    [Fact]
    public void Ambiguous_simple_name_is_unresolved_with_warning()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(18, "Customer", ns: "A", fields: [Field(1, "No.")]),
            Table(19, "Customer", ns: "B", fields: [Field(1, "No.")]),
            Table(36, "Header", fields: [Field(1, "No."), Field(2, "Cust", relation: "Customer")]),
        ]));

        var relation = RelationOf(result, 36, 2);
        Assert.Null(relation.TargetTableId);
        Assert.Equal(ExclusionReason.Unresolved, relation.ExclusionReason);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.UnresolvedRelationTarget, issue.Type);
        Assert.Contains("ambiguous", issue.Message);
    }

    [Fact]
    public void Unparseable_relation_is_stored_without_target_and_reported_once()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
            [Table(36, "Header", fields: [Field(1, "No."), Field(2, "Cust", relation: "IF (")])]));

        var relation = RelationOf(result, 36, 2);
        Assert.Null(relation.TargetTableId);
        Assert.Equal(ExclusionReason.Unresolved, relation.ExclusionReason);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.RelationParseError, issue.Type);
    }

    [Fact]
    public void Flow_field_relation_is_stored_but_not_a_dependency()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(18, "Customer", fields: [Field(1, "No."), Field(59, "Balance", "Decimal", null, FieldClass.FlowField, calc: "Sum(\"Cust. Entry\".Amount WHERE (\"Customer No.\" = FIELD(\"No.\")))")]),
            Table(21, "Cust. Entry", fields: [Field(1, "Entry No.")]),
        ]));

        var relation = RelationOf(result, 18, 59, type: RelationType.FlowField);
        Assert.Equal("Sum", relation.FormulaType);
        Assert.Equal(21, relation.TargetTableId);
        Assert.Equal("Amount", relation.TargetFieldName);
        Assert.Null(relation.Strength);
        Assert.False(relation.IsMigrationDependency);
        Assert.Equal(ExclusionReason.FlowField, relation.ExclusionReason);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Eligibility_rules_apply_in_the_documented_order()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(1, "Target", fields: [Field(1, "Code")]),
            Table(2, "Gone", obsolete: ObsoleteState.Removed, fields: [Field(1, "Code")]),
            Table(3, "Cust. Ledger Entry", fields: [Field(1, "Entry No.")]),
            Table(
                10, "Source", ["Code"],
                fields:
                [
                    Field(1, "Code"),
                    Field(2, "Normal", relation: "Target"),
                    Field(3, "FlowFieldWithRelation", fieldClass: FieldClass.FlowField, relation: "Target"),
                    Field(4, "Filter", fieldClass: FieldClass.FlowFilter, relation: "Target"),
                    Field(5, "Cond", relation: "IF (Type = CONST(A)) Target ELSE Target"),
                    Field(6, "NotValidated", relation: "Target", validate: false),
                    Field(7, "ObsoleteField", relation: "Target", obsolete: ObsoleteState.Removed),
                    Field(8, "ToRemovedTable", relation: "Gone"),
                    Field(9, "ToLedger", relation: "\"Cust. Ledger Entry\""),
                    Field(10, "ToUnknown", relation: "\"Unknown Table\""),
                    Field(11, "ToSelf", relation: "Source"),
                    Field(12, "PendingObsolete", relation: "Target", obsolete: ObsoleteState.Pending),
                ]),
            Table(20, "Special Ledger Entry", fields: [Field(1, "Code"), Field(2, "ToTarget", relation: "Target")]),
        ]));

        Assert.Null(RelationOf(result, 10, 2).ExclusionReason);
        Assert.Equal(ExclusionReason.FlowField, RelationOf(result, 10, 3).ExclusionReason);
        Assert.Equal(ExclusionReason.FlowFilter, RelationOf(result, 10, 4).ExclusionReason);
        Assert.Equal(ExclusionReason.Conditional, RelationOf(result, 10, 5, 0).ExclusionReason);
        Assert.Equal(ExclusionReason.Conditional, RelationOf(result, 10, 5, 1).ExclusionReason);
        Assert.Equal(ExclusionReason.NotValidated, RelationOf(result, 10, 6).ExclusionReason);
        Assert.Equal(ExclusionReason.Obsolete, RelationOf(result, 10, 7).ExclusionReason);
        Assert.Equal(ExclusionReason.Obsolete, RelationOf(result, 10, 8).ExclusionReason);
        Assert.Equal(ExclusionReason.ExcludedTarget, RelationOf(result, 10, 9).ExclusionReason);
        Assert.Equal(ExclusionReason.Unresolved, RelationOf(result, 10, 10).ExclusionReason);
        Assert.Equal(ExclusionReason.SelfReference, RelationOf(result, 10, 11).ExclusionReason);
        Assert.Null(RelationOf(result, 10, 12).ExclusionReason);
        Assert.Equal(ExclusionReason.ExcludedSource, RelationOf(result, 20, 2).ExclusionReason);
    }

    [Fact]
    public void Hard_and_soft_depend_on_primary_key_membership()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(1, "Target", fields: [Field(1, "Code")]),
            Table(10, "Source", ["Key"], fields: [Field(1, "Key", relation: "Target"), Field(2, "Other", relation: "Target")]),
        ]));

        Assert.Equal(RelationStrength.Hard, RelationOf(result, 10, 1).Strength);
        Assert.Equal(RelationStrength.Soft, RelationOf(result, 10, 2).Strength);
    }

    [Fact]
    public void Warnings_for_unresolved_and_excluded_targets()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(3, "Cust. Ledger Entry", fields: [Field(1, "Entry No.")]),
            Table(
                10, "Source",
                fields:
                [
                    Field(1, "Code"),
                    Field(2, "A", relation: "\"Cust. Ledger Entry\""),
                    Field(3, "B", relation: "\"Cust. Ledger Entry\""),
                    Field(4, "C", relation: "\"Unknown Table\""),
                    Field(5, "D", relation: "\"Unknown Table\""),
                ]),
        ]));

        Assert.Single(result.Issues, i => i.Type == IssueType.DependencyOnExcludedTable);
        Assert.Equal(2, result.Issues.Count(i => i.Type == IssueType.UnresolvedRelationTarget));
    }

    [Fact]
    public void Relation_ids_are_sequential_and_ordered_by_table_then_field()
    {
        var result = Build(Package("a.app", BaseAppId, "Base",
        [
            Table(20, "B", fields: [Field(1, "Code"), Field(2, "X", relation: "A")]),
            Table(10, "A", fields: [Field(1, "Code"), Field(5, "Y", relation: "B"), Field(2, "Z", relation: "B")]),
        ]));

        Assert.Equal([1, 2, 3], result.Relations.Select(r => r.RelationId));
        Assert.Equal([(10, 2), (10, 5), (20, 2)], result.Relations.Select(r => (r.SourceTableId, r.SourceFieldId)));
    }

    [Fact]
    public void Build_is_deterministic_regardless_of_package_order()
    {
        LoadedPackage[] Packages() =>
        [
            Package("a.app", BaseAppId, "Base", [Table(18, "Customer", ["No."], fields: [Field(1, "No."), Field(2, "Bank", relation: "Bank")]), Table(287, "Bank", ["Code"], fields: [Field(1, "Code"), Field(2, "Cust", relation: "Customer")])]),
            Package("b.app", ExtAppId, "Ext", extensions: [Extension(50000, "CustomerExt", "Customer", Field(50000, "X", relation: "Bank"))]),
        ];

        var first = ModelBuilder.Build(Packages(), Categorizer(), Included);
        var second = ModelBuilder.Build(Enumerable.Reverse(Packages()).ToList(), Categorizer(), Included);

        string Describe(AnalysisResult r) => string.Join('\n', r.Relations.Select(x => $"{x.RelationId}:{x.SourceTableId}.{x.SourceFieldId}->{x.TargetTableId}:{x.Strength}:{x.ExclusionReason}"));
        Assert.Equal(Describe(first), Describe(second));
    }
}
