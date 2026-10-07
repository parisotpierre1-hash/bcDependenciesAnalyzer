using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Model;
using static BcDepAnalyzer.Tests.Analysis.GraphTestHelper;

namespace BcDepAnalyzer.Tests.Analysis;

public sealed class DependencyAnalyzerTests
{
    private static TableModel Get(AnalysisResult result, int id) => result.Tables.Single(t => t.Id == id);

    [Fact]
    public void Linear_chain_gets_increasing_levels()
    {
        var result = Result(
            [Table(1, "A"), Table(2, "B"), Table(3, "C")],
            [Dependency(1, 1, 10, 2, RelationStrength.Hard), Dependency(2, 2, 10, 3, RelationStrength.Hard)]);

        DependencyAnalyzer.Analyze(result);

        Assert.Equal(2, Get(result, 1).Level);
        Assert.Equal(1, Get(result, 2).Level);
        Assert.Equal(0, Get(result, 3).Level);
        Assert.Equal(1, Get(result, 3).MigrationSequence);
        Assert.Equal(2, Get(result, 2).MigrationSequence);
        Assert.Equal(3, Get(result, 1).MigrationSequence);
        Assert.Empty(result.DependencyGroups);
    }

    [Fact]
    public void Diamond_uses_highest_dependency_level()
    {
        var result = Result(
            [Table(1, "A"), Table(2, "B"), Table(3, "C"), Table(4, "D")],
            [
                Dependency(1, 2, 1, 1, RelationStrength.Hard),
                Dependency(2, 3, 1, 1, RelationStrength.Hard),
                Dependency(3, 4, 1, 2, RelationStrength.Hard),
                Dependency(4, 4, 2, 3, RelationStrength.Hard),
            ]);

        DependencyAnalyzer.Analyze(result);

        Assert.Equal([0, 1, 1, 2], result.Tables.OrderBy(t => t.Id).Select(t => t.Level!.Value));
    }

    [Fact]
    public void Same_level_tables_are_ordered_by_table_id()
    {
        var result = Result([Table(30, "C"), Table(10, "A"), Table(20, "B")], []);

        DependencyAnalyzer.Analyze(result);

        Assert.Equal(1, Get(result, 10).MigrationSequence);
        Assert.Equal(2, Get(result, 20).MigrationSequence);
        Assert.Equal(3, Get(result, 30).MigrationSequence);
    }

    [Fact]
    public void Soft_cycle_is_broken_by_deferring_the_soft_field()
    {
        const int customer = 18;
        const int bankAccount = 287;
        var result = Result(
            [Table(customer, "Customer"), Table(bankAccount, "Customer Bank Account")],
            [
                Dependency(1, customer, 100, bankAccount, RelationStrength.Soft),
                Dependency(2, bankAccount, 2, customer, RelationStrength.Hard),
            ]);

        DependencyAnalyzer.Analyze(result);

        var group = Assert.Single(result.DependencyGroups);
        Assert.Equal(1, group.Id);
        Assert.Equal([customer, bankAccount], group.TableIds);
        Assert.False(group.IsUnresolvable);

        var deferred = Assert.Single(result.DeferredFields);
        Assert.Equal(new DeferredFieldModel(customer, 100, 1, DeferralReason.DependencyGroup, 1), deferred);
        Assert.True(result.Relations.Single(r => r.RelationId == 1).IsDeferred);
        Assert.False(result.Relations.Single(r => r.RelationId == 2).IsDeferred);

        Assert.Equal(0, Get(result, customer).Level);
        Assert.Equal(1, Get(result, bankAccount).Level);
        Assert.Equal(1, Get(result, customer).DependencyGroupId);
        Assert.False(Get(result, customer).IsUnresolvable);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Hard_cycle_is_unresolvable_and_dependents_still_get_a_level()
    {
        var result = Result(
            [Table(1, "A"), Table(2, "B"), Table(3, "C")],
            [
                Dependency(1, 1, 1, 2, RelationStrength.Hard),
                Dependency(2, 2, 1, 1, RelationStrength.Hard),
                Dependency(3, 3, 5, 1, RelationStrength.Soft),
            ]);

        DependencyAnalyzer.Analyze(result);

        Assert.Null(Get(result, 1).Level);
        Assert.Null(Get(result, 2).Level);
        Assert.True(Get(result, 1).IsUnresolvable);
        Assert.True(Get(result, 2).IsUnresolvable);
        Assert.Null(Get(result, 1).MigrationSequence);
        Assert.Equal(1, Get(result, 3).Level);
        Assert.Equal(1, Get(result, 3).MigrationSequence);

        Assert.True(Assert.Single(result.DependencyGroups).IsUnresolvable);
        Assert.Empty(result.DeferredFields);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.UnresolvableDependencyGroup, issue.Type);
        Assert.Equal(IssueSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Group_with_soft_and_hard_cycles_defers_soft_and_reports_remaining_hard_cycle()
    {
        var result = Result(
            [Table(1, "A"), Table(2, "B"), Table(3, "C")],
            [
                Dependency(1, 1, 1, 2, RelationStrength.Hard),
                Dependency(2, 2, 1, 1, RelationStrength.Hard),
                Dependency(3, 2, 2, 3, RelationStrength.Soft),
                Dependency(4, 3, 1, 2, RelationStrength.Hard),
            ]);

        DependencyAnalyzer.Analyze(result);

        var group = Assert.Single(result.DependencyGroups);
        Assert.Equal([1, 2, 3], group.TableIds);
        Assert.True(group.IsUnresolvable);
        Assert.True(Get(result, 1).IsUnresolvable);
        Assert.True(Get(result, 2).IsUnresolvable);
        Assert.False(Get(result, 3).IsUnresolvable);
        Assert.Equal(1, Get(result, 3).Level);
        Assert.Equal(new DeferredFieldModel(2, 2, 3, DeferralReason.DependencyGroup, 1), Assert.Single(result.DeferredFields));
    }

    [Fact]
    public void Soft_self_reference_is_deferred_without_creating_a_group()
    {
        var result = Result([Table(18, "Customer")], [SelfReference(1, 18, 30, RelationStrength.Soft)]);

        DependencyAnalyzer.Analyze(result);

        Assert.Empty(result.DependencyGroups);
        Assert.Equal(new DeferredFieldModel(18, 30, 1, DeferralReason.SelfReference, null), Assert.Single(result.DeferredFields));
        Assert.True(result.Relations[0].IsDeferred);
        Assert.Equal(0, Get(result, 18).Level);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Hard_self_reference_raises_a_warning()
    {
        var table = Table(18, "Customer");
        var result = Result([table], [SelfReference(1, 18, 1, RelationStrength.Hard)]);

        DependencyAnalyzer.Analyze(result);

        Assert.Empty(result.DeferredFields);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(IssueType.HardSelfReference, issue.Type);
        Assert.Equal("Customer", issue.ObjectName);
        Assert.Equal(0, Get(result, 18).Level);
    }

    [Fact]
    public void Excluded_categories_and_removed_tables_are_not_part_of_the_graph()
    {
        var result = Result(
            [
                Table(1, "Customer"),
                Table(2, "Cust. Ledger Entry", TableCategory.LedgerEntry),
                Table(3, "Old Thing", obsolete: ObsoleteState.Removed),
            ],
            []);

        DependencyAnalyzer.Analyze(result);

        Assert.Equal(0, Get(result, 1).Level);
        Assert.Null(Get(result, 2).Level);
        Assert.Null(Get(result, 2).MigrationSequence);
        Assert.Null(Get(result, 3).Level);
    }

    [Fact]
    public void Long_chain_does_not_overflow_the_stack()
    {
        const int size = 10_000;
        var tables = Enumerable.Range(1, size).Select(i => Table(i, $"T{i}")).ToList();
        var relations = Enumerable.Range(1, size - 1).Select(i => Dependency(i, i + 1, 1, i, RelationStrength.Hard)).ToList();
        var result = Result(tables, relations);

        DependencyAnalyzer.Analyze(result);

        Assert.Equal(0, Get(result, 1).Level);
        Assert.Equal(size - 1, Get(result, size).Level);
    }

    [Fact]
    public void Analysis_does_not_depend_on_input_order()
    {
        RelationModel[] Relations() =>
        [
            Dependency(1, 18, 100, 287, RelationStrength.Soft),
            Dependency(2, 287, 2, 18, RelationStrength.Hard),
            Dependency(3, 36, 1, 18, RelationStrength.Hard),
            Dependency(4, 37, 1, 36, RelationStrength.Hard),
            Dependency(5, 50, 1, 51, RelationStrength.Hard),
            Dependency(6, 51, 1, 50, RelationStrength.Hard),
            SelfReference(7, 18, 30, RelationStrength.Soft),
        ];

        TableModel[] Tables() => [Table(18, "Customer"), Table(287, "Bank"), Table(36, "Header"), Table(37, "Line"), Table(50, "X"), Table(51, "Y")];

        var first = Result(Tables(), Relations());
        var second = Result(Enumerable.Reverse(Tables()), Enumerable.Reverse(Relations()));

        DependencyAnalyzer.Analyze(first);
        DependencyAnalyzer.Analyze(second);

        Assert.Equal(Summarize(first), Summarize(second));
    }

    [Fact]
    public void Analysis_can_run_twice_with_identical_result()
    {
        var result = Result(
            [Table(1, "A"), Table(2, "B")],
            [Dependency(1, 1, 1, 2, RelationStrength.Soft), Dependency(2, 2, 1, 1, RelationStrength.Hard)]);

        DependencyAnalyzer.Analyze(result);
        var firstRun = Summarize(result);
        DependencyAnalyzer.Analyze(result);

        Assert.Equal(firstRun, Summarize(result));
    }
}
