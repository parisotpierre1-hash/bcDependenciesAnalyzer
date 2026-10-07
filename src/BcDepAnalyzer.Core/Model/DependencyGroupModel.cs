namespace BcDepAnalyzer.Core.Model;

public sealed class DependencyGroupModel
{
    public required int Id { get; init; }
    public required IReadOnlyList<int> TableIds { get; init; }
    public bool IsUnresolvable { get; set; }
}
