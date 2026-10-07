namespace BcDepAnalyzer.Core.Model;

public sealed record RawTableExtension(
    int Id,
    string Name,
    string ExtendedTableName,
    IReadOnlyList<RawField> Fields,
    Guid? ExtendedAppId = null);
