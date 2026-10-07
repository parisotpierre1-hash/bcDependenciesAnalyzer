namespace BcDepAnalyzer.Core.Parsing;

internal sealed class TokenCursor
{
    private readonly string _text;
    private readonly List<AlToken> _tokens;
    private int _position;

    public TokenCursor(string text)
    {
        _text = text;
        _tokens = AlTokenizer.Tokenize(text);
    }

    public AlToken Peek => _tokens[_position];

    public AlToken Next()
    {
        var token = _tokens[_position];
        if (token.Kind != AlTokenKind.End)
        {
            _position++;
        }

        return token;
    }

    public bool PeekKeyword(string keyword) =>
        Peek.Kind == AlTokenKind.Identifier && Peek.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase);

    public bool PeekSymbol(char symbol) =>
        Peek.Kind == AlTokenKind.Symbol && Peek.Text[0] == symbol;

    public void ExpectKeyword(string keyword)
    {
        if (!PeekKeyword(keyword))
        {
            throw Error($"Expected '{keyword}'.");
        }

        Next();
    }

    public void ExpectSymbol(char symbol)
    {
        if (!PeekSymbol(symbol))
        {
            throw Error($"Expected '{symbol}'.");
        }

        Next();
    }

    public void ExpectEnd()
    {
        if (PeekSymbol(';'))
        {
            Next();
        }

        if (Peek.Kind != AlTokenKind.End)
        {
            throw Error("Unexpected trailing content.");
        }
    }

    public List<string> ParseName()
    {
        var segments = new List<string> { ParseNamePart() };
        while (PeekSymbol('.'))
        {
            Next();
            segments.Add(ParseNamePart());
        }

        return segments;
    }

    public string? ParseOptionalWhere()
    {
        if (!PeekKeyword("WHERE"))
        {
            return null;
        }

        Next();
        ExpectSymbol('(');
        return CaptureBalanced();
    }

    // Call right after consuming '('; consumes through the matching ')' and returns the raw inner text.
    public string CaptureBalanced()
    {
        var start = _tokens[_position - 1].End;
        var depth = 1;

        while (true)
        {
            var token = Next();
            if (token.Kind == AlTokenKind.End)
            {
                throw new AlParseException("Missing closing parenthesis.", token.Start);
            }

            if (token.Kind != AlTokenKind.Symbol)
            {
                continue;
            }

            if (token.Text[0] == '(')
            {
                depth++;
            }
            else if (token.Text[0] == ')' && --depth == 0)
            {
                return _text[start..token.Start].Trim();
            }
        }
    }

    public AlParseException Error(string message) => new(message, Peek.Start);

    private string ParseNamePart()
    {
        var token = Peek;
        if (token.Kind is not (AlTokenKind.Identifier or AlTokenKind.QuotedIdentifier))
        {
            throw Error("Expected a name.");
        }

        Next();
        return token.Text;
    }
}
