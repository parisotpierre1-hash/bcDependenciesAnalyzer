# Business Central Extension Dependency Analyzer

A small command-line tool that reads Microsoft Dynamics 365 Business Central `.app` packages, rebuilds the table / field / relation model they define, and works out in which order the tables should be loaded into an empty company, for example with RapidStart configuration packages.

It is meant for occasional, single-user analysis work, not for production use.

## What it does

- Merges all selected apps into one logical model: tables, table extensions (fields keep their origin app), primary keys, `TableRelation` and `CalcFormula` definitions.
- Classifies each table (ledger entry, posted document, setup, system, ...) so you can leave out tables you would not migrate.
- Builds a dependency graph from table relations and computes dependency levels (a migration order).
- Detects circular dependencies and breaks them by listing the fields to load in a second pass ("deferred fields"). Circular dependencies made only of primary-key relations are reported as unresolvable.
- Stores everything in a SQL Server database (browsable in SSMS) and exports CSV files and a GraphML graph (for yEd, Gephi, Cytoscape).

The functional design is described in `BC_Extension_Dependency_Analyzer_MVP_Specification.md`.

## Prerequisites

- .NET 10 SDK
- SQL Server (Developer or Express edition is fine) reachable with Windows authentication. LocalDB works too with a suitable connection string.
- The `.app` packages to analyze, **including the platform `System` app**. Otherwise relations to tables such as `User` are reported as unresolved. Only the symbol file inside each package is used, so packages without source code work.

This repository does not contain any `.app` file. Provide your own packages and respect their licenses.

Tested with Business Central 23 packages; other versions should work since the symbol format is the same, including packages that use AL namespaces.

## Configuration

`src/BcDepAnalyzer.Cli/appsettings.json` (copied next to `analyzer.exe`):

| Key | Meaning |
|---|---|
| `ConnectionString` | SQL Server connection. The database is created by `db init` if missing. |
| `RulesFile` | Table categorization rules, relative to the executable |
| `IncludedCategories` | Categories part of the migration order (default `Setup`, `Data`) |
| `Csv.Delimiter` | CSV delimiter (single character) |

Command-line options override the configuration.

## Getting started

```powershell
dotnet build -c Release
$a = "src\BcDepAnalyzer.Cli\bin\Release\net10.0\analyzer.exe"

& $a db init                                         # creates the database and schema (once)
& $a analyze --apps C:\path\to\apps                   # a folder, or repeat --apps for files
& $a analyze --apps a.app b.app --include-categories Setup,Data,LedgerEntry
& $a export csv --out export-output --delimiter ";"
& $a export graphml --out export-output\model.graphml
& $a export graphml --out export-output\customer.graphml --tables Customer --depth 2
& $a export graphml --out export-output\base.graphml --app "Base Application" --all-relations
& $a inspect --app some.app --out inspect-output     # dumps manifest, symbols and sources (debugging aid)
```

Check the connection string in `src/BcDepAnalyzer.Cli/appsettings.json` first (default: `Server=localhost;Database=BcDependencyAnalyzer;Integrated Security=True`).

`analyze` replaces the stored model in one transaction. SSMS views for browsing: `vw_MigrationOrder`, `vw_DeferredFields`, `vw_TableFields`, `vw_Relations`.

GraphML options: `--app` (name or app ID), `--category`, `--tables` (ID or name) with `--depth` (default 1), `--all-relations`. Without filters the file contains the included categories and migration dependencies only. Open it in yEd and apply a Hierarchical layout.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success (warnings allowed) |
| 1 | Analysis error (unreadable package, duplicate app or table ID, ...) |
| 2 | Invalid arguments or configuration |
| 3 | Database error |

## Categorization rules

`src/BcDepAnalyzer.Cli/rules/default-categories.json`:

- `nameRules`: ordered list, first match wins. `*` is the only wildcard; the whole table name is matched, case-insensitive.
- `overrides`: table ID to category, evaluated first.
- System (ID >= 2000000000), temporary and external tables are categorized before name rules.

Categories: `System`, `Temporary`, `External`, `LedgerEntry`, `PostedDocument`, `Archive`, `Buffer`, `Log`, `Setup`, `Data`.

## Using the output with RapidStart

- `MigrationOrder.csv`: processing order of the tables (order inside a level is not significant).
- `DeferredFields.csv`: fields to exclude from the first package and load in a second pass (circular dependencies and self-references).
- Tables of an unresolvable dependency group (`IsUnresolvable`) have no level and need a manual decision. This only happens when a cycle consists solely of primary-key relations; it did not occur with the Business Central 23 base packages.

CSV cells starting with `=`, `+`, `-` or `@` are prefixed with `'` so spreadsheets do not evaluate them (for example `'-Sum(...)` for a CalcFormula).

## Tests

```powershell
dotnet test
```

Optional, skipped silently when not available:

- `BCDEP_TEST_CONNECTION`: connection string of a **disposable** database, enables the SQL Server integration tests (the schema is created and the content replaced).
- An `AppsSample` folder with Business Central 23 packages at the repository root enables the reference tests on real data. Do not commit it.

## Limitations

- Dependencies are derived from table metadata only. Relations created by business logic (for example `OnValidate` triggers run during import) are not visible.
- Conditional relations (`if ... else ...`) and FlowFields are stored and exported but are informational: they do not take part in the migration order.
- Relations with `ValidateTableRelation = false` are not treated as dependencies, since they are not enforced on import.
- The set of deferred fields is simple and deterministic, not minimal: all soft relations inside a dependency group are deferred.
- Table categories come from name patterns and are only a starting point. Adjust the rules file to your needs.
- Windows-oriented defaults (Windows authentication); other SQL Server setups need a different connection string.
