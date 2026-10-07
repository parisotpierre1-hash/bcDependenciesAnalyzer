using BcDepAnalyzer.Cli;
using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Tests.Cli;

public sealed class AppConfigTests
{
    private static AppConfig LoadJson(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bcdep-config-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        try
        {
            return AppConfig.Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private const string Json = """
        {
          "ConnectionString": "Server=x;Database=y",
          "RulesFile": "rules/custom.json",
          "IncludedCategories": [ "Data" ],
          "Csv": { "Delimiter": ";" }
        }
        """;

    [Fact]
    public void Loads_values_from_file()
    {
        var config = LoadJson(Json);

        Assert.Equal("Server=x;Database=y", config.ConnectionString);
        Assert.Equal("rules/custom.json", config.RulesFile);
        Assert.Equal(["Data"], config.IncludedCategories);
        Assert.Equal(";", config.Csv.Delimiter);
    }

    [Fact]
    public void Command_line_overrides_configuration()
    {
        var config = LoadJson(Json);

        Assert.Equal("Server=other", config.ResolveConnectionString("Server=other"));
        Assert.Equal("Server=x;Database=y", config.ResolveConnectionString(null));
        Assert.Equal([TableCategory.Data], config.ResolveIncludedCategories(null));
        Assert.Equal([TableCategory.LedgerEntry, TableCategory.Setup], config.ResolveIncludedCategories("ledgerentry, Setup"));
        Assert.Equal("|", config.ResolveDelimiter("|"));
        Assert.Equal(";", config.ResolveDelimiter(null));
    }

    [Fact]
    public void Rules_file_is_relative_to_the_executable_unless_given_on_the_command_line()
    {
        var config = LoadJson(Json);
        var baseDirectory = Path.Combine(Path.GetTempPath(), "app");

        Assert.Equal(Path.GetFullPath("rules/custom.json", baseDirectory), config.ResolveRulesFile(null, baseDirectory));
        Assert.Equal(Path.GetFullPath("mine.json"), config.ResolveRulesFile("mine.json", baseDirectory));
    }

    [Fact]
    public void Invalid_values_are_configuration_errors()
    {
        var config = LoadJson(Json);

        Assert.Throws<ConfigurationException>(() => config.ResolveIncludedCategories("Nope"));
        Assert.Throws<ConfigurationException>(() => config.ResolveDelimiter("ab"));
        Assert.Throws<ConfigurationException>(() => LoadJson("{ not json"));
        Assert.Throws<ConfigurationException>(() => new AppConfig().ResolveConnectionString(null));
        Assert.Throws<ConfigurationException>(() => AppConfig.Load(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".json")));
    }
}
