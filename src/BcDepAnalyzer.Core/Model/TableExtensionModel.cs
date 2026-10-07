namespace BcDepAnalyzer.Core.Model;

public sealed record TableExtensionModel(
    Guid AppId,
    int Id,
    string Name,
    string ExtendedTableName,
    int? ExtendedTableId,
    bool IsOrphan);
