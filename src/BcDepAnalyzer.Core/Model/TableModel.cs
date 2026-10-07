namespace BcDepAnalyzer.Core.Model;

public sealed class TableModel
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public string? Namespace { get; init; }
    public required Guid AppId { get; init; }
    public required string TableType { get; init; }
    public required ObsoleteState ObsoleteState { get; init; }
    public bool DataPerCompany { get; init; } = true;
    public TableCategory Category { get; set; } = TableCategory.Data;
    public List<FieldModel> Fields { get; } = [];
    public List<int> PrimaryKeyFieldIds { get; } = [];
    public int? Level { get; set; }
    public int? MigrationSequence { get; set; }
    public int? DependencyGroupId { get; set; }
    public bool IsUnresolvable { get; set; }
}
