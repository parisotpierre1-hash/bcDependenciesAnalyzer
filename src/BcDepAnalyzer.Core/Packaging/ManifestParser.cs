using System.Xml.Linq;

namespace BcDepAnalyzer.Core.Packaging;

public static class ManifestParser
{
    public static AppManifest Parse(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new PackageReadException("Manifest is not valid XML.", ex);
        }

        var app = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "App")
            ?? throw new PackageReadException("Manifest has no App element.");

        if (!Guid.TryParse(Attr(app, "Id"), out var appId))
        {
            throw new PackageReadException("Manifest App/@Id is not a valid GUID.");
        }

        var dependencies = new List<ManifestDependency>();
        var declared = document.Descendants()
            .Where(e => e.Name.LocalName == "Dependency" && e.Parent?.Name.LocalName == "Dependencies");
        foreach (var dependency in declared)
        {
            var rawId = Attr(dependency, "Id") ?? Attr(dependency, "AppId");
            if (!Guid.TryParse(rawId, out var dependencyId))
            {
                throw new PackageReadException("Manifest dependency has no valid Id.");
            }

            dependencies.Add(new ManifestDependency(
                dependencyId,
                Attr(dependency, "Name") ?? string.Empty,
                Attr(dependency, "Publisher") ?? string.Empty,
                Attr(dependency, "MinVersion") ?? Attr(dependency, "Version") ?? string.Empty));
        }

        return new AppManifest(
            appId,
            Attr(app, "Name") ?? string.Empty,
            Attr(app, "Publisher") ?? string.Empty,
            Attr(app, "Version") ?? string.Empty,
            dependencies);
    }

    private static string? Attr(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => a.Name.LocalName == name)?.Value;
}
