using System.CommandLine;
using System.Reflection;
using BcDepAnalyzer.Core.Analysis;
using BcDepAnalyzer.Core.Extraction;
using BcDepAnalyzer.Core.Persistence;

namespace BcDepAnalyzer.Cli.Commands;

public static class AnalyzeCommand
{
    public static Command Create()
    {
        var apps = new Option<string[]>("--apps") { Description = "An .app file or a folder; repeatable", Required = true, AllowMultipleArgumentsPerToken = true };
        var rules = new Option<string?>("--rules") { Description = "Categorization rules file (default: from configuration)" };
        var categories = new Option<string?>("--include-categories") { Description = "Comma-separated table categories to include in the migration order" };
        var connection = new Option<string?>("--connection") { Description = "SQL Server connection string (default: from configuration)" };

        var command = new Command("analyze", "Analyze .app packages and store the model in SQL Server")
        {
            apps, rules, categories, connection,
        };

        command.SetAction(parseResult => ExitCodes.Run(() =>
        {
            var config = AppConfig.LoadDefault();
            var options = new AnalysisOptions(
                InputResolver.Resolve(parseResult.GetRequiredValue(apps)),
                config.ResolveRulesFile(parseResult.GetValue(rules), AppContext.BaseDirectory),
                config.ResolveIncludedCategories(parseResult.GetValue(categories)),
                typeof(AnalyzeCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
            var connectionString = config.ResolveConnectionString(parseResult.GetValue(connection));

            var outcome = AnalysisPipeline.Run(options, new SymbolExtractor(), message => Console.WriteLine(message));
            if (!outcome.Succeeded)
            {
                ConsoleReport.PrintErrors(outcome.Errors);
                return ExitCodes.AnalysisError;
            }

            Console.WriteLine("Saving to the database");
            new AnalysisRepository(connectionString).Save(outcome.Result);

            ConsoleReport.PrintSummary(outcome.Result);
            return ExitCodes.Success;
        }));

        return command;
    }
}
