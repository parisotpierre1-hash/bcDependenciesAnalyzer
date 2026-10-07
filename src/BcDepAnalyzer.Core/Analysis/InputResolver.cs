namespace BcDepAnalyzer.Core.Analysis;

public static class InputResolver
{
    // Each entry is a .app file or a folder (non-recursive); the result is sorted and distinct.
    public static IReadOnlyList<string> Resolve(IEnumerable<string> paths)
    {
        var files = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path, "*.app", SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                throw new ConfigurationException($"Input path '{path}' does not exist.");
            }
        }

        var resolved = files
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (resolved.Count == 0)
        {
            throw new ConfigurationException("No .app file found in the given inputs.");
        }

        return resolved;
    }
}
