using System.Globalization;
using System.Text.Json;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Core.Extraction;

public sealed class SymbolExtractor : IMetadataExtractor
{
    public ExtractedPackage Extract(AppPackage package)
    {
        if (package.SymbolReferenceJson is null)
        {
            throw new MetadataExtractionException($"'{package.FilePath}' has no SymbolReference.json.");
        }

        try
        {
            using var document = JsonDocument.Parse(package.SymbolReferenceJson, new JsonDocumentOptions { MaxDepth = 256 });
            var tables = new List<RawTable>();
            var extensions = new List<RawTableExtension>();
            Walk(document.RootElement, string.Empty, tables, extensions);
            return new ExtractedPackage(package.Manifest, tables, extensions);
        }
        catch (JsonException ex)
        {
            throw new MetadataExtractionException($"SymbolReference.json of '{package.FilePath}' is not valid JSON.", ex);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new MetadataExtractionException($"SymbolReference.json of '{package.FilePath}' has an unexpected structure: {ex.Message}", ex);
        }
    }

    private static void Walk(JsonElement container, string ns, List<RawTable> tables, List<RawTableExtension> extensions)
    {
        if (container.TryGetProperty("Tables", out var tableArray))
        {
            foreach (var table in tableArray.EnumerateArray())
            {
                tables.Add(ReadTable(table, ns));
            }
        }

        if (container.TryGetProperty("TableExtensions", out var extensionArray))
        {
            foreach (var extension in extensionArray.EnumerateArray())
            {
                extensions.Add(ReadExtension(extension));
            }
        }

        if (container.TryGetProperty("Namespaces", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                var name = child.GetProperty("Name").GetString()!;
                Walk(child, ns.Length == 0 ? name : $"{ns}.{name}", tables, extensions);
            }
        }
    }

    private static RawTable ReadTable(JsonElement table, string ns)
    {
        var properties = ReadProperties(table);
        var keyFields = new List<string>();

        if (table.TryGetProperty("Keys", out var keys) && keys.GetArrayLength() > 0
            && keys[0].TryGetProperty("FieldNames", out var names))
        {
            keyFields.AddRange(names.EnumerateArray().Select(n => n.GetString()!));
        }

        return new RawTable(
            table.GetProperty("Id").GetInt32(),
            table.GetProperty("Name").GetString()!,
            ns.Length == 0 ? null : ns,
            properties.GetValueOrDefault("TableType") ?? "Normal",
            ParseObsoleteState(properties.GetValueOrDefault("ObsoleteState")),
            ParseBool(properties.GetValueOrDefault("DataPerCompany"), true),
            ReadFields(table),
            keyFields);
    }

    private static RawTableExtension ReadExtension(JsonElement extension)
    {
        var target = extension.GetProperty("TargetObject").GetString()!;
        Guid? appId = null;

        // Form "#<app id without dashes>#<table name>"; other forms are a plain or dotted table name.
        if (target.StartsWith('#'))
        {
            var end = target.IndexOf('#', 1);
            if (end > 1 && Guid.TryParseExact(target[1..end], "N", out var parsed))
            {
                appId = parsed;
                target = target[(end + 1)..];
            }
        }

        return new RawTableExtension(
            extension.GetProperty("Id").GetInt32(),
            extension.GetProperty("Name").GetString()!,
            target,
            ReadFields(extension),
            appId);
    }

    private static List<RawField> ReadFields(JsonElement owner)
    {
        var fields = new List<RawField>();
        if (!owner.TryGetProperty("Fields", out var array))
        {
            return fields;
        }

        foreach (var field in array.EnumerateArray())
        {
            var properties = ReadProperties(field);
            var (dataType, length) = ReadType(field.GetProperty("TypeDefinition"));

            fields.Add(new RawField(
                field.GetProperty("Id").GetInt32(),
                field.GetProperty("Name").GetString()!,
                dataType,
                length,
                ParseFieldClass(properties.GetValueOrDefault("FieldClass")),
                ParseObsoleteState(properties.GetValueOrDefault("ObsoleteState")),
                NullIfBlank(properties.GetValueOrDefault("TableRelation")),
                ParseBool(properties.GetValueOrDefault("ValidateTableRelation"), true),
                NullIfBlank(properties.GetValueOrDefault("CalcFormula"))));
        }

        return fields;
    }

    private static (string DataType, int? Length) ReadType(JsonElement typeDefinition)
    {
        var name = typeDefinition.GetProperty("Name").GetString() ?? string.Empty;
        var bracket = name.IndexOf('[');

        if (bracket > 0 && name.EndsWith(']'))
        {
            var length = int.TryParse(name[(bracket + 1)..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : (int?)null;
            return (name[..bracket], length);
        }

        if (typeDefinition.TryGetProperty("Subtype", out var subtype)
            && subtype.TryGetProperty("Name", out var subtypeName)
            && subtypeName.GetString() is { Length: > 0 } subtypeText)
        {
            return ($"{name} \"{subtypeText}\"", null);
        }

        return (name, null);
    }

    // Property names are case-insensitive in symbols; the first occurrence wins.
    private static Dictionary<string, string> ReadProperties(JsonElement owner)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!owner.TryGetProperty("Properties", out var array))
        {
            return result;
        }

        foreach (var property in array.EnumerateArray())
        {
            if (property.TryGetProperty("Name", out var name)
                && property.TryGetProperty("Value", out var value)
                && name.GetString() is { } key)
            {
                result.TryAdd(key, value.GetString() ?? string.Empty);
            }
        }

        return result;
    }

    private static FieldClass ParseFieldClass(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "flowfield" => FieldClass.FlowField,
        "flowfilter" => FieldClass.FlowFilter,
        _ => FieldClass.Normal,
    };

    private static ObsoleteState ParseObsoleteState(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "pending" => ObsoleteState.Pending,
        "removed" => ObsoleteState.Removed,
        _ => ObsoleteState.No,
    };

    private static bool ParseBool(string? value, bool defaultValue) => value?.Trim().ToLowerInvariant() switch
    {
        "0" or "false" => false,
        "1" or "true" => true,
        _ => defaultValue,
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
