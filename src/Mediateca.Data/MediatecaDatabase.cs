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
                command.CommandText = """
                        CREATE TABLE IF NOT EXISTS channels (
                            id              INTEGER PRIMARY KEY,
                            access_hash     INTEGER,
                            title           TEXT NOT NULL,
                            username        TEXT,
                            kind            TEXT NOT NULL DEFAULT 'channel',
                            is_source       INTEGER NOT NULL DEFAULT 0,
                            last_scan_at    TEXT,
                            item_count      INTEGER NOT NULL DEFAULT 0,
                            last_message_id INTEGER NOT NULL DEFAULT 0
                        );

                        CREATE TABLE IF NOT EXISTS media_items (
                            id            INTEGER PRIMARY KEY AUTOINCREMENT,
                            channel_id    INTEGER NOT NULL REFERENCES channels(id) ON DELETE CASCADE,
                            message_id    INTEGER NOT NULL,
                            parent_id     INTEGER REFERENCES media_items(id) ON DELETE CASCADE,
                            name          TEXT NOT NULL,
                            kind          TEXT NOT NULL DEFAULT 'unknown',
                            size          INTEGER NOT NULL DEFAULT 0,
                            duration_s    INTEGER,
                            width         INTEGER,
                            height        INTEGER,
                            title         TEXT,
                            year          INTEGER,
                            season        INTEGER,
                            episode       INTEGER,
                            external_id   TEXT,
                            cover_path    TEXT,
                            updated_at    TEXT NOT NULL,
                            UNIQUE (channel_id, message_id)
                        );

                        CREATE INDEX IF NOT EXISTS idx_media_name   ON media_items(name);
                        CREATE INDEX IF NOT EXISTS idx_media_kind   ON media_items(kind);
                        CREATE INDEX IF NOT EXISTS idx_media_parent ON media_items(parent_id);
                        CREATE INDEX IF NOT EXISTS idx_media_title  ON media_items(title);

                        CREATE TABLE IF NOT EXISTS transfer_jobs (
                            id          INTEGER PRIMARY KEY AUTOINCREMENT,
                            item_id     INTEGER REFERENCES media_items(id) ON DELETE SET NULL,
                            direction   TEXT NOT NULL,
                            state       TEXT NOT NULL DEFAULT 'queued',
                            bytes_total INTEGER NOT NULL DEFAULT 0,
                            bytes_done  INTEGER NOT NULL DEFAULT 0,
                            error       TEXT,
                            created_at  TEXT NOT NULL,
                            updated_at  TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS backups (
                            id         INTEGER PRIMARY KEY AUTOINCREMENT,
                            path       TEXT NOT NULL,
                            bytes      INTEGER NOT NULL DEFAULT 0,
                            scope      TEXT NOT NULL DEFAULT 'database',
                            created_at TEXT NOT NULL
                        );

                        CREATE TABLE IF NOT EXISTS logs (
                            id         INTEGER PRIMARY KEY AUTOINCREMENT,
                            level      TEXT NOT NULL,
                            scope      TEXT NOT NULL DEFAULT '',
                            message    TEXT NOT NULL,
                            created_at TEXT NOT NULL
                        );
                        """;
            }
            else if (version == 2)
            {
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS bot_users (
                        user_id    INTEGER PRIMARY KEY,
                        username   TEXT,
                        role       TEXT NOT NULL DEFAULT 'user',
                        first_seen TEXT NOT NULL,
                        last_seen  TEXT NOT NULL
                    );
                    """;
            }
            else
            {
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS address_book (
                        peer_id     INTEGER PRIMARY KEY,
                        access_hash INTEGER,
                        kind        TEXT NOT NULL DEFAULT 'user',
                        title       TEXT NOT NULL,
                        username    TEXT,
                        phone       TEXT,
                        updated_at  TEXT NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_address_book_title ON address_book(title);
                    """;
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
