using System.CommandLine;
using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Export;

namespace BcDepAnalyzer.Cli.Commands;

public static class ExportCommand
{
    public static Command Create()
    {
        var connection = new Option<string?>("--connection") { Description = "SQL Server connection string (default: from configuration)" };

        var csvOut = new Option<DirectoryInfo>("--out") { Description = "Output folder", Required = true };
        var delimiter = new Option<string?>("--delimiter") { Description = "CSV delimiter (default: from configuration)" };
        var csv = new Command("csv", "Export the stored analysis to CSV files") { csvOut, delimiter, connection };
        csv.SetAction(parseResult => ExitCodes.Run(() =>
        {
            var config = AppConfig.LoadDefault();
            var reader = new AnalysisReader(config.ResolveConnectionString(parseResult.GetValue(connection)));
            var files = CsvExporter.Export(reader, parseResult.GetRequiredValue(csvOut).FullName, config.ResolveDelimiter(parseResult.GetValue(delimiter))[0]);

            foreach (var file in files)
            {
                Console.WriteLine(file);
            }

            return ExitCodes.Success;
        }));

        var graphOut = new Option<FileInfo>("--out") { Description = "Output .graphml file", Required = true };
        var apps = new Option<string[]>("--app") { Description = "Origin apps (name or app ID); comma-separated or repeated", AllowMultipleArgumentsPerToken = true };
        var categories = new Option<string[]>("--category") { Description = "Table categories (default: the categories of the stored analysis)", AllowMultipleArgumentsPerToken = true };
        var tables = new Option<string[]>("--tables") { Description = "Start tables (ID or name); keeps their neighborhood only", AllowMultipleArgumentsPerToken = true };
        var depth = new Option<int?>("--depth") { Description = "Neighborhood depth for --tables (default 1)" };
        var allRelations = new Option<bool>("--all-relations") { Description = "Include relations that are not migration dependencies" };
        var graphml = new Command("graphml", "Export the stored analysis to a GraphML file") { graphOut, apps, categories, tables, depth, allRelations, connection };
        graphml.SetAction(parseResult => ExitCodes.Run(() =>
        {
            var config = AppConfig.LoadDefault();
            var reader = new AnalysisReader(config.ResolveConnectionString(parseResult.GetValue(connection)));

            var tableFilter = Split(parseResult.GetValue(tables));
            if (parseResult.GetValue(depth) is not null && tableFilter.Count == 0)
            {
                throw new ConfigurationException("--depth is only valid together with --tables.");
            }

            var categoryFilter = Split(parseResult.GetValue(categories));
            var validated = TableCategoryParser.Parse(categoryFilter).Select(c => c.ToString()).ToList();
            var options = new GraphMlOptions(Split(parseResult.GetValue(apps)), validated, tableFilter, parseResult.GetValue(depth) ?? 1, parseResult.GetValue(allRelations));

            var defaultCategories = reader.Info().IncludedCategories.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var selection = GraphSelector.Select(reader.Tables(), reader.Relations(), defaultCategories, options);

            var output = parseResult.GetRequiredValue(graphOut);
            output.Directory?.Create();
            GraphMlWriter.Write(output.FullName, selection);

            Console.WriteLine($"{selection.Nodes.Count} nodes, {selection.Edges.Count} edges written to {output.FullName}");
            return ExitCodes.Success;
        }));

        return new Command("export", "Export the stored analysis") { csv, graphml };
    }

    private static IReadOnlyList<string> Split(string[]? values) =>
        (values ?? []).SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToList();
}
