using System.CommandLine;
using BcDepAnalyzer.Core.Persistence;

namespace BcDepAnalyzer.Cli.Commands;

public static class DbCommand
{
    public static Command Create()
    {
        var connection = new Option<string?>("--connection") { Description = "SQL Server connection string (default: from configuration)" };

        var init = new Command("init", "Create the database and apply the schema scripts") { connection };
        init.SetAction(parseResult => ExitCodes.Run(() =>
        {
            var connectionString = AppConfig.LoadDefault().ResolveConnectionString(parseResult.GetValue(connection));
            new DatabaseInitializer(connectionString).Initialize();
            Console.WriteLine("Database is up to date.");
            return ExitCodes.Success;
        }));

        return new Command("db", "Database maintenance") { init };
    }
}
