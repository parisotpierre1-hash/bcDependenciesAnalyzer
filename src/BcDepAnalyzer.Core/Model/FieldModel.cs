namespace BcDepAnalyzer.Core.Model;

public sealed class FieldModel
{
    public required int TableId { get; init; }
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string DataType { get; init; }
    public int? Length { get; init; }
    public required FieldClass FieldClass { get; init; }
    public required ObsoleteState ObsoleteState { get; init; }
    public required Guid AppId { get; init; }
    public required string SourceObjectType { get; init; }
    public required int SourceObjectId { get; init; }
    public required string SourceObjectName { get; init; }
    public string? TableRelationText { get; init; }
    public bool ValidateTableRelation { get; init; } = true;
    public string? CalcFormulaText { get; init; }
    public bool IsPrimaryKey { get; set; }
}
