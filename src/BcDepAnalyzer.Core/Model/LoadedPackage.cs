using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Model;

public sealed record LoadedPackage(string FilePath, ExtractedPackage Package);
