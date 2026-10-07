using BcDepAnalyzer.Core.Parsing;

namespace BcDepAnalyzer.Tests.Parsing;

public sealed class CalcFormulaParserTests
{
    private static ParsedCalcFormula ParseOk(string text)
    {
        var result = CalcFormulaParser.Parse(text);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    [Fact]
    public void Sum_with_where_and_upperlimit()
    {
        var parsed = ParseOk(
            "Sum(\"Detailed Cust. Ledg. Entry\".\"Amount (LCY)\" WHERE (\"Customer No.\" = FIELD(\"No.\"), \"Posting Date\" = FIELD(UPPERLIMIT(\"Date Filter\"))))");

        Assert.Equal("Sum", parsed.FormulaType);
        Assert.Equal(["Detailed Cust. Ledg. Entry", "Amount (LCY)"], parsed.NameSegments);
        Assert.Equal("\"Customer No.\" = FIELD(\"No.\"), \"Posting Date\" = FIELD(UPPERLIMIT(\"Date Filter\"))", parsed.WhereText);
    }

    [Fact]
    public void Negated_sum()
    {
        var parsed = ParseOk("-Sum(\"Cust. Ledger Entry\".Amount)");

        Assert.Equal("Sum", parsed.FormulaType);
        Assert.Equal(["Cust. Ledger Entry", "Amount"], parsed.NameSegments);
        Assert.Null(parsed.WhereText);
    }

    [Fact]
    public void Count_with_single_segment()
    {
        var parsed = ParseOk("Count(\"Sales Line\" WHERE (\"Document No.\" = FIELD(\"No.\")))");

        Assert.Equal("Count", parsed.FormulaType);
        Assert.Equal(["Sales Line"], parsed.NameSegments);
    }

    [Fact]
    public void Exist()
    {
        var parsed = ParseOk("Exist(\"Comment Line\" WHERE (\"Table Name\" = CONST(Customer), \"No.\" = FIELD(\"No.\")))");

        Assert.Equal("Exist", parsed.FormulaType);
    }

    [Fact]
    public void Lookup()
    {
        var parsed = ParseOk("Lookup(Customer.Name WHERE (\"No.\" = FIELD(\"Sell-to Customer No.\")))");

        Assert.Equal("Lookup", parsed.FormulaType);
        Assert.Equal(["Customer", "Name"], parsed.NameSegments);
    }

    [Theory]
    [InlineData("Average")]
    [InlineData("Min")]
    [InlineData("Max")]
    public void Other_formula_types_are_case_insensitive(string type)
    {
        var parsed = ParseOk($"{type.ToLowerInvariant()}(Item.\"Unit Cost\")");

        Assert.Equal(type, parsed.FormulaType);
    }

    [Theory]
    [InlineData("Foo(Customer)")]
    [InlineData("Sum(")]
    [InlineData("Sum(Customer.Amount")]
    [InlineData("")]
    public void Invalid_input_returns_error(string text)
    {
        var result = CalcFormulaParser.Parse(text);

        Assert.False(result.IsSuccess);
    }
}
