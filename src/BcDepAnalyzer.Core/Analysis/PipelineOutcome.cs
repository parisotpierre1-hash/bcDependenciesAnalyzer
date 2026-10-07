using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Analysis;

public sealed record PipelineOutcome(AnalysisResult Result, IReadOnlyList<Issue> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
