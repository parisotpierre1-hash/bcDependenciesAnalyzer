using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Tests.Packaging;

public sealed class ManifestParserTests
{
    [Theory]
    [InlineData("Id")]
    [InlineData("AppId")]
    public void Parses_dependency_id_attribute_variants(string attribute)
    {
        var manifest = ManifestParser.Parse(TestPackageBuilder.ManifestXml(attribute));

        var dependency = Assert.Single(manifest.Dependencies);
        Assert.Equal(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), dependency.AppId);
        Assert.Equal("Base", dependency.Name);
        Assert.Equal("Microsoft", dependency.Publisher);
        Assert.Equal("23.0.0.0", dependency.MinVersion);
    }

    [Fact]
    public void Throws_when_app_id_is_not_a_guid()
    {
        const string xml = "<Package><App Id=\"nope\" Name=\"x\" Publisher=\"y\" Version=\"1.0.0.0\"/></Package>";

        Assert.Throws<PackageReadException>(() => ManifestParser.Parse(xml));
    }

    [Fact]
    public void Throws_on_invalid_xml()
    {
        Assert.Throws<PackageReadException>(() => ManifestParser.Parse("<Package>"));
    }
}
