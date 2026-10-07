using System.Text;

namespace BcDepAnalyzer.Core.Parsing;

internal static class AlTokenizer
{
    public static List<AlToken> Tokenize(string text)
    {
        var tokens = new List<AlToken>();
        var i = 0;

        while (true)
        {
            i = SkipTrivia(text, i);
            if (i >= text.Length)
            {
                tokens.Add(new AlToken(AlTokenKind.End, string.Empty, text.Length, text.Length));
                return tokens;
            }

            var start = i;
            var c = text[i];

            if (char.IsLetter(c) || c == '_')
            {
                while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                {
                    i++;
                }

                tokens.Add(new AlToken(AlTokenKind.Identifier, text[start..i], start, i));
            }
            else if (c == '"')
            {
                var close = text.IndexOf('"', i + 1);
                if (close < 0)
                {
                    throw new AlParseException("Unterminated quoted identifier.", start);
                }

                tokens.Add(new AlToken(AlTokenKind.QuotedIdentifier, text.Substring(i + 1, close - i - 1), start, close + 1));
                i = close + 1;
            }
            else if (c == '\'')
            {
                var value = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= text.Length)
                    {
                        throw new AlParseException("Unterminated string literal.", start);
                    }

                    if (text[i] == '\'')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\'')
                        {
                            value.Append('\'');
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    value.Append(text[i]);
                    i++;
                }

                tokens.Add(new AlToken(AlTokenKind.String, value.ToString(), start, i));
            }
            else if (char.IsDigit(c))
            {
                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i++;
                }

                if (i + 1 < text.Length && text[i] == '.' && char.IsDigit(text[i + 1]))
                {
                    i++;
                    while (i < text.Length && char.IsDigit(text[i]))
                    {
                        i++;
                    }
                }

                tokens.Add(new AlToken(AlTokenKind.Number, text[start..i], start, i));
            }
            else
            {
                i++;
                tokens.Add(new AlToken(AlTokenKind.Symbol, c.ToString(), start, i));
            }
        }
    }

    private static int SkipTrivia(string text, int i)
    {
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    throw new AlParseException("Unterminated block comment.", i);
                }

                i = close + 2;
            }
            else if (text[i] == '#')
            {
                // Preprocessor directive (#if, #pragma, ...): symbols keep every branch, so only the line is dropped.
                while (i < text.Length && text[i] != '\n')
                {
                    i++;
                }
            }
            else
            {
                break;
            }
        }

        return i;
    }
}
