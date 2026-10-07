namespace BcDepAnalyzer.Core.Parsing;

public sealed record ParseResult<T> where T : class
{
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public bool IsSuccess => Value is not null;

    public static ParseResult<T> Ok(T value) => new() { Value = value };

    public static ParseResult<T> Fail(string error) => new() { Error = error };
}
