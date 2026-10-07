using System.Globalization;
using BcDepAnalyzer.Core.Categorization;
using BcDepAnalyzer.Core.Model;

namespace BcDepAnalyzer.Core.Analysis;

public static class ModelBuilder
{
    public static AnalysisResult Build(
        IReadOnlyList<LoadedPackage> packages,
        TableCategorizer categorizer,
        IReadOnlyList<TableCategory> includedCategories)
    {
        var result = new AnalysisResult { IncludedCategories = includedCategories };
        var accepted = AddApps(result, packages);
        AddMissingDependencyWarnings(result);

        var tablesById = new Dictionary<int, TableModel>();
        var firstFieldByTable = new Dictionary<int, int>();
        AddTables(result, accepted, tablesById, firstFieldByTable);

        if (result.Issues.Any(i => i.Severity == IssueSeverity.Error))
        {
            return result;
        }

        var resolver = new TableNameResolver(result.Tables);
        AddTableExtensions(result, accepted, resolver, tablesById);

        foreach (var table in result.Tables)
        {
            table.Fields.Sort((a, b) => a.Id.CompareTo(b.Id));
            AssignPrimaryKey(table, accepted, firstFieldByTable);
            table.Category = categorizer.Categorize(table.Id, table.Name, table.TableType);
        }

        RelationBuilder.Build(result, resolver);
        return result;
    }

    private static List<LoadedPackage> AddApps(AnalysisResult result, IReadOnlyList<LoadedPackage> packages)
    {
        var accepted = new List<LoadedPackage>();
        var pathByAppId = new Dictionary<Guid, string>();

        foreach (var package in packages.OrderBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            var app = package.Package.App;
            if (pathByAppId.TryGetValue(app.AppId, out var firstPath))
            {
                result.Issues.Add(new Issue(
                    IssueSeverity.Error,
                    IssueType.DuplicateAppId,
                    app.Name,
                    null,
                    $"App '{app.Name}' ({app.AppId}) is selected twice: '{firstPath}' and '{package.FilePath}'."));
                continue;
            }

            pathByAppId[app.AppId] = package.FilePath;
            accepted.Add(package);
            result.Apps.Add(new AppModel(app.AppId, app.Name, app.Publisher, app.Version, package.FilePath, app.Dependencies));
        }

        return accepted;
    }

    private static void AddMissingDependencyWarnings(AnalysisResult result)
    {
        var known = result.Apps.Select(a => a.AppId).ToHashSet();

        foreach (var app in result.Apps)
        {
            foreach (var dependency in app.Dependencies.Where(d => !known.Contains(d.AppId)))
            {
                result.Issues.Add(new Issue(
                    IssueSeverity.Warning,
                    IssueType.MissingDependency,
                    app.Name,
                    dependency.Name,
                    $"App '{app.Name}' depends on '{dependency.Name}' ({dependency.AppId}), which is not part of the analysis."));
            }
        }
    }

    private static void AddTables(
        AnalysisResult result,
        List<LoadedPackage> accepted,
        Dictionary<int, TableModel> tablesById,
        Dictionary<int, int> firstFieldByTable)
    {
        foreach (var package in accepted)
        {
            var app = package.Package.App;

            foreach (var raw in package.Package.Tables.OrderBy(t => t.Id))
            {
                if (tablesById.TryGetValue(raw.Id, out var existing))
                {
                    result.Issues.Add(new Issue(
                        IssueSeverity.Error,
                        IssueType.DuplicateTableId,
                        app.Name,
                        raw.Name,
                        $"Table ID {raw.Id} is defined by '{raw.Name}' in app '{app.Name}' and by '{existing.Name}' in another app."));
                    continue;
                }

                var table = new TableModel
                {
                    Id = raw.Id,
                    Name = raw.Name,
                    Namespace = string.IsNullOrEmpty(raw.Namespace) ? null : raw.Namespace,
                    AppId = app.AppId,
                    TableType = string.IsNullOrEmpty(raw.TableType) ? "Normal" : raw.TableType,
                    ObsoleteState = raw.ObsoleteState,
                    DataPerCompany = raw.DataPerCompany,
                };

                foreach (var field in raw.Fields)
                {
                    AddField(result, table, field, app.AppId, app.Name, "Table", raw.Id, raw.Name);
                }

                if (raw.Fields.Count > 0)
                {
                    firstFieldByTable[raw.Id] = raw.Fields[0].Id;
                }

                tablesById[raw.Id] = table;
                result.Tables.Add(table);
            }
        }
    }

    private static void AddTableExtensions(
        AnalysisResult result,
        List<LoadedPackage> accepted,
        TableNameResolver resolver,
        Dictionary<int, TableModel> tablesById)
    {
        foreach (var package in accepted)
        {
            var app = package.Package.App;

            foreach (var extension in package.Package.TableExtensions.OrderBy(e => e.Id))
            {
                var target = resolver.ResolveTableName(extension.ExtendedTableName, extension.ExtendedAppId).Table;

                if (target is null)
                {
                    result.TableExtensions.Add(new TableExtensionModel(app.AppId, extension.Id, extension.Name, extension.ExtendedTableName, null, true));
                    result.Issues.Add(new Issue(
                        IssueSeverity.Warning,
                        IssueType.OrphanTableExtension,
                        app.Name,
                        extension.Name,
                        $"Table extension '{extension.Name}' extends '{extension.ExtendedTableName}', which is not part of the analysis (or is ambiguous)."));
                    continue;
                }

                result.TableExtensions.Add(new TableExtensionModel(app.AppId, extension.Id, extension.Name, extension.ExtendedTableName, target.Id, false));
                foreach (var field in extension.Fields)
                {
                    AddField(result, tablesById[target.Id], field, app.AppId, app.Name, "TableExtension", extension.Id, extension.Name);
                }
            }
        }
    }

    private static void AddField(
        AnalysisResult result,
        TableModel table,
        RawField raw,
        Guid appId,
        string appName,
        string sourceType,
        int sourceId,
        string sourceName)
    {
        var existing = table.Fields.FirstOrDefault(f => f.Id == raw.Id);
        if (existing is not null)
        {
            result.Issues.Add(new Issue(
                IssueSeverity.Warning,
                IssueType.DuplicateFieldId,
                appName,
                $"{table.Name}.{raw.Name}",
                $"Field ID {raw.Id.ToString(CultureInfo.InvariantCulture)} of table '{table.Name}' is defined by '{sourceName}' and already by '{existing.SourceObjectName}'; the first definition is kept."));
            return;
        }

        table.Fields.Add(new FieldModel
        {
            TableId = table.Id,
            Id = raw.Id,
            Name = raw.Name,
            DataType = raw.DataType,
            Length = raw.Length,
            FieldClass = raw.FieldClass,
            ObsoleteState = raw.ObsoleteState,
            AppId = appId,
            SourceObjectType = sourceType,
            SourceObjectId = sourceId,
            SourceObjectName = sourceName,
            TableRelationText = raw.TableRelationText,
            ValidateTableRelation = raw.ValidateTableRelation,
            CalcFormulaText = raw.CalcFormulaText,
        });
    }

    private static void AssignPrimaryKey(TableModel table, List<LoadedPackage> accepted, Dictionary<int, int> firstFieldByTable)
    {
        var keyNames = accepted
            .SelectMany(p => p.Package.Tables)
            .Where(t => t.Id == table.Id)
            .Select(t => t.PrimaryKeyFieldNames)
            .FirstOrDefault() ?? [];

        foreach (var name in keyNames)
        {
            var field = table.Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (field is not null && !table.PrimaryKeyFieldIds.Contains(field.Id))
            {
                table.PrimaryKeyFieldIds.Add(field.Id);
            }
        }

        if (table.PrimaryKeyFieldIds.Count == 0 && firstFieldByTable.TryGetValue(table.Id, out var firstFieldId))
        {
            table.PrimaryKeyFieldIds.Add(firstFieldId);
        }

        foreach (var field in table.Fields)
        {
            field.IsPrimaryKey = table.PrimaryKeyFieldIds.Contains(field.Id);
        }
    }
}
