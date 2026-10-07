using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Model;

public sealed record AppModel(
    Guid AppId,
    string Name,
    string Publisher,
    string Version,
    string FilePath,
    IReadOnlyList<ManifestDependency> Dependencies);
