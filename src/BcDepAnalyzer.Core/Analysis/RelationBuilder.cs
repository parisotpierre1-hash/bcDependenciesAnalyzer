using BcDepAnalyzer.Core.Model;
using BcDepAnalyzer.Core.Parsing;

namespace BcDepAnalyzer.Core.Analysis;

internal static class RelationBuilder
{
    public static void Build(AnalysisResult result, TableNameResolver resolver)
    {
        var appNames = result.Apps.ToDictionary(a => a.AppId, a => a.Name);
        var parseErrors = new HashSet<RelationModel>();
        var resolveErrors = new Dictionary<RelationModel, string>();
        var rows = new List<RelationModel>();

        foreach (var table in result.Tables.OrderBy(t => t.Id))
        {
            foreach (var field in table.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.TableRelationText))
                {
                    AddTableRelation(result, resolver, appNames, table, field, rows, parseErrors, resolveErrors);
                }

                if (!string.IsNullOrWhiteSpace(field.CalcFormulaText))
                {
                    AddCalcFormula(result, resolver, appNames, table, field, rows, parseErrors, resolveErrors);
                }
            }
        }

        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].RelationId = i + 1;
        }

        result.Relations.AddRange(rows);
        Evaluate(result, appNames, parseErrors, resolveErrors);
    }

    private static void AddTableRelation(
        AnalysisResult result,
        TableNameResolver resolver,
        Dictionary<Guid, string> appNames,
        TableModel table,
        FieldModel field,
        List<RelationModel> rows,
        HashSet<RelationModel> parseErrors,
        Dictionary<RelationModel, string> resolveErrors)
    {
        var text = field.TableRelationText!;
        var parsed = TableRelationParser.Parse(text);

        if (!parsed.IsSuccess)
        {
            var failed = NewRow(table, field, RelationType.TableRelation, text);
            rows.Add(failed);
            parseErrors.Add(failed);
            result.Issues.Add(ParseErrorIssue(appNames, table, field, "TableRelation", parsed.Error!, text));
            return;
        }

        var relation = parsed.Value!;
        for (var i = 0; i < relation.Branches.Count; i++)
        {
            var branch = relation.Branches[i];
            var row = NewRow(table, field, RelationType.TableRelation, text, relation.IsConditional, i, branch.ConditionText, branch.WhereText);
            Resolve(resolver, branch.NameSegments, row, resolveErrors);
            rows.Add(row);
        }
    }

    private static void AddCalcFormula(
        AnalysisResult result,
        TableNameResolver resolver,
        Dictionary<Guid, string> appNames,
        TableModel table,
        FieldModel field,
        List<RelationModel> rows,
        HashSet<RelationModel> parseErrors,
        Dictionary<RelationModel, string> resolveErrors)
    {
        var text = field.CalcFormulaText!;
        var parsed = CalcFormulaParser.Parse(text);

        if (!parsed.IsSuccess)
        {
            var failed = NewRow(table, field, RelationType.FlowField, text);
            rows.Add(failed);
            parseErrors.Add(failed);
            result.Issues.Add(ParseErrorIssue(appNames, table, field, "CalcFormula", parsed.Error!, text));
            return;
        }

        var formula = parsed.Value!;
        var row = NewRow(table, field, RelationType.FlowField, text, formulaType: formula.FormulaType, whereText: formula.WhereText);
        Resolve(resolver, formula.NameSegments, row, resolveErrors);
        rows.Add(row);
    }

    private static RelationModel NewRow(
        TableModel table,
        FieldModel field,
        RelationType type,
        string text,
        bool isConditional = false,
        int branchIndex = 0,
        string? conditionText = null,
        string? whereText = null,
        string? formulaType = null) =>
        new()
        {
            SourceTableId = table.Id,
            SourceFieldId = field.Id,
            RelationType = type,
            FormulaType = formulaType,
            IsConditional = isConditional,
            BranchIndex = branchIndex,
            ConditionText = conditionText,
            WhereText = whereText,
            RelationText = text,
        };

    private static void Resolve(
        TableNameResolver resolver,
        IReadOnlyList<string> segments,
        RelationModel row,
        Dictionary<RelationModel, string> resolveErrors)
    {
        var lookup = resolver.Resolve(segments);
        row.TargetTableName = lookup.Table?.Name ?? string.Join('.', segments);
        row.TargetTableId = lookup.Table?.Id;
        row.TargetFieldName = lookup.FieldName;

        if (lookup.Error is not null)
        {
            resolveErrors[row] = lookup.Error;
        }
    }

    private static Issue ParseErrorIssue(Dictionary<Guid, string> appNames, TableModel table, FieldModel field, string property, string error, string text) =>
        new(
            IssueSeverity.Warning,
            IssueType.RelationParseError,
            appNames.GetValueOrDefault(field.AppId),
            $"{table.Name}.{field.Name}",
            $"Cannot parse {property} of field '{field.Name}' in table '{table.Name}': {error}. Text: {text}");

    private static void Evaluate(
        AnalysisResult result,
        Dictionary<Guid, string> appNames,
        HashSet<RelationModel> parseErrors,
        Dictionary<RelationModel, string> resolveErrors)
    {
        var included = result.IncludedCategories.ToHashSet();
        var tables = result.Tables.ToDictionary(t => t.Id);
        var excludedPairs = new HashSet<(int Source, int Target)>();

        foreach (var relation in result.Relations)
        {
            var sourceTable = tables[relation.SourceTableId];
            var sourceField = sourceTable.Fields.First(f => f.Id == relation.SourceFieldId);
            var target = relation.TargetTableId is int targetId ? tables[targetId] : null;

            if (relation.RelationType == RelationType.TableRelation)
            {
                relation.Strength = sourceField.IsPrimaryKey ? RelationStrength.Hard : RelationStrength.Soft;
            }

            relation.ExclusionReason = FirstFailingCondition(relation, sourceTable, sourceField, target, included);
            relation.IsMigrationDependency = relation.ExclusionReason is null;

            if (relation.ExclusionReason == ExclusionReason.Unresolved && !parseErrors.Contains(relation))
            {
                var reason = resolveErrors.GetValueOrDefault(relation, "table not found");
                result.Issues.Add(new Issue(
                    IssueSeverity.Warning,
                    IssueType.UnresolvedRelationTarget,
                    appNames.GetValueOrDefault(sourceField.AppId),
                    $"{sourceTable.Name}.{sourceField.Name}",
                    $"Relation of field '{sourceField.Name}' in table '{sourceTable.Name}' points to '{relation.TargetTableName}' ({reason})."));
            }
            else if (relation.ExclusionReason == ExclusionReason.ExcludedTarget
                && excludedPairs.Add((sourceTable.Id, target!.Id)))
            {
                result.Issues.Add(new Issue(
                    IssueSeverity.Warning,
                    IssueType.DependencyOnExcludedTable,
                    appNames.GetValueOrDefault(sourceTable.AppId),
                    sourceTable.Name,
                    $"Table '{sourceTable.Name}' depends on '{target.Name}', which is in the excluded category {target.Category}."));
            }
        }
    }

    private static ExclusionReason? FirstFailingCondition(
        RelationModel relation,
        TableModel sourceTable,
        FieldModel sourceField,
        TableModel? target,
        HashSet<TableCategory> included)
    {
        if (relation.RelationType == RelationType.FlowField)
        {
            return ExclusionReason.FlowField;
        }

        if (sourceField.FieldClass == FieldClass.FlowField)
        {
            return ExclusionReason.FlowField;
        }

        if (sourceField.FieldClass == FieldClass.FlowFilter)
        {
            return ExclusionReason.FlowFilter;
        }

        if (relation.IsConditional)
        {
            return ExclusionReason.Conditional;
        }

        if (!sourceField.ValidateTableRelation)
        {
            return ExclusionReason.NotValidated;
        }

        if (sourceTable.ObsoleteState == ObsoleteState.Removed
            || sourceField.ObsoleteState == ObsoleteState.Removed
            || target?.ObsoleteState == ObsoleteState.Removed)
        {
            return ExclusionReason.Obsolete;
        }

        if (!included.Contains(sourceTable.Category))
        {
            return ExclusionReason.ExcludedSource;
        }

        if (target is null)
        {
            return ExclusionReason.Unresolved;
        }

        if (!included.Contains(target.Category))
        {
            return ExclusionReason.ExcludedTarget;
        }

        if (target.Id == sourceTable.Id)
        {
            return ExclusionReason.SelfReference;
        }

        return null;
    }
}
