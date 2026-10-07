using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Extraction;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Analysis;

public static class AnalysisPipeline
{
    // Never touches the database: persistence is the caller's job, and only when the outcome succeeded.
    public static PipelineOutcome Run(AnalysisOptions options, IMetadataExtractor extractor, Action<string>? log = null)
    {
        var rules = CategoryRulesLoader.LoadFile(options.RulesFile);
        var errors = new List<Issue>();
        var loaded = new List<LoadedPackage>();

        foreach (var file in options.AppFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            log?.Invoke($"Reading {Path.GetFileName(file)}");
            try
            {
                var package = AppPackageReader.Read(file);
                loaded.Add(new LoadedPackage(file, extractor.Extract(package)));
            }
            catch (PackageReadException ex)
            {
                errors.Add(new Issue(IssueSeverity.Error, IssueType.UnreadablePackage, null, Path.GetFileName(file), ex.Message));
            }
            catch (MetadataExtractionException ex)
            {
                errors.Add(new Issue(IssueSeverity.Error, IssueType.NoUsableMetadata, null, Path.GetFileName(file), ex.Message));
            }
        }

        if (errors.Count > 0)
        {
            return new PipelineOutcome(new AnalysisResult(), errors);
        }

        log?.Invoke("Building the model");
        var result = ModelBuilder.Build(loaded, new TableCategorizer(rules), options.IncludedCategories);

        var buildErrors = result.Issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
        if (buildErrors.Count > 0)
        {
            return new PipelineOutcome(result, buildErrors);
        }

        log?.Invoke("Analyzing dependencies");
        DependencyAnalyzer.Analyze(result);

        var sorted = result.Issues
            .OrderBy(i => i.Severity)
            .ThenBy(i => i.Type)
            .ThenBy(i => i.AppName, StringComparer.Ordinal)
            .ThenBy(i => i.ObjectName, StringComparer.Ordinal)
            .ThenBy(i => i.Message, StringComparer.Ordinal)
            .Select((issue, index) => issue with { IssueId = index + 1 })
            .ToList();
        result.Issues.Clear();
        result.Issues.AddRange(sorted);

        result.InputFiles = options.AppFiles.ToList();
        result.RulesFile = options.RulesFile;
        result.AnalysisDate = DateTime.UtcNow;
        result.ToolVersion = options.ToolVersion;

        return new PipelineOutcome(result, []);
    }
}
