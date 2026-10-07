using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Tests.Categorization;

public sealed class TableCategorizerTests
{
    private static CategoryRules DefaultRules() =>
        CategoryRulesLoader.LoadFile(Path.Combine(AppContext.BaseDirectory, "rules", "default-categories.json"));

    private static TableCategorizer Create(CategoryRules? rules = null) => new(rules ?? DefaultRules());

    [Theory]
    [InlineData(18, "Customer", "Normal", TableCategory.Data)]
    [InlineData(18, "Customer", null, TableCategory.Data)]
    [InlineData(98, "General Ledger Setup", "Normal", TableCategory.Setup)]
    [InlineData(21, "Cust. Ledger Entry", "Normal", TableCategory.LedgerEntry)]
    [InlineData(17, "G/L Entry", "Normal", TableCategory.LedgerEntry)]
    [InlineData(110, "Sales Shipment Header", "Normal", TableCategory.PostedDocument)]
    [InlineData(112, "Sales Invoice Header", "Normal", TableCategory.PostedDocument)]
    [InlineData(5000, "Sales Header Archive", "Normal", TableCategory.Archive)]
    [InlineData(5001, "Import Buffer", "Normal", TableCategory.Buffer)]
    [InlineData(5002, "Job Queue Log Entry", "Normal", TableCategory.Log)]
    [InlineData(2000000120, "User", "Normal", TableCategory.System)]
    [InlineData(5003, "Anything", "Temporary", TableCategory.Temporary)]
    [InlineData(5004, "Dataverse Thing", "CRM", TableCategory.External)]
    [InlineData(5005, "Other Thing", "ExternalSQL", TableCategory.External)]
    public void Categorizes_with_default_rules(int id, string name, string? tableType, TableCategory expected)
    {
        Assert.Equal(expected, Create().Categorize(id, name, tableType));
    }

    [Fact]
    public void Override_wins_over_structural_rules()
    {
        var rules = new CategoryRules([], [new CategoryOverride(2000000120, TableCategory.Data)]);

        Assert.Equal(TableCategory.Data, Create(rules).Categorize(2000000120, "User", "Normal"));
    }

    [Fact]
    public void Structural_rules_win_over_name_rules()
    {
        Assert.Equal(TableCategory.Temporary, Create().Categorize(5006, "Foo Setup", "Temporary"));
    }

    [Fact]
    public void First_matching_name_rule_wins()
    {
        var rules = new CategoryRules(
            [new NameRule("* Log", TableCategory.Log), new NameRule("Job *", TableCategory.Setup)],
            []);

        Assert.Equal(TableCategory.Log, Create(rules).Categorize(1, "Job Queue Log", "Normal"));
    }

    [Theory]
    [InlineData("* Setup", "General Ledger Setup", true)]
    [InlineData("* Setup", "Setup", false)]
    [InlineData("* setup", "GENERAL LEDGER SETUP", true)]
    [InlineData("G/L Entry", "G/L Entry", true)]
    [InlineData("G/L Entry", "G/L Entries", false)]
    [InlineData("Sales Cr.Memo *", "Sales Cr.Memo Header", true)]
    [InlineData("Sales Cr.Memo *", "Sales CrXMemo Header", false)]
    [InlineData("*", "Anything", true)]
    [InlineData("A*B*C", "AxxBxxC", true)]
    [InlineData("A*B*C", "AxxBxx", false)]
    public void Wildcard_matching(string pattern, string text, bool expected)
    {
        Assert.Equal(expected, TableCategorizer.WildcardMatch(pattern, text));
    }

    [Fact]
    public void Unknown_category_in_rules_file_is_a_configuration_error()
    {
        const string json = "{ \"nameRules\": [ { \"pattern\": \"* X\", \"category\": \"Nope\" } ] }";

        Assert.Throws<ConfigurationException>(() => CategoryRulesLoader.Parse(json));
    }

    [Fact]
    public void Rules_file_parses_overrides_and_comments()
    {
        const string json = """
            {
              // comment
              "nameRules": [ { "pattern": "* X", "category": "Log" } ],
              "overrides": [ { "tableId": 42, "category": "Data", "comment": "keep" } ]
            }
            """;

        var rules = CategoryRulesLoader.Parse(json);

        Assert.Single(rules.NameRules);
        Assert.Equal(new CategoryOverride(42, TableCategory.Data, "keep"), Assert.Single(rules.Overrides));
    }

    [Fact]
    public void Missing_rules_file_is_a_configuration_error()
    {
        Assert.Throws<ConfigurationException>(() => CategoryRulesLoader.LoadFile(Path.Combine(Path.GetTempPath(), "nope-" + Guid.NewGuid() + ".json")));
    }
}
