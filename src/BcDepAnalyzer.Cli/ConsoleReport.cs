using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Cli;

public static class ConsoleReport
{
    public static void PrintErrors(IEnumerable<Issue> errors)
    {
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"{error.Severity,-7} {error.Type} {error.AppName} {error.ObjectName}: {error.Message}");
        }
    }

    public static void PrintSummary(AnalysisResult result)
    {
        var included = result.IncludedCategories.ToHashSet();
        var levels = result.Tables.Where(t => t.Level is not null).Select(t => t.Level!.Value).ToList();

        Console.WriteLine($"Apps analyzed:          {result.Apps.Count}");
        Console.WriteLine($"Tables:                 {result.Tables.Count} (included: {result.Tables.Count(t => included.Contains(t.Category))})");
        Console.WriteLine($"Fields:                 {result.Tables.Sum(t => t.Fields.Count)}");
        Console.WriteLine($"Relations:              {result.Relations.Count} (migration dependencies: {result.Relations.Count(r => r.IsMigrationDependency)})");
        Console.WriteLine($"Dependency groups:      {result.DependencyGroups.Count} (unresolvable: {result.DependencyGroups.Count(g => g.IsUnresolvable)})");
        Console.WriteLine($"Deferred fields:        {result.DeferredFields.Count}");
        Console.WriteLine(levels.Count == 0 ? "Levels:                 none" : $"Levels:                 0..{levels.Max()}");
        Console.WriteLine("Issues:");

        if (result.Issues.Count == 0)
        {
            Console.WriteLine("  none");
        }

        foreach (var group in result.Issues.GroupBy(i => (i.Severity, i.Type)).OrderBy(g => g.Key.Severity).ThenBy(g => g.Key.Type))
        {
            Console.WriteLine($"  {group.Key.Severity,-8}{group.Key.Type,-32}{group.Count()}");
        }
    }
}
