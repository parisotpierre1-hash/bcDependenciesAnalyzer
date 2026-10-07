using System.Text.Json;
using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Cli;

public sealed class CsvConfig
{
    public string Delimiter { get; set; } = ",";
}

public sealed class AppConfig
{
    public string ConnectionString { get; set; } = string.Empty;
    public string RulesFile { get; set; } = "rules/default-categories.json";
    public List<string> IncludedCategories { get; set; } = ["Setup", "Data"];
    public CsvConfig Csv { get; set; } = new();

    public static AppConfig Load(string path)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options)
                ?? throw new ConfigurationException($"Configuration file '{path}' is empty.");
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            throw new ConfigurationException($"Cannot load configuration file '{path}': {ex.Message}", ex);
        }
    }

    public static AppConfig LoadDefault() => Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

    public string ResolveConnectionString(string? commandLineValue)
    {
        var value = string.IsNullOrWhiteSpace(commandLineValue) ? ConnectionString : commandLineValue;
        return string.IsNullOrWhiteSpace(value)
            ? throw new ConfigurationException("No connection string configured (appsettings.json or --connection).")
            : value;
    }

    // A command-line path is relative to the current directory; the configured one is relative to the executable.
    public string ResolveRulesFile(string? commandLineValue, string baseDirectory) =>
        !string.IsNullOrWhiteSpace(commandLineValue)
            ? Path.GetFullPath(commandLineValue)
            : Path.GetFullPath(RulesFile, baseDirectory);

    public IReadOnlyList<TableCategory> ResolveIncludedCategories(string? commandLineValue) =>
        TableCategoryParser.Parse(string.IsNullOrWhiteSpace(commandLineValue) ? IncludedCategories : [commandLineValue]);

    public string ResolveDelimiter(string? commandLineValue)
    {
        var value = string.IsNullOrEmpty(commandLineValue) ? Csv.Delimiter : commandLineValue;
        return value.Length == 1 ? value : throw new ConfigurationException("The CSV delimiter must be a single character.");
    }
}
