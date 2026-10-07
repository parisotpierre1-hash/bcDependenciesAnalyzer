using System.Text;
using System.Text.Json;

namespace BcDepAnalyzer.Core.Packaging;

public static class PackageInspector
{
    public static string WriteTo(AppPackage package, string outputRoot)
    {
        var manifest = package.Manifest;
        var folder = Path.Combine(outputRoot, Sanitize($"{manifest.Name}_{manifest.Version}"));
        Directory.CreateDirectory(folder);

        if (package.ManifestXml is not null)
        {
            File.WriteAllText(Path.Combine(folder, "manifest.xml"), package.ManifestXml);
        }

        if (package.SymbolReferenceJson is not null)
        {
            File.WriteAllText(Path.Combine(folder, "SymbolReference.json"), PrettyPrint(package.SymbolReferenceJson));
        }

        File.WriteAllLines(Path.Combine(folder, "entries.txt"), package.EntryNames);

        var sourceRoot = Path.GetFullPath(Path.Combine(folder, "src"));
        foreach (var source in package.SourceFiles)
        {
            var relative = source.Path.Replace('/', Path.DirectorySeparatorChar);
            if (relative.StartsWith("src" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                relative = relative["src".Length..].TrimStart(Path.DirectorySeparatorChar);
            }

            var target = Path.GetFullPath(Path.Combine(sourceRoot, relative));
            if (!target.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, source.Content);
        }

        File.WriteAllText(Path.Combine(folder, "summary.txt"), BuildSummary(package));
        return folder;
    }

    private static string BuildSummary(AppPackage package)
    {
        var manifest = package.Manifest;
        var builder = new StringBuilder();
        builder.AppendLine($"App ID:              {manifest.AppId}");
        builder.AppendLine($"Name:                {manifest.Name}");
        builder.AppendLine($"Publisher:           {manifest.Publisher}");
        builder.AppendLine($"Version:             {manifest.Version}");
        builder.AppendLine($"Dependencies:        {manifest.Dependencies.Count}");
        builder.AppendLine($"Symbol file present: {(package.SymbolReferenceJson is null ? "no" : "yes")}");
        builder.AppendLine($".al files:           {package.SourceFiles.Count}");
        return builder.ToString();
    }

    private static string PrettyPrint(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            document.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
