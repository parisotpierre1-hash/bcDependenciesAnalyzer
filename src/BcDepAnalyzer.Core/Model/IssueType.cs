namespace BcDepAnalyzer.Core.Model;

public enum IssueType
{
    UnreadablePackage,
    NoUsableMetadata,
    DuplicateAppId,
    DuplicateTableId,
    DuplicateFieldId,
    MissingDependency,
    OrphanTableExtension,
    UnresolvedRelationTarget,
    RelationParseError,
    DependencyOnExcludedTable,
    HardSelfReference,
    UnresolvableDependencyGroup,
}
