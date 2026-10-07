using System.Globalization;
using System.Text;
using System.Xml;

namespace BcDepAnalyzer.Core.Export;

public static class GraphMlWriter
{
    private const string GraphMlNamespace = "http://graphml.graphdrawing.org/xmlns";
    private const string YedNamespace = "http://www.yworks.com/xml/graphml";

    private static readonly Dictionary<string, string> CategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Data"] = "#99CCFF",
        ["Setup"] = "#99FF99",
        ["LedgerEntry"] = "#FFCC99",
        ["PostedDocument"] = "#FFE699",
        ["Archive"] = "#D9D2E9",
        ["Buffer"] = "#EAD1DC",
        ["Log"] = "#F4CCCC",
        ["System"] = "#D9D9D9",
        ["Temporary"] = "#EFEFEF",
        ["External"] = "#B6D7A8",
    };

    private static readonly (string Name, string Type)[] NodeAttributes =
    [
        ("TableId", "int"), ("TableName", "string"), ("OriginApp", "string"), ("Category", "string"), ("Level", "int"), ("DependencyGroupId", "int"),
    ];

    private static readonly (string Name, string Type)[] EdgeAttributes =
    [
        ("RelationType", "string"), ("SourceField", "string"), ("TargetField", "string"), ("Conditional", "boolean"),
        ("Strength", "string"), ("IsMigrationDependency", "boolean"), ("IsDeferred", "boolean"),
    ];

    public static void Write(string path, GraphSelection selection)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = XmlWriter.Create(path, settings);
        Write(writer, selection);
    }

    public static void Write(XmlWriter writer, GraphSelection selection)
    {
        writer.WriteStartDocument();
        writer.WriteStartElement("graphml", GraphMlNamespace);
        writer.WriteAttributeString("xmlns", "y", null, YedNamespace);

        WriteKey(writer, "ng", "node", null, null, yfilesType: "nodegraphics");
        WriteKey(writer, "eg", "edge", null, null, yfilesType: "edgegraphics");
        foreach (var (name, type) in NodeAttributes)
        {
            WriteKey(writer, "n_" + name, "node", name, type);
        }

        foreach (var (name, type) in EdgeAttributes)
        {
            WriteKey(writer, "e_" + name, "edge", name, type);
        }

        writer.WriteStartElement("graph", GraphMlNamespace);
        writer.WriteAttributeString("id", "G");
        writer.WriteAttributeString("edgedefault", "directed");

        foreach (var table in selection.Nodes)
        {
            WriteNode(writer, table);
        }

        foreach (var relation in selection.Edges)
        {
            WriteEdge(writer, relation);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteKey(XmlWriter writer, string id, string target, string? name, string? type, string? yfilesType = null)
    {
        writer.WriteStartElement("key", GraphMlNamespace);
        if (name is not null)
        {
            writer.WriteAttributeString("attr.name", name);
            writer.WriteAttributeString("attr.type", type);
        }

        writer.WriteAttributeString("for", target);
        writer.WriteAttributeString("id", id);
        if (yfilesType is not null)
        {
            writer.WriteAttributeString("yfiles.type", yfilesType);
        }

        writer.WriteEndElement();
    }

    private static void WriteData(XmlWriter writer, string key, object? value)
    {
        if (value is null)
        {
            return;
        }

        writer.WriteStartElement("data", GraphMlNamespace);
        writer.WriteAttributeString("key", key);
        writer.WriteString(value switch
        {
            bool flag => flag ? "true" : "false",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        });
        writer.WriteEndElement();
    }

    private static void WriteNode(XmlWriter writer, TableRow table)
    {
        var label = $"{table.TableName} ({table.TableId.ToString(CultureInfo.InvariantCulture)})";

        writer.WriteStartElement("node", GraphMlNamespace);
        writer.WriteAttributeString("id", $"t{table.TableId.ToString(CultureInfo.InvariantCulture)}");
        WriteData(writer, "n_TableId", table.TableId);
        WriteData(writer, "n_TableName", table.TableName);
        WriteData(writer, "n_OriginApp", table.OriginApp);
        WriteData(writer, "n_Category", table.Category);
        WriteData(writer, "n_Level", table.Level);
        WriteData(writer, "n_DependencyGroupId", table.DependencyGroupId);

        writer.WriteStartElement("data", GraphMlNamespace);
        writer.WriteAttributeString("key", "ng");
        writer.WriteStartElement("y", "ShapeNode", YedNamespace);

        writer.WriteStartElement("y", "Geometry", YedNamespace);
        writer.WriteAttributeString("height", "30.0");
        writer.WriteAttributeString("width", (8 * label.Length + 20).ToString("0.0", CultureInfo.InvariantCulture));
        writer.WriteEndElement();

        writer.WriteStartElement("y", "Fill", YedNamespace);
        writer.WriteAttributeString("color", CategoryColors.GetValueOrDefault(table.Category, "#FFFFFF"));
        writer.WriteAttributeString("transparent", "false");
        writer.WriteEndElement();

        writer.WriteStartElement("y", "BorderStyle", YedNamespace);
        writer.WriteAttributeString("color", "#000000");
        writer.WriteAttributeString("type", "line");
        writer.WriteAttributeString("width", "1.0");
        writer.WriteEndElement();

        writer.WriteStartElement("y", "NodeLabel", YedNamespace);
        writer.WriteString(label);
        writer.WriteEndElement();

        writer.WriteStartElement("y", "Shape", YedNamespace);
        writer.WriteAttributeString("type", "roundrectangle");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteEdge(XmlWriter writer, RelationRow relation)
    {
        writer.WriteStartElement("edge", GraphMlNamespace);
        writer.WriteAttributeString("id", $"r{relation.RelationId.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteAttributeString("source", $"t{relation.SourceTableId.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteAttributeString("target", $"t{relation.TargetTableId!.Value.ToString(CultureInfo.InvariantCulture)}");
        WriteData(writer, "e_RelationType", relation.RelationType);
        WriteData(writer, "e_SourceField", relation.SourceFieldName);
        WriteData(writer, "e_TargetField", relation.TargetFieldName);
        WriteData(writer, "e_Conditional", relation.IsConditional);
        WriteData(writer, "e_Strength", relation.Strength);
        WriteData(writer, "e_IsMigrationDependency", relation.IsMigrationDependency);
        WriteData(writer, "e_IsDeferred", relation.IsDeferred);

        var (type, color) = !relation.IsMigrationDependency ? ("dotted", "#808080")
            : relation.IsDeferred ? ("dashed", "#FF0000")
            : relation.Strength == "Soft" ? ("dashed", "#000000")
            : ("line", "#000000");

        writer.WriteStartElement("data", GraphMlNamespace);
        writer.WriteAttributeString("key", "eg");
        writer.WriteStartElement("y", "PolyLineEdge", YedNamespace);

        writer.WriteStartElement("y", "LineStyle", YedNamespace);
        writer.WriteAttributeString("color", color);
        writer.WriteAttributeString("type", type);
        writer.WriteAttributeString("width", "1.0");
        writer.WriteEndElement();

        writer.WriteStartElement("y", "Arrows", YedNamespace);
        writer.WriteAttributeString("source", "none");
        writer.WriteAttributeString("target", "standard");
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }
}
