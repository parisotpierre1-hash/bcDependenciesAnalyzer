using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Persistence;

namespace BcDepAnalyzer.Cli;

public static class ExitCodes
{
    public const int Success = 0;
    public const int AnalysisError = 1;
    public const int InvalidArguments = 2;
    public const int DatabaseError = 3;

    public static int Run(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (ConfigurationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return InvalidArguments;
        }
        catch (DatabaseException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("If the schema does not exist yet, run 'analyzer db init' first.");
            return DatabaseError;
        }
    }
}
