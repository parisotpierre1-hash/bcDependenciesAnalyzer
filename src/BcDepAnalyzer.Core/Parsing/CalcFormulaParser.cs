namespace BcDepAnalyzer.Core.Parsing;

public static class CalcFormulaParser
{
    private static readonly string[] FormulaTypes = ["Lookup", "Sum", "Average", "Count", "Exist", "Min", "Max"];

    public static ParseResult<ParsedCalcFormula> Parse(string text)
    {
        try
        {
            var cursor = new TokenCursor(text);

            if (cursor.PeekSymbol('-'))
            {
                cursor.Next();
            }

            var typeToken = cursor.Peek;
            var formulaType = FormulaTypes.FirstOrDefault(t =>
                typeToken.Kind == AlTokenKind.Identifier && t.Equals(typeToken.Text, StringComparison.OrdinalIgnoreCase));
            if (formulaType is null)
            {
                throw cursor.Error("Unknown formula type.");
            }

            cursor.Next();
            cursor.ExpectSymbol('(');
            var name = cursor.ParseName();
            var where = cursor.ParseOptionalWhere();
            cursor.ExpectSymbol(')');
            cursor.ExpectEnd();

            return ParseResult<ParsedCalcFormula>.Ok(new ParsedCalcFormula(formulaType, name, where));
        }
        catch (AlParseException ex)
        {
            return ParseResult<ParsedCalcFormula>.Fail($"{ex.Message} (offset {ex.Offset})");
        }
    }
}
