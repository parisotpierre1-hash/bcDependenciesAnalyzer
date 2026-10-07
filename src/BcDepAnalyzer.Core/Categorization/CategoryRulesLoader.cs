using System.Text.Json;
using System.Text.Json.Serialization;

namespace BcDepAnalyzer.Core.Categorization;

public static class CategoryRulesLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static CategoryRules LoadFile(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException($"Cannot read rules file '{path}'.", ex);
        }

        return Parse(json, path);
    }

    public static CategoryRules Parse(string json, string source = "rules")
    {
        RulesDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<RulesDocument>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new ConfigurationException($"Invalid rules file '{source}': {ex.Message}", ex);
        }

        if (document is null)
        {
            throw new ConfigurationException($"Rules file '{source}' is empty.");
        }

        var nameRules = document.NameRules ?? [];
        if (nameRules.Any(r => string.IsNullOrWhiteSpace(r.Pattern)))
        {
            throw new ConfigurationException($"Rules file '{source}' contains a name rule without pattern.");
        }

        return new CategoryRules(nameRules, document.Overrides ?? []);
    }

    private sealed class RulesDocument
    {
        public List<NameRule>? NameRules { get; set; }
        public List<CategoryOverride>? Overrides { get; set; }
    }
}
