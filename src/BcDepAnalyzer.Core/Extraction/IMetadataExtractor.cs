using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Extraction;

public interface IMetadataExtractor
{
    ExtractedPackage Extract(AppPackage package);
}
