using BcDepAnalyzer.Core.Extraction;
using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Packaging;

namespace BcDepAnalyzer.Tests.Extraction;

public sealed class SymbolExtractorTests
{
    private static ExtractedPackage Extract(string? json)
    {
        var manifest = new AppManifest(Guid.NewGuid(), "Test", "Contoso", "1.0.0.0", []);
        var package = new AppPackage("test.app", manifest, null, json, [], []);
        return new SymbolExtractor().Extract(package);
    }

    private const string Json = """
        {
          "Namespaces": [
            {
              "Name": "Microsoft",
              "Namespaces": [
                {
                  "Name": "Sales",
                  "Tables": [
                    {
                      "Id": 18,
                      "Name": "Customer",
                      "Properties": [ { "Value": "0", "Name": "DataPerCompany" }, { "Name": "ObsoleteState", "Value": "Pending" } ],
                      "Keys": [ { "FieldNames": [ "No." ], "Name": "Key1" }, { "FieldNames": [ "Name" ], "Name": "Key2" } ],
                      "Fields": [
                        { "Id": 1, "Name": "No.", "TypeDefinition": { "Name": "Code[20]" } },
                        { "Id": 2, "Name": "Name", "TypeDefinition": { "Name": "Text[100]" }, "Properties": [ { "Name": "Caption", "Value": "Name" } ] },
                        { "Id": 5, "Name": "Type", "TypeDefinition": { "Name": "Enum", "Subtype": { "Name": "Sales Document Type", "Id": 1 } } },
                        { "Id": 7, "Name": "Kind", "TypeDefinition": { "Name": "Option", "OptionMembers": [ "A", "B" ] } },
                        { "Id": 9, "Name": "Amount", "TypeDefinition": { "Name": "Decimal" }, "Properties": [ { "Name": "FieldClass", "Value": "Flowfield" }, { "Name": "CalcFormula", "Value": "sum(\"X\".Amount)" } ] },
                        { "Id": 10, "Name": "Flt", "TypeDefinition": { "Name": "Code[20]" }, "Properties": [ { "Name": "fieldclass", "Value": "FlowFilter" }, { "Name": "TableRelation", "Value": "  Customer  " }, { "Name": "ValidateTableRelation", "Value": "false" } ] }
                      ]
                    }
                  ]
                }
              ]
            }
          ],
          "Tables": [
            {
              "Id": 100,
              "Name": "Temp Thing",
              "Properties": [ { "Name": "Tabletype", "Value": "temporary" } ],
              "Fields": [ { "Id": 1, "Name": "Code", "TypeDefinition": { "Name": "Code[10]" }, "Properties": [ { "Name": "ValidateTableRelation", "Value": "1" } ] } ]
            }
          ],
          "TableExtensions": [
            { "Id": 50000, "Name": "CustExt", "TargetObject": "#63ca2fa44f034f2ba480172fef340d3f#Email Address Lookup", "Fields": [ { "Id": 10, "Name": "Contact No.", "TypeDefinition": { "Name": "Code[20]" } } ] },
            { "Id": 50001, "Name": "PlainExt", "TargetObject": "Cust. Ledger Entry" }
          ]
        }
        """;

    [Fact]
    public void Reads_tables_from_namespaces_and_root()
    {
        var result = Extract(Json);

        Assert.Equal([18, 100], result.Tables.Select(t => t.Id).OrderBy(i => i));
        Assert.Equal("Microsoft.Sales", result.Tables.Single(t => t.Id == 18).Namespace);
        Assert.Null(result.Tables.Single(t => t.Id == 100).Namespace);
    }

    [Fact]
    public void Reads_table_properties_with_defaults_and_case_variants()
    {
        var result = Extract(Json);
        var customer = result.Tables.Single(t => t.Id == 18);
        var temp = result.Tables.Single(t => t.Id == 100);

        Assert.Equal("Normal", customer.TableType);
        Assert.False(customer.DataPerCompany);
        Assert.Equal(ObsoleteState.Pending, customer.ObsoleteState);
        Assert.Equal("temporary", temp.TableType);
        Assert.True(temp.DataPerCompany);
        Assert.Equal(ObsoleteState.No, temp.ObsoleteState);
    }

    [Fact]
    public void Primary_key_is_the_first_key_and_missing_keys_give_an_empty_list()
    {
        var result = Extract(Json);

        Assert.Equal(["No."], result.Tables.Single(t => t.Id == 18).PrimaryKeyFieldNames);
        Assert.Empty(result.Tables.Single(t => t.Id == 100).PrimaryKeyFieldNames);
    }

    [Fact]
    public void Reads_data_types_and_lengths()
    {
        var fields = Extract(Json).Tables.Single(t => t.Id == 18).Fields;

        Assert.Equal(("Code", 20), (fields[0].DataType, fields[0].Length));
        Assert.Equal(("Text", 100), (fields[1].DataType, fields[1].Length));
        Assert.Equal(("Enum \"Sales Document Type\"", (int?)null), (fields[2].DataType, fields[2].Length));
        Assert.Equal(("Option", (int?)null), (fields[3].DataType, fields[3].Length));
        Assert.Equal(("Decimal", (int?)null), (fields[4].DataType, fields[4].Length));
    }

    [Fact]
    public void Reads_field_class_relations_and_validation_flag()
    {
        var fields = Extract(Json).Tables.Single(t => t.Id == 18).Fields;

        var flowField = fields.Single(f => f.Id == 9);
        Assert.Equal(FieldClass.FlowField, flowField.FieldClass);
        Assert.Equal("sum(\"X\".Amount)", flowField.CalcFormulaText);
        Assert.Null(flowField.TableRelationText);

        var filter = fields.Single(f => f.Id == 10);
        Assert.Equal(FieldClass.FlowFilter, filter.FieldClass);
        Assert.Equal("Customer", filter.TableRelationText);
        Assert.False(filter.ValidateTableRelation);

        Assert.Equal(FieldClass.Normal, fields[0].FieldClass);
        Assert.True(fields[0].ValidateTableRelation);
    }

    [Fact]
    public void Reads_extension_targets_in_all_forms()
    {
        var extensions = Extract(Json).TableExtensions;

        var withApp = extensions.Single(e => e.Id == 50000);
        Assert.Equal("Email Address Lookup", withApp.ExtendedTableName);
        Assert.Equal(Guid.Parse("63ca2fa4-4f03-4f2b-a480-172fef340d3f"), withApp.ExtendedAppId);
        Assert.Equal("Contact No.", Assert.Single(withApp.Fields).Name);

        var plain = extensions.Single(e => e.Id == 50001);
        Assert.Equal("Cust. Ledger Entry", plain.ExtendedTableName);
        Assert.Null(plain.ExtendedAppId);
        Assert.Empty(plain.Fields);
    }

    [Fact]
    public void Missing_symbol_file_is_an_extraction_error()
    {
        Assert.Throws<MetadataExtractionException>(() => Extract(null));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{ \"Tables\": [ { \"Name\": \"NoId\" } ] }")]
    public void Invalid_symbol_file_is_an_extraction_error(string json)
    {
        Assert.Throws<MetadataExtractionException>(() => Extract(json));
    }

    [Fact]
    public void Extension_with_app_id_resolves_through_the_model_builder()
    {
        var systemApp = Guid.Parse("63ca2fa4-4f03-4f2b-a480-172fef340d3f");
        var other = Guid.NewGuid();
        var tables = new[]
        {
            new RawTable(1, "Thing", "A", "Normal", ObsoleteState.No, true, [new RawField(1, "Code", "Code", 10, FieldClass.Normal, ObsoleteState.No, null, true, null)], []),
            new RawTable(2, "Thing", "B", "Normal", ObsoleteState.No, true, [new RawField(1, "Code", "Code", 10, FieldClass.Normal, ObsoleteState.No, null, true, null)], []),
        };

        var result = Core.Analysis.ModelBuilder.Build(
            [
                new LoadedPackage("sys.app", new ExtractedPackage(new AppManifest(systemApp, "System", "Microsoft", "1", []), [tables[0]], [])),
                new LoadedPackage("oth.app", new ExtractedPackage(new AppManifest(other, "Other", "Microsoft", "1", []), [tables[1]],
                    [new RawTableExtension(50000, "ThingExt", "Thing", [new RawField(50000, "X", "Code", 10, FieldClass.Normal, ObsoleteState.No, null, true, null)], systemApp)])),
            ],
            new Core.Categorization.TableCategorizer(Core.Categorization.CategoryRules.Empty),
            [TableCategory.Data]);

        var extension = Assert.Single(result.TableExtensions);
        Assert.False(extension.IsOrphan);
        Assert.Equal(1, extension.ExtendedTableId);
    }
}
