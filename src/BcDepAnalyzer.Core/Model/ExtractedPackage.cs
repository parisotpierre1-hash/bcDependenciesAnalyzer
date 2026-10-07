using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Model;

public sealed record ExtractedPackage(
    AppManifest App,
    IReadOnlyList<RawTable> Tables,
    IReadOnlyList<RawTableExtension> TableExtensions);
