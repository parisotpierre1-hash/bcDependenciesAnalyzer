using System.CommandLine;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Cli.Commands;

public static class InspectCommand
{
    public static Command Create()
    {
        var app = new Option<FileInfo>("--app") { Description = "Path to the .app package", Required = true };
        var output = new Option<DirectoryInfo>("--out") { Description = "Output folder", Required = true };

        var command = new Command("inspect", "Dump the content of an .app package (development aid)") { app, output };
        command.SetAction(parseResult =>
        {
            try
            {
                var package = AppPackageReader.Read(parseResult.GetRequiredValue(app).FullName);
                var folder = PackageInspector.WriteTo(package, parseResult.GetRequiredValue(output).FullName);
                Console.WriteLine($"Written to {folder}");
                return ExitCodes.Success;
            }
            catch (PackageReadException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return ExitCodes.AnalysisError;
            }
        });

        return command;
    }
}
