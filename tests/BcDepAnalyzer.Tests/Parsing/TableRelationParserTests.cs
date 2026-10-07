using BcDepAnalyzer.Core.Parsing;

namespace BcDepAnalyzer.Tests.Parsing;

public sealed class TableRelationParserTests
{
    private static ParsedTableRelation ParseOk(string text)
    {
        var result = TableRelationParser.Parse(text);
        Assert.True(result.IsSuccess, result.Error);
        return result.Value!;
    }

    [Fact]
    public void Simple_target()
    {
        var parsed = ParseOk("Customer");

        Assert.False(parsed.IsConditional);
        var branch = Assert.Single(parsed.Branches);
        Assert.Equal(["Customer"], branch.NameSegments);
        Assert.Null(branch.ConditionText);
        Assert.Null(branch.WhereText);
    }

    [Fact]
    public void Quoted_target_with_field_and_where()
    {
        var parsed = ParseOk("\"Customer Bank Account\".Code WHERE (\"Customer No.\" = FIELD(\"Bill-to Customer No.\"))");

        var branch = Assert.Single(parsed.Branches);
        Assert.Equal(["Customer Bank Account", "Code"], branch.NameSegments);
        Assert.Equal("\"Customer No.\" = FIELD(\"Bill-to Customer No.\")", branch.WhereText);
    }

    [Fact]
    public void Conditional_with_two_branches()
    {
        var parsed = ParseOk("IF (Type = CONST(Customer)) Customer ELSE IF (Type = CONST(Vendor)) Vendor");

        Assert.True(parsed.IsConditional);
        Assert.Equal(2, parsed.Branches.Count);
        Assert.Equal("Type = CONST(Customer)", parsed.Branches[0].ConditionText);
        Assert.Equal(["Customer"], parsed.Branches[0].NameSegments);
        Assert.Equal("Type = CONST(Vendor)", parsed.Branches[1].ConditionText);
        Assert.Equal(["Vendor"], parsed.Branches[1].NameSegments);
    }

    [Fact]
    public void Conditional_with_three_branches_and_where_on_second()
    {
        var parsed = ParseOk(
            "IF (Type = CONST(\" \")) \"Standard Text\" ELSE IF (Type = CONST(\"G/L Account\")) \"G/L Account\" WHERE (\"Direct Posting\" = CONST(true)) ELSE IF (Type = CONST(Item)) Item");

        Assert.True(parsed.IsConditional);
        Assert.Equal(3, parsed.Branches.Count);
        Assert.Null(parsed.Branches[0].WhereText);
        Assert.Equal("\"Direct Posting\" = CONST(true)", parsed.Branches[1].WhereText);
        Assert.Equal(["Item"], parsed.Branches[2].NameSegments);
    }

    [Fact]
    public void Conditional_with_single_branch()
    {
        var parsed = ParseOk("IF (Type = CONST(Customer)) Customer");

        Assert.True(parsed.IsConditional);
        Assert.Single(parsed.Branches);
    }

    [Fact]
    public void Final_else_branch_has_no_condition()
    {
        var parsed = ParseOk("IF (Type = CONST(Customer)) Customer ELSE Vendor");

        Assert.Equal(2, parsed.Branches.Count);
        Assert.Null(parsed.Branches[1].ConditionText);
        Assert.Equal(["Vendor"], parsed.Branches[1].NameSegments);
    }

    [Fact]
    public void Keywords_are_case_insensitive()
    {
        var parsed = ParseOk("if (type = const(customer)) customer else vendor");

        Assert.Equal(2, parsed.Branches.Count);
    }

    [Fact]
    public void Trailing_semicolon_is_ignored()
    {
        var parsed = ParseOk("Item.\"No.\" WHERE (Type = CONST(Inventory));");

        var branch = Assert.Single(parsed.Branches);
        Assert.Equal(["Item", "No."], branch.NameSegments);
    }

    [Fact]
    public void Where_with_comma()
    {
        var parsed = ParseOk("\"Dimension Value\".Code WHERE (\"Global Dimension No.\" = CONST(1), Blocked = CONST(false))");

        Assert.Equal("\"Global Dimension No.\" = CONST(1), Blocked = CONST(false)", parsed.Branches[0].WhereText);
    }

    [Fact]
    public void Parentheses_inside_strings_are_ignored()
    {
        var parsed = ParseOk("Customer WHERE (Name = FILTER('<>''(x)'''))");

        Assert.Equal("Name = FILTER('<>''(x)''')", parsed.Branches[0].WhereText);
    }

    [Fact]
    public void Comments_are_ignored()
    {
        var parsed = ParseOk("// leading\nCustomer /* inline */ WHERE (Blocked = CONST(false)) // trailing");

        var branch = Assert.Single(parsed.Branches);
        Assert.Equal(["Customer"], branch.NameSegments);
        Assert.Equal("Blocked = CONST(false)", branch.WhereText);
    }

    [Fact]
    public void Namespace_qualified_name()
    {
        var parsed = ParseOk("Microsoft.Sales.Customer.Customer");

        Assert.Equal(["Microsoft", "Sales", "Customer", "Customer"], parsed.Branches[0].NameSegments);
    }

    [Fact]
    public void Preprocessor_directives_are_ignored_and_all_branches_are_kept()
    {
        var parsed = ParseOk(
            "if (Type = const(Item)) Item\r\n            else #IF BC23_Plus\r\n            if (Type = const(\"Allocation Account\")) \"Allocation Account\"\r\n            else #ENDIF\r\n            if (Type = const(Resource)) Resource");

        Assert.Equal(3, parsed.Branches.Count);
        Assert.Equal(["Allocation Account"], parsed.Branches[1].NameSegments);
    }

    [Fact]
    public void Pragma_lines_inside_a_chain_are_ignored()
    {
        var parsed = ParseOk(
            "if (A = const(1)) \"Production Order\".\"No.\" where(Status = field(\"S\"))\r\n #pragma warning restore AL0603\r\n else if (A = const(2)) Job");

        Assert.Equal(2, parsed.Branches.Count);
    }

    [Theory]
    [InlineData("IF (")]
    [InlineData("")]
    [InlineData("Customer Vendor")]
    [InlineData("\"Customer")]
    [InlineData("IF (Type = CONST(Customer)) ")]
    [InlineData("Customer WHERE (Blocked = CONST(false)")]
    public void Invalid_input_returns_error(string text)
    {
        var result = TableRelationParser.Parse(text);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }
}
