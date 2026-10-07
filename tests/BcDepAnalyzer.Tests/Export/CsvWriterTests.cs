using System.Text;
using BcDepAnalyzer.Core.Export;

namespace BcDepAnalyzer.Tests.Export;

public sealed class CsvWriterTests
{
    private static byte[] Write(char delimiter, string[] header, params object?[][] rows)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bcdep-csv-{Guid.NewGuid():N}.csv");
        try
        {
            CsvWriter.Write(path, delimiter, header, rows);
            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string Text(byte[] bytes) => new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

    [Fact]
    public void Starts_with_utf8_bom_and_uses_crlf()
    {
        var bytes = Write(';', ["A", "B"], [1, "x"]);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("A;B\r\n1;x\r\n", Text(bytes));
    }

    [Fact]
    public void Quotes_values_containing_delimiter_quote_or_line_breaks()
    {
        var text = Text(Write(';', ["H"], ["a;b"], ["say \"hi\""], ["line1\r\nline2"], ["plain, comma"]));

        Assert.Equal("H\r\n\"a;b\"\r\n\"say \"\"hi\"\"\"\r\n\"line1\r\nline2\"\r\nplain, comma\r\n", text);
    }

    [Fact]
    public void Delimiter_is_configurable()
    {
        Assert.Equal("H\r\n\"a,b\"\r\n", Text(Write(',', ["H"], ["a,b"])));
    }

    [Fact]
    public void Formats_nulls_booleans_numbers_and_guids()
    {
        var guid = Guid.Parse("AABBCCDD-0000-0000-0000-000000000001");
        var text = Text(Write(',', ["A", "B", "C", "D", "E"], [null, true, false, 12.5m, guid]));

        Assert.Equal("A,B,C,D,E\r\n,true,false,12.5,aabbccdd-0000-0000-0000-000000000001\r\n", text);
    }

    [Fact]
    public void Neutralizes_text_that_a_spreadsheet_would_evaluate_as_a_formula()
    {
        var text = Text(Write(',', ["H"], ["-Sum(X)"], ["=1+1"], ["@cmd"], ["+1"], ["normal"], [-5]));

        Assert.Equal("H\r\n'-Sum(X)\r\n'=1+1\r\n'@cmd\r\n'+1\r\nnormal\r\n-5\r\n", text);
    }
}
