namespace BcDepAnalyzer.Core.Model;

public sealed record RawField(
    int Id,
    string Name,
    string DataType,
    int? Length,
    FieldClass FieldClass,
    ObsoleteState ObsoleteState,
    string? TableRelationText,
    bool ValidateTableRelation,
    string? CalcFormulaText);
