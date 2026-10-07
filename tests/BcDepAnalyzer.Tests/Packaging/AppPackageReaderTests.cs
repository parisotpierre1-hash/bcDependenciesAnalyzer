using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Tests.Packaging;

public sealed class AppPackageReaderTests
{
    [Fact]
    public void Reads_plain_zip_package()
    {
        var path = TestPackageBuilder.WriteTemp(TestPackageBuilder.BuildZip(
            TestPackageBuilder.ManifestXml(),
            "{\"Tables\":[]}",
            ("src/Customer.al", "table 18 Customer { }")));

        try
        {
            var package = AppPackageReader.Read(path);

            Assert.Equal(Guid.Parse(TestPackageBuilder.AppId), package.Manifest.AppId);
            Assert.Equal("Test App", package.Manifest.Name);
            Assert.Equal("Contoso", package.Manifest.Publisher);
            Assert.Equal("1.2.3.4", package.Manifest.Version);
            Assert.Equal("{\"Tables\":[]}", package.SymbolReferenceJson);
            var source = Assert.Single(package.SourceFiles);
            Assert.Equal("src/Customer.al", source.Path);
            Assert.Equal("table 18 Customer { }", source.Content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Reads_package_with_navx_header()
    {
        var zip = TestPackageBuilder.BuildZip(TestPackageBuilder.ManifestXml());
        var path = TestPackageBuilder.WriteTemp(TestPackageBuilder.WithNavxHeader(zip));

        try
        {
            var package = AppPackageReader.Read(path);

            Assert.Equal("Test App", package.Manifest.Name);
            Assert.Null(package.SymbolReferenceJson);
            Assert.Empty(package.SourceFiles);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Finds_zip_by_scanning_when_header_length_is_wrong()
    {
        var zip = TestPackageBuilder.BuildZip(TestPackageBuilder.ManifestXml());
        var bytes = TestPackageBuilder.WithNavxHeader(zip);
        BitConverter.GetBytes(7u).CopyTo(bytes, 4);
        var path = TestPackageBuilder.WriteTemp(bytes);

        try
        {
            Assert.Equal("Test App", AppPackageReader.Read(path).Manifest.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Throws_on_non_package_file()
    {
        var path = TestPackageBuilder.WriteTemp("this is not a package"u8.ToArray());

        try
        {
            Assert.Throws<PackageReadException>(() => AppPackageReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Throws_on_missing_file()
    {
        Assert.Throws<PackageReadException>(() => AppPackageReader.Read(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".app")));
    }

    [Fact]
    public void Throws_when_manifest_is_missing()
    {
        using var stream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("other.txt");
        }

        var path = TestPackageBuilder.WriteTemp(stream.ToArray());

        try
        {
            Assert.Throws<PackageReadException>(() => AppPackageReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
