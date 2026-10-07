using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Model;
using static BcDepAnalyzer.Tests.Analysis.ModelTestHelper;

namespace BcDepAnalyzer.Tests.Analysis;

public sealed class PipelineIntegrationTests
{
    [Fact]
    public void Customer_and_bank_account_form_a_group_with_the_preferred_bank_field_deferred()
    {
        var result = ModelBuilder.Build(
            [
                Package("base.app", BaseAppId, "Base",
                [
                    Table(
                        18, "Customer", ["No."],
                        fields:
                        [
                            Field(1, "No."),
                            Field(30, "Bill-to Customer No.", relation: "Customer"),
                            Field(31, "Preferred Bank Account Code", relation: "\"Customer Bank Account\".Code WHERE (\"Customer No.\" = FIELD(\"No.\"))"),
                        ]),
                    Table(287, "Customer Bank Account", ["Customer No.", "Code"], fields: [Field(1, "Customer No.", relation: "Customer"), Field(2, "Code")]),
                    Table(36, "Sales Header", ["No."], fields: [Field(1, "No."), Field(2, "Sell-to Customer No.", relation: "Customer")]),
                    Table(37, "Sales Line", ["Document No.", "Line No."], fields: [Field(1, "Document No.", relation: "\"Sales Header\""), Field(2, "Line No.", "Integer", null)]),
                    Table(21, "Cust. Ledger Entry", ["Entry No."], fields: [Field(1, "Entry No."), Field(3, "Customer No.", relation: "Customer")]),
                ]),
            ],
            Categorizer(),
            Included);

        DependencyAnalyzer.Analyze(result);

        TableModel T(int id) => result.Tables.Single(t => t.Id == id);
        Assert.True(T(36).MigrationSequence < T(37).MigrationSequence);
        Assert.True(T(18).MigrationSequence < T(287).MigrationSequence);
        Assert.Null(T(21).MigrationSequence);

        var group = Assert.Single(result.DependencyGroups);
        Assert.Equal([18, 287], group.TableIds);
        Assert.False(group.IsUnresolvable);

        Assert.Contains(result.DeferredFields, d => d.TableId == 18 && d.FieldId == 31 && d.Reason == DeferralReason.DependencyGroup);
        Assert.Contains(result.DeferredFields, d => d.TableId == 18 && d.FieldId == 30 && d.Reason == DeferralReason.SelfReference);
        Assert.Equal(2, result.DeferredFields.Count);
    }
}
