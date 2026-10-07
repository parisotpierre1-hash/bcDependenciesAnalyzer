namespace BcDepAnalyzer.Core.Extraction;

public sealed class MetadataExtractionException(string message, Exception? inner = null) : Exception(message, inner);
