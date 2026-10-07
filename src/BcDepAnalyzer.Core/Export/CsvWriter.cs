using System.Globalization;
using System.Text;

namespace BcDepAnalyzer.Core.Export;

public static class CsvWriter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static void Write(string path, char delimiter, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var writer = new StreamWriter(path, append: false, Utf8WithBom) { NewLine = "\r\n" };
        writer.WriteLine(string.Join(delimiter, header.Select(h => Escape(h, delimiter))));

        foreach (var row in rows)
        {
            writer.WriteLine(string.Join(delimiter, row.Select(v => Escape(Format(v), delimiter))));
        }
    }

    internal static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool flag => flag ? "true" : "false",
        Guid guid => guid.ToString("D").ToLowerInvariant(),
        string text => NeutralizeFormula(text),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    internal static string Escape(string value, char delimiter)
    {
        var needsQuotes = value.Contains(delimiter) || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    // Spreadsheet applications evaluate cells starting with these characters as formulas.
    private static string NeutralizeFormula(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' ? "'" + text : text;
}
