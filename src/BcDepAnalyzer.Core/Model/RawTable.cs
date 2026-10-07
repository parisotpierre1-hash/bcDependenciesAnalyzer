namespace BcDepAnalyzer.Core.Model;

public sealed record RawTable(
    int Id,
    string Name,
    string? Namespace,
    string TableType,
    ObsoleteState ObsoleteState,
    bool DataPerCompany,
    IReadOnlyList<RawField> Fields,
    IReadOnlyList<string> PrimaryKeyFieldNames);
