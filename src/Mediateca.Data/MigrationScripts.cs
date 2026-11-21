namespace Mediateca.Data;

/// <summary>Script SQL di migrazione, separati dalla logica di applicazione.</summary>
internal static class MigrationScripts
{
    internal const string Script1 = """
            
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

    internal const string Script2 = """
            
                                CREATE TABLE IF NOT EXISTS bot_users (
                                    user_id    INTEGER PRIMARY KEY,
                                    username   TEXT,
                                    role       TEXT NOT NULL DEFAULT 'user',
                                    first_seen TEXT NOT NULL,
                                    last_seen  TEXT NOT NULL
                                );
            
            """;

    internal const string Script3 = """
            
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

