namespace BcDepAnalyzer.Core.Parsing;

internal sealed class AlParseException(string message, int offset) : Exception(message)
{
    public int Offset { get; } = offset;
}
