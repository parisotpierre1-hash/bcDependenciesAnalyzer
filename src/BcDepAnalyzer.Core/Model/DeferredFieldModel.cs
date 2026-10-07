namespace BcDepAnalyzer.Core.Model;

public sealed record DeferredFieldModel(
    int TableId,
    int FieldId,
    int RelationId,
    DeferralReason Reason,
    int? DependencyGroupId);
