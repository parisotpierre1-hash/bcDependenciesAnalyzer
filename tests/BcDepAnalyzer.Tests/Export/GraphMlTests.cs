using System.Xml.Linq;
using BcDepAnalyzer.Core;
using BcDepAnalyzer.Core.Export;

namespace BcDepAnalyzer.Tests.Export;

public sealed class GraphMlTests
{
    private static readonly Guid AppA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid AppB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static TableRow Table(int id, string name, string category = "Data", Guid? app = null, string appName = "A", string obsolete = "No", int? level = 0, int? group = null) =>
        new(id, name, null, app ?? AppA, appName, "Normal", category, obsolete, level, group, false);

    private static RelationRow Relation(int id, int source, int target, bool dependency = true, string strength = "Hard", bool deferred = false) =>
        new(id, source, $"T{source}", "F", target, $"T{target}", null, "TableRelation", null, false, strength, dependency, dependency ? null : "Conditional", deferred, "x");

    private static readonly TableRow[] Tables =
    [
        Table(1, "T1"), Table(2, "T2"), Table(3, "T3"), Table(4, "T4"),
        Table(5, "T5", app: AppB, appName: "B"),
        Table(6, "T6", category: "LedgerEntry"),
        Table(7, "T7", obsolete: "Removed"),
        Table(8, "Isolated", level: null),
    ];

    private static readonly RelationRow[] Relations =
    [
        Relation(1, 1, 2),
        Relation(2, 2, 3),
        Relation(3, 3, 4),
        Relation(4, 5, 1),
        Relation(5, 1, 6),
        Relation(6, 2, 2),
        Relation(7, 4, 1, dependency: false),
        Relation(8, 1, 7),
    ];

    private static GraphMlOptions Options(string[]? apps = null, string[]? categories = null, string[]? tables = null, int depth = 1, bool all = false) =>
        new(apps ?? [], categories ?? [], tables ?? [], depth, all);

    private static GraphSelection Select(GraphMlOptions options) =>
        GraphSelector.Select(Tables, Relations, ["Data"], options);

    [Fact]
    public void Default_selection_keeps_included_categories_and_migration_dependencies()
    {
        var selection = Select(Options());

        Assert.Equal([1, 2, 3, 4, 5, 8], selection.Nodes.Select(n => n.TableId));
        Assert.Equal([1, 2, 3, 4], selection.Edges.Select(e => e.RelationId));
    }

    [Fact]
    public void All_relations_adds_non_dependencies_but_never_self_loops()
    {
        var selection = Select(Options(all: true));

        Assert.Equal([1, 2, 3, 4, 7], selection.Edges.Select(e => e.RelationId));
    }

    [Fact]
    public void Category_filter_overrides_the_default()
    {
        var selection = Select(Options(categories: ["LedgerEntry", "Data"]));

        Assert.Contains(selection.Nodes, n => n.TableId == 6);
        Assert.Contains(selection.Edges, e => e.RelationId == 5);
    }

    [Fact]
    public void App_filter_matches_by_name_or_id_and_is_intersected_with_categories()
    {
        Assert.Equal([5], Select(Options(apps: ["b"])).Nodes.Select(n => n.TableId));
        Assert.Equal([5], Select(Options(apps: [AppB.ToString("D")])).Nodes.Select(n => n.TableId));
        Assert.Empty(Select(Options(apps: ["B"], categories: ["LedgerEntry"])).Nodes);
    }

    [Theory]
    [InlineData(0, new[] { 2 })]
    [InlineData(1, new[] { 1, 2, 3 })]
    [InlineData(2, new[] { 1, 2, 3, 4, 5 })]
    public void Neighborhood_follows_edges_in_both_directions_up_to_the_depth(int depth, int[] expected)
    {
        var selection = Select(Options(tables: ["T2"], depth: depth));

        Assert.Equal(expected, selection.Nodes.Select(n => n.TableId));
    }

    [Fact]
    public void Start_tables_can_be_given_by_id()
    {
        Assert.Equal([5], Select(Options(tables: ["5"], depth: 0)).Nodes.Select(n => n.TableId));
    }

    [Fact]
    public void Unknown_or_filtered_out_start_table_is_a_configuration_error()
    {
        Assert.Throws<ConfigurationException>(() => Select(Options(tables: ["Nope"])));
        Assert.Throws<ConfigurationException>(() => Select(Options(tables: ["T6"])));
    }

    [Fact]
    public void Written_file_is_well_formed_with_expected_structure()
    {
        var selection = GraphSelector.Select(
            [Table(1, "A & B", level: null), Table(2, "T2", level: 3, group: 4)],
            [Relation(9, 1, 2, strength: "Soft", deferred: true)],
            ["Data"],
            Options());
        var path = Path.Combine(Path.GetTempPath(), $"bcdep-{Guid.NewGuid():N}.graphml");

        try
        {
            GraphMlWriter.Write(path, selection);
            var document = XDocument.Load(path);
            XNamespace g = "http://graphml.graphdrawing.org/xmlns";
            XNamespace y = "http://www.yworks.com/xml/graphml";

            Assert.Equal("directed", document.Root!.Element(g + "graph")!.Attribute("edgedefault")!.Value);
            Assert.Contains(document.Root.Elements(g + "key"), k => (string?)k.Attribute("yfiles.type") == "nodegraphics");
            Assert.Contains(document.Root.Elements(g + "key"), k => (string?)k.Attribute("yfiles.type") == "edgegraphics");

            var nodes = document.Descendants(g + "node").ToList();
            Assert.Equal(2, nodes.Count);
            Assert.Equal("t1", (string)nodes[0].Attribute("id")!);
            Assert.Equal("A & B (1)", nodes[0].Descendants(y + "NodeLabel").Single().Value);

            var firstKeys = nodes[0].Elements(g + "data").Select(d => (string)d.Attribute("key")!).ToList();
            Assert.DoesNotContain("n_Level", firstKeys);
            Assert.DoesNotContain("n_DependencyGroupId", firstKeys);
            Assert.Contains("n_Level", nodes[1].Elements(g + "data").Select(d => (string)d.Attribute("key")!));

            var edge = Assert.Single(document.Descendants(g + "edge"));
            Assert.Equal(("r9", "t1", "t2"), ((string)edge.Attribute("id")!, (string)edge.Attribute("source")!, (string)edge.Attribute("target")!));
            var style = edge.Descendants(y + "LineStyle").Single();
            Assert.Equal(("dashed", "#FF0000"), ((string)style.Attribute("type")!, (string)style.Attribute("color")!));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
