namespace BcDepAnalyzer.Core.Export;

public sealed record GraphMlOptions(
    IReadOnlyList<string> Apps,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tables,
    int Depth,
    bool AllRelations);
