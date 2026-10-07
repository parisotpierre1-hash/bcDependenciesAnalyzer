namespace BcDepAnalyzer.Core.Packaging;

public sealed record AppPackage(
    string FilePath,
    AppManifest Manifest,
    string? ManifestXml,
    string? SymbolReferenceJson,
    IReadOnlyList<SourceFile> SourceFiles,
    IReadOnlyList<string> EntryNames);
