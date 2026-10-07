namespace BcDepAnalyzer.Core.Packaging;

public sealed record ManifestDependency(Guid AppId, string Name, string Publisher, string MinVersion);
