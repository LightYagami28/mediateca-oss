using Microsoft.Data.Sqlite;
using Teledisk.Data;

namespace Teledisk.Tests;

/// <summary>Test di <see cref="TelediskDatabase"/>: migrazioni, versione schema e integrità.</summary>
public sealed class DatabaseTests
{
    /// <summary>La migrazione porta il file nuovo alla versione corrente dello schema.</summary>
    [Fact]
    public void Migrate_PortaAllaVersioneCorrente()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);

            Assert.Equal(TelediskDatabase.SchemaVersion, database.Migrate());
            Assert.Equal(TelediskDatabase.SchemaVersion, database.CurrentVersion());
            Assert.True(File.Exists(database.Path));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Una seconda migrazione non cambia nulla e non fallisce.</summary>
    [Fact]
    public void Migrate_ÈIdempotente()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);

            Assert.Equal(1, database.Migrate());
            Assert.Equal(1, database.Migrate());
            Assert.Equal(1, database.CurrentVersion());
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Lo schema creato contiene tutte le tabelle attese.</summary>
    [Fact]
    public void Migrate_CreaLeTabelleAttese()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);
            database.Migrate();

            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";

            var names = new HashSet<string>(StringComparer.Ordinal);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                names.Add(reader.GetString(0));
            }

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "channels", "media_items", "transfer_jobs", "backups", "logs",
            };
            Assert.Superset(expected, names);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Ogni versione dichiarata ha la propria istruzione di migrazione (oggi una sola).</summary>
    [Fact]
    public void Migrazioni_CopronoTutteLeVersioni()
    {
        Assert.Single(Migrations.Statements);
    }

    /// <summary>L'integrità del database appena migrato è valida.</summary>
    [Fact]
    public void CheckIntegrity_RestituisceOk()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);
            database.Migrate();

            Assert.True(database.CheckIntegrity(out var detail));
            Assert.Equal("ok", detail);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>La connessione abilita i pragma attesi: foreign key, WAL e timeout.</summary>
    [Fact]
    public void Open_ConfiguraIPragma()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);
            database.Migrate();

            using var connection = database.Open();

            Assert.Equal(1L, Convert.ToInt64(Scalar(connection, "PRAGMA foreign_keys;")));
            Assert.Equal("wal", Convert.ToString(Scalar(connection, "PRAGMA journal_mode;")));
            Assert.Equal(5_000L, Convert.ToInt64(Scalar(connection, "PRAGMA busy_timeout;")));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>L'apertura crea la cartella del file quando manca.</summary>
    [Fact]
    public void Open_CreaLaCartellaMancante()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "nuova", "cartella", "teledisk.db"));

            using (var connection = database.Open())
            {
                Assert.True(File.Exists(database.Path));
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Un percorso vuoto non è accettato.</summary>
    [Fact]
    public void Constructor_ValidaIlPercorso()
    {
        Assert.Throws<ArgumentException>(() => new TelediskDatabase("  "));
        Assert.Throws<ArgumentNullException>(() => new TelediskDatabase(null!));
    }

    private static TelediskDatabase NewDatabase(string directory) =>
        new(Path.Combine(directory, "teledisk.db"));

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "teledisk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }

                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
