using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace BcDepAnalyzer.Core.Packaging;

public static class AppPackageReader
{
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] NavxMagic = "NAVX"u8.ToArray();

    public static AppPackage Read(string filePath)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PackageReadException($"Cannot read file '{filePath}'.", ex);
        }

        var zipOffset = FindZipOffset(bytes)
            ?? throw new PackageReadException($"'{filePath}' does not contain a ZIP archive.");

        try
        {
            using var stream = new MemoryStream(bytes, zipOffset, bytes.Length - zipOffset, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return ReadArchive(filePath, archive);
        }
        catch (InvalidDataException ex)
        {
            throw new PackageReadException($"'{filePath}' is not a valid package.", ex);
        }
    }

    private static int? FindZipOffset(byte[] bytes)
    {
        if (StartsWithAt(bytes, 0, ZipSignature))
        {
            return 0;
        }

        if (StartsWithAt(bytes, 0, NavxMagic) && bytes.Length >= 8)
        {
            var headerLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
            if (headerLength < bytes.Length && StartsWithAt(bytes, (int)headerLength, ZipSignature))
            {
                return (int)headerLength;
            }
        }

        var limit = Math.Min(bytes.Length, 4096);
        for (var i = 0; i + ZipSignature.Length <= limit; i++)
        {
            if (StartsWithAt(bytes, i, ZipSignature))
            {
                return i;
            }
        }

        return null;
    }

    private static bool StartsWithAt(byte[] bytes, int offset, byte[] pattern) =>
        offset >= 0
        && offset + pattern.Length <= bytes.Length
        && bytes.AsSpan(offset, pattern.Length).SequenceEqual(pattern);

    private static AppPackage ReadArchive(string filePath, ZipArchive archive)
    {
        var entryNames = archive.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();

        var manifestEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("NavxManifest.xml", StringComparison.OrdinalIgnoreCase))
            ?? throw new PackageReadException($"'{filePath}' has no NavxManifest.xml.");
        var manifestXml = ReadText(manifestEntry);
        var manifest = ManifestParser.Parse(manifestXml);

        var symbolEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("SymbolReference.json", StringComparison.OrdinalIgnoreCase));
        var symbolJson = symbolEntry is null ? null : ReadText(symbolEntry);

        var sources = archive.Entries
            .Where(e => e.FullName.EndsWith(".al", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .Select(e => new SourceFile(e.FullName, ReadText(e)))
            .ToList();

        return new AppPackage(filePath, manifest, manifestXml, symbolJson, sources, entryNames);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
