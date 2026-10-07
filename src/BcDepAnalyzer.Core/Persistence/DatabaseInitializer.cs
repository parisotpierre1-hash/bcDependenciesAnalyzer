using System.Globalization;
using Microsoft.Data.SqlClient;

namespace BcDepAnalyzer.Core.Persistence;

public sealed class DatabaseInitializer(string connectionString)
{
    private const string ScriptMarker = ".Scripts.";

    public void Initialize()
    {
        try
        {
            CreateDatabaseIfMissing();
            ApplyScripts();
        }
        catch (SqlException ex)
        {
            throw new DatabaseException($"Database initialization failed: {ex.Message}", ex);
        }
    }

    private void CreateDatabaseIfMissing()
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new ConfigurationException("The connection string must specify a database (Database=...).");
        }

        builder.InitialCatalog = "master";
        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@name) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name);
                EXEC (@sql);
            END
            """;
        command.Parameters.AddWithValue("@name", database);
        command.ExecuteNonQuery();
    }

    private void ApplyScripts()
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using (var create = connection.CreateCommand())
        {
            create.CommandText = """
                IF OBJECT_ID('dbo.SchemaVersion') IS NULL
                    CREATE TABLE dbo.SchemaVersion (
                        Version int NOT NULL CONSTRAINT PK_SchemaVersion PRIMARY KEY,
                        AppliedAt datetime2 NOT NULL
                    );
                """;
            create.ExecuteNonQuery();
        }

        var applied = new HashSet<int>();
        using (var read = connection.CreateCommand())
        {
            read.CommandText = "SELECT Version FROM dbo.SchemaVersion";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                applied.Add(reader.GetInt32(0));
            }
        }

        foreach (var (version, sql) in ReadScripts().Where(s => !applied.Contains(s.Version)))
        {
            using var transaction = connection.BeginTransaction();
            foreach (var batch in SplitBatches(sql))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = batch;
                command.ExecuteNonQuery();
            }

            using var record = connection.CreateCommand();
            record.Transaction = transaction;
            record.CommandText = "INSERT INTO dbo.SchemaVersion (Version, AppliedAt) VALUES (@version, SYSUTCDATETIME())";
            record.Parameters.AddWithValue("@version", version);
            record.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    private static IEnumerable<(int Version, string Sql)> ReadScripts()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        var scripts = new List<(int Version, string Sql)>();

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            var marker = resource.IndexOf(ScriptMarker, StringComparison.Ordinal);
            if (marker < 0 || !resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = resource[(marker + ScriptMarker.Length)..];
            var underscore = fileName.IndexOf('_');
            if (underscore <= 0 || !int.TryParse(fileName[..underscore], NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                throw new InvalidOperationException($"Script resource '{resource}' must be named NNN_description.sql.");
            }

            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            scripts.Add((version, reader.ReadToEnd()));
        }

        return scripts.OrderBy(s => s.Version);
    }

    internal static IEnumerable<string> SplitBatches(string script)
    {
        var current = new List<string>();

        foreach (var line in script.Split('\n'))
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Any(l => l.Trim().Length > 0))
                {
                    yield return string.Join('\n', current);
                }

                current.Clear();
            }
            else
            {
                current.Add(line.TrimEnd('\r'));
            }
        }

        if (current.Any(l => l.Trim().Length > 0))
        {
            yield return string.Join('\n', current);
        }
    }
}
