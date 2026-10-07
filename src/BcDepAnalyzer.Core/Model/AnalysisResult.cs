namespace BcDepAnalyzer.Core.Model;

public sealed class AnalysisResult
{
    public List<AppModel> Apps { get; } = [];
    public List<TableModel> Tables { get; } = [];
    public List<TableExtensionModel> TableExtensions { get; } = [];
    public List<RelationModel> Relations { get; } = [];
    public List<DependencyGroupModel> DependencyGroups { get; } = [];
    public List<DeferredFieldModel> DeferredFields { get; } = [];
    public List<Issue> Issues { get; } = [];
    public IReadOnlyList<TableCategory> IncludedCategories { get; set; } = [];
    public IReadOnlyList<string> InputFiles { get; set; } = [];
    public string RulesFile { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; }
    public string ToolVersion { get; set; } = string.Empty;
}
