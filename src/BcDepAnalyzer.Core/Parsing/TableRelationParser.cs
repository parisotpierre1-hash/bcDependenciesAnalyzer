namespace BcDepAnalyzer.Core.Parsing;

public static class TableRelationParser
{
    public static ParseResult<ParsedTableRelation> Parse(string text)
    {
        try
        {
            var cursor = new TokenCursor(text);
            var result = cursor.PeekKeyword("IF")
                ? ParseIfChain(cursor)
                : new ParsedTableRelation(false, [ParseTarget(cursor, null)]);
            cursor.ExpectEnd();
            return ParseResult<ParsedTableRelation>.Ok(result);
        }
        catch (AlParseException ex)
        {
            return ParseResult<ParsedTableRelation>.Fail($"{ex.Message} (offset {ex.Offset})");
        }
    }

    private static ParsedTableRelation ParseIfChain(TokenCursor cursor)
    {
        var branches = new List<ParsedBranch>();

        while (true)
        {
            cursor.ExpectKeyword("IF");
            cursor.ExpectSymbol('(');
            var condition = cursor.CaptureBalanced();
            branches.Add(ParseTarget(cursor, condition));

            if (!cursor.PeekKeyword("ELSE"))
            {
                break;
            }

            cursor.Next();
            if (!cursor.PeekKeyword("IF"))
            {
                branches.Add(ParseTarget(cursor, null));
                break;
            }
        }

        return new ParsedTableRelation(true, branches);
    }

    private static ParsedBranch ParseTarget(TokenCursor cursor, string? condition)
    {
        var name = cursor.ParseName();
        var where = cursor.ParseOptionalWhere();
        return new ParsedBranch(name, condition, where);
    }
}
