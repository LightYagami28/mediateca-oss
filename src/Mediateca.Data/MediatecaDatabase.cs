using Microsoft.Data.Sqlite;

namespace Mediateca.Data;

/// <summary>
/// Apre il database SQLite dell'installazione e applica le migrazioni.
/// </summary>
/// <remarks>
/// Schema versionato con <c>user_version</c>, migrazioni idempotenti e applicate in una
/// transazione: un'interruzione lascia il database alla versione precedente, mai a metà.
/// Come nell'originale il database è locale e unico per installazione.
/// </remarks>
public sealed class MediatecaDatabase
{
    /// <summary>Versione dello schema attesa dal codice.</summary>
    public const int SchemaVersion = 3;

    private readonly string _path;

    /// <summary>Crea l'accesso al database sul percorso indicato.</summary>
    public MediatecaDatabase(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = System.IO.Path.GetFullPath(path);
    }

    /// <summary>Percorso del file.</summary>
    public string Path => _path;

    /// <summary>Apre una connessione già configurata.</summary>
    public SqliteConnection Open()
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
        }.ToString());

        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    /// <summary>Applica le migrazioni mancanti.</summary>
    public int Migrate()
    {
        using var connection = Open();

        var current = ReadUserVersion(connection);
        if (current >= SchemaVersion)
        {
            return current;
        }

        using var transaction = connection.BeginTransaction();
        for (var version = current + 1; version <= SchemaVersion; version++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (version == 1)
            {
                command.CommandText = MigrationScripts.Script1;
            }
            else if (version == 2)
            {
                command.CommandText = MigrationScripts.Script2;
            }
            else
            {
                command.CommandText = MigrationScripts.Script3;
            }

            command.ExecuteNonQuery();
        }

        using (var bump = connection.CreateCommand())
        {
            bump.Transaction = transaction;
            // SQLite non accetta parametri in PRAGMA: il valore è letterale e resta
            // allineato a SchemaVersion.
            bump.CommandText = "PRAGMA user_version=3;";
            bump.ExecuteNonQuery();
        }

        transaction.Commit();
        return SchemaVersion;
    }

    /// <summary>Versione schema attualmente memorizzata.</summary>
    public int CurrentVersion()
    {
        using var connection = Open();
        return ReadUserVersion(connection);
    }

    /// <summary>Esegue <c>PRAGMA integrity_check</c> e restituisce l'esito.</summary>
    public bool CheckIntegrity(out string detail)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        detail = Convert.ToString(command.ExecuteScalar()) ?? "";
        return string.Equals(detail, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }
}

/// <summary>Numero di migrazioni disponibili, una per versione dello schema.</summary>
public static class Migrations
{
    public const int Count = 3;
}

