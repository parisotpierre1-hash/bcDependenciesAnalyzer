namespace BcDepAnalyzer.Core.Persistence;

public sealed class DatabaseException(string message, Exception? inner = null) : Exception(message, inner);
