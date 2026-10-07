using System.CommandLine;
using BcDepAnalyzer.Cli.Commands;

var root = new RootCommand("Business Central extension dependency analyzer")
{
    InspectCommand.Create(),
    DbCommand.Create(),
    AnalyzeCommand.Create(),
    ExportCommand.Create(),
};

return root.Parse(args).Invoke();
