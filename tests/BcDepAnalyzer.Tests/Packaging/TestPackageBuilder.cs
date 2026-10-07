using System.IO.Compression;
using System.Text;

namespace BcDepAnalyzer.Tests.Packaging;

internal static class TestPackageBuilder
{
    public const string AppId = "11111111-2222-3333-4444-555555555555";

    public static string ManifestXml(string idAttribute = "Id") => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <Package xmlns="http://schemas.microsoft.com/navx/2015/manifest">
          <App Id="{AppId}" Name="Test App" Publisher="Contoso" Version="1.2.3.4" />
          <Dependencies>
            <Dependency {idAttribute}="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" Name="Base" Publisher="Microsoft" MinVersion="23.0.0.0" />
          </Dependencies>
        </Package>
        """;

    public static byte[] BuildZip(string manifestXml, string? symbolJson = null, params (string Path, string Content)[] sources)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "NavxManifest.xml", manifestXml);
            if (symbolJson is not null)
            {
                Add(archive, "SymbolReference.json", symbolJson);
            }

            foreach (var (path, content) in sources)
            {
                Add(archive, path, content);
            }
        }

        return stream.ToArray();
    }

    public static byte[] WithNavxHeader(byte[] zip, int headerLength = 40)
    {
        var header = new byte[headerLength];
        Encoding.ASCII.GetBytes("NAVX").CopyTo(header, 0);
        BitConverter.GetBytes((uint)headerLength).CopyTo(header, 4);
        return [.. header, .. zip];
    }

    public static string WriteTemp(byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bcdep-test-{Guid.NewGuid():N}.app");
        File.WriteAllBytes(path, content);
        return path;
    }

    private static void Add(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        writer.Write(content);
    }
}
