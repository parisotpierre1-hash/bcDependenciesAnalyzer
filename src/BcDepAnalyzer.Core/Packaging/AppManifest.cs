namespace BcDepAnalyzer.Core.Packaging;

public sealed record AppManifest(
    Guid AppId,
    string Name,
    string Publisher,
    string Version,
    IReadOnlyList<ManifestDependency> Dependencies);
