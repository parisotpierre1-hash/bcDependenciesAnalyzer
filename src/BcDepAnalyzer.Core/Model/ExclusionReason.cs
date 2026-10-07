namespace BcDepAnalyzer.Core.Model;

public enum ExclusionReason
{
    FlowField,
    FlowFilter,
    Conditional,
    NotValidated,
    Obsolete,
    ExcludedSource,
    Unresolved,
    ExcludedTarget,
    SelfReference,
}
