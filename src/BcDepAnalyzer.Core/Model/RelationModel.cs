namespace BcDepAnalyzer.Core.Model;

public sealed class RelationModel
{
    public int RelationId { get; set; }
    public required int SourceTableId { get; init; }
    public required int SourceFieldId { get; init; }
    public required RelationType RelationType { get; init; }
    public string? FormulaType { get; init; }
    public string? TargetTableName { get; set; }
    public int? TargetTableId { get; set; }
    public string? TargetFieldName { get; set; }
    public bool IsConditional { get; init; }
    public int BranchIndex { get; init; }
    public string? ConditionText { get; init; }
    public string? WhereText { get; init; }
    public required string RelationText { get; init; }
    public RelationStrength? Strength { get; set; }
    public bool IsMigrationDependency { get; set; }
    public ExclusionReason? ExclusionReason { get; set; }
    public bool IsDeferred { get; set; }
}
