using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Extraction;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Tests.Reference;

// Runs the full pipeline (no database) on the packages in the AppsSample folder; passes silently when it is absent.
public sealed class ReferenceTests
{
    private static readonly Lazy<(AnalysisResult First, AnalysisResult Second)?> Runs = new(RunTwice);

    private static string? FindSampleFolder()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "AppsSample");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static (AnalysisResult, AnalysisResult)? RunTwice()
    {
        var folder = FindSampleFolder();
        if (folder is null)
        {
            return null;
        }

        var rules = Path.Combine(AppContext.BaseDirectory, "rules", "default-categories.json");
        var options = new AnalysisOptions(InputResolver.Resolve([folder]), rules, [TableCategory.Setup, TableCategory.Data], "test");

        var first = AnalysisPipeline.Run(options, new SymbolExtractor());
        var second = AnalysisPipeline.Run(options, new SymbolExtractor());
        Assert.True(first.Succeeded && second.Succeeded);
        return (first.Result, second.Result);
    }

    private static TableModel Table(AnalysisResult result, string name) => result.Tables.Single(t => t.Name == name && t.Namespace?.StartsWith("Microsoft") != false);

    [Fact]
    public void Sales_line_comes_after_sales_header()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        Assert.True(Table(result, "Sales Header").MigrationSequence < Table(result, "Sales Line").MigrationSequence);
    }

    [Fact]
    public void Customer_and_bank_account_share_a_group_with_the_preferred_bank_field_deferred()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        var customer = Table(result, "Customer");
        var bank = Table(result, "Customer Bank Account");
        Assert.NotNull(customer.DependencyGroupId);
        Assert.Equal(customer.DependencyGroupId, bank.DependencyGroupId);
        Assert.True(customer.Level < bank.Level);

        var field = customer.Fields.Single(f => f.Name == "Preferred Bank Account Code");
        Assert.Contains(result.DeferredFields, d => d.TableId == customer.Id && d.FieldId == field.Id && d.Reason == DeferralReason.DependencyGroup);
    }

    [Fact]
    public void Bill_to_customer_is_a_deferred_self_reference_and_not_a_group()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        var customer = Table(result, "Customer");
        var field = customer.Fields.Single(f => f.Name == "Bill-to Customer No.");
        var relation = result.Relations.Single(r => r.SourceTableId == customer.Id && r.SourceFieldId == field.Id);

        Assert.Equal(ExclusionReason.SelfReference, relation.ExclusionReason);
        Assert.Contains(result.DeferredFields, d => d.TableId == customer.Id && d.FieldId == field.Id && d.Reason == DeferralReason.SelfReference);
        Assert.All(result.DependencyGroups, g => Assert.True(g.TableIds.Count > 1));
    }

    [Fact]
    public void Flow_filter_produces_no_migration_dependency()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        var customer = Table(result, "Customer");
        var field = customer.Fields.Single(f => f.Name == "Global Dimension 1 Filter");

        var relation = Assert.Single(result.Relations, r => r.SourceTableId == customer.Id && r.SourceFieldId == field.Id);
        Assert.False(relation.IsMigrationDependency);
        Assert.Equal(ExclusionReason.FlowFilter, relation.ExclusionReason);
    }

    [Fact]
    public void Ledger_entry_is_categorized_and_left_out_of_the_migration_order()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        var entry = Table(result, "Cust. Ledger Entry");
        Assert.Equal(TableCategory.LedgerEntry, entry.Category);
        Assert.Null(entry.MigrationSequence);
    }

    [Fact]
    public void Flow_field_relations_are_stored_but_are_not_dependencies()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        var customer = Table(result, "Customer");
        var field = customer.Fields.Single(f => f.Name == "Balance (LCY)");

        var relation = Assert.Single(result.Relations, r => r.SourceTableId == customer.Id && r.SourceFieldId == field.Id);
        Assert.Equal(RelationType.FlowField, relation.RelationType);
        Assert.Equal("Detailed Cust. Ledg. Entry", relation.TargetTableName);
        Assert.False(relation.IsMigrationDependency);
    }

    [Fact]
    public void Two_runs_on_the_same_input_give_identical_results()
    {
        if (Runs.Value is not { First: var first, Second: var second })
        {
            return;
        }

        string Describe(AnalysisResult r) => string.Join(
            '\n',
            r.Tables.OrderBy(t => t.Id).Select(t => $"{t.Id}:{t.Level}:{t.MigrationSequence}:{t.DependencyGroupId}:{t.IsUnresolvable}")
                .Concat(r.Relations.Select(x => $"{x.RelationId}:{x.SourceTableId}.{x.SourceFieldId}>{x.TargetTableId}:{x.Strength}:{x.ExclusionReason}:{x.IsDeferred}"))
                .Concat(r.DeferredFields.OrderBy(d => d.TableId).ThenBy(d => d.FieldId).Select(d => $"{d.TableId}.{d.FieldId}:{d.Reason}"))
                .Concat(r.Issues.Select(i => $"{i.IssueId}:{i.Type}:{i.Message}")));

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void Real_packages_produce_no_errors_unresolved_targets_or_orphans()
    {
        if (Runs.Value is not { First: var result })
        {
            return;
        }

        Assert.DoesNotContain(result.Issues, i => i.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(result.Issues, i => i.Type is IssueType.UnresolvedRelationTarget or IssueType.OrphanTableExtension or IssueType.RelationParseError);
        Assert.DoesNotContain(result.DependencyGroups, g => g.IsUnresolvable);
    }
}
