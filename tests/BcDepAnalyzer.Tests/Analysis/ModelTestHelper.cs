using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Tests.Analysis;

internal static class ModelTestHelper
{
    public static readonly Guid BaseAppId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid ExtAppId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public static RawField Field(
        int id,
        string name,
        string dataType = "Code",
        int? length = 20,
        FieldClass fieldClass = FieldClass.Normal,
        string? relation = null,
        bool validate = true,
        string? calc = null,
        ObsoleteState obsolete = ObsoleteState.No) =>
        new(id, name, dataType, length, fieldClass, obsolete, relation, validate, calc);

    public static RawTable Table(
        int id,
        string name,
        string[]? key = null,
        string? ns = null,
        string tableType = "Normal",
        ObsoleteState obsolete = ObsoleteState.No,
        params RawField[] fields) =>
        new(id, name, ns, tableType, obsolete, true, fields, key ?? []);

    public static RawTableExtension Extension(int id, string name, string extended, params RawField[] fields) =>
        new(id, name, extended, fields);

    public static LoadedPackage Package(
        string path,
        Guid appId,
        string appName,
        RawTable[]? tables = null,
        RawTableExtension[]? extensions = null,
        params ManifestDependency[] dependencies) =>
        new(path, new ExtractedPackage(
            new AppManifest(appId, appName, "Contoso", "1.0.0.0", dependencies),
            tables ?? [],
            extensions ?? []));

    public static TableCategorizer Categorizer() =>
        new(new CategoryRules([new NameRule("* Ledger Entry", TableCategory.LedgerEntry)], []));

    public static IReadOnlyList<TableCategory> Included { get; } = [TableCategory.Setup, TableCategory.Data];
}
