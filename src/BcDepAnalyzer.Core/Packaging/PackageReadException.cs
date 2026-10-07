namespace BcDepAnalyzer.Core.Packaging;

public sealed class PackageReadException : Exception
{
    public PackageReadException(string message) : base(message) { }

    public PackageReadException(string message, Exception inner) : base(message, inner) { }
}
