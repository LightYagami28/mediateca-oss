using Microsoft.Data.Sqlite;
using Teledisk.Core.Domain;

namespace Teledisk.Data;

/// <summary>Lettura e scrittura del catalogo media.</summary>
public sealed class SqliteMediaStore
{
    private readonly TelediskDatabase _database;

    /// <summary>Crea lo store sul database indicato.</summary>
    public SqliteMediaStore(TelediskDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>Inserisce o aggiorna un elemento in base alla coppia canale/messaggio.</summary>
    public long Upsert(MediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO media_items (channel_id, message_id, parent_id, name, kind, size, duration_s, width, height, title, year, season, episode, external_id, cover_path, updated_at)
            VALUES ($channel, $message, $parent, $name, $kind, $size, $duration, $width, $height, $title, $year, $season, $episode, $external, $cover, $updated)
            ON CONFLICT (channel_id, message_id) DO UPDATE SET
                parent_id  = excluded.parent_id,
                name       = excluded.name,
                kind       = excluded.kind,
                size       = excluded.size,
                duration_s = excluded.duration_s,
                width      = excluded.width,
                height     = excluded.height,
                title      = excluded.title,
                year       = excluded.year,
                season     = excluded.season,
                episode    = excluded.episode,
                external_id= excluded.external_id,
                cover_path = excluded.cover_path,
                updated_at = excluded.updated_at
            RETURNING id;
            """;
        command.Parameters.AddWithValue("$channel", item.ChannelId);
        command.Parameters.AddWithValue("$message", item.MessageId);
        command.Parameters.AddWithValue("$parent", (object?)item.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", item.Name);
        command.Parameters.AddWithValue("$kind", item.Kind.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$size", item.Size);
        command.Parameters.AddWithValue("$duration", (object?)item.DurationSeconds ?? DBNull.Value);
        command.Parameters.AddWithValue("$width", (object?)item.Width ?? DBNull.Value);
        command.Parameters.AddWithValue("$height", (object?)item.Height ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
        command.Parameters.AddWithValue("$year", (object?)item.Year ?? DBNull.Value);
        command.Parameters.AddWithValue("$season", (object?)item.Season ?? DBNull.Value);
        command.Parameters.AddWithValue("$episode", (object?)item.Episode ?? DBNull.Value);
        command.Parameters.AddWithValue("$external", (object?)item.ExternalId ?? DBNull.Value);
        command.Parameters.AddWithValue("$cover", (object?)item.CoverPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", item.UpdatedAt.UtcDateTime.ToString("O"));

        return Convert.ToInt64(command.ExecuteScalar());
    }

    /// <summary>L'elemento con l'identificativo indicato, oppure <c>null</c>.</summary>
    public MediaItem? Find(long id)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, channel_id, message_id, parent_id, name, kind, size, duration_s, width, height,
                   title, year, season, episode, external_id, cover_path, updated_at
            FROM media_items
            WHERE id = $id
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    /// <summary>True quando la coppia canale/messaggio esiste già.</summary>
    public bool Exists(long channelId, int messageId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM media_items WHERE channel_id = $channel AND message_id = $message LIMIT 1;";
        command.Parameters.AddWithValue("$channel", channelId);
        command.Parameters.AddWithValue("$message", messageId);
        using var reader = command.ExecuteReader();
        return reader.Read();
    }

    /// <summary>Inserisce o aggiorna un canale.</summary>
    public void UpsertChannel(Channel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO channels (id, access_hash, title, username, kind, is_source, last_scan_at, item_count, last_message_id)
            VALUES ($id, $access, $title, $username, $kind, $source, $scan, $count, $lastMessage)
            ON CONFLICT (id) DO UPDATE SET
                title           = excluded.title,
                username        = excluded.username,
                kind            = excluded.kind,
                is_source       = excluded.is_source,
                last_scan_at    = excluded.last_scan_at,
                item_count      = excluded.item_count,
                last_message_id = excluded.last_message_id;
            """;
        command.Parameters.AddWithValue("$id", channel.Id);
        command.Parameters.AddWithValue("$access", (object?)channel.AccessHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", channel.Title);
        command.Parameters.AddWithValue("$username", (object?)channel.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("$kind", channel.Kind.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$source", channel.IsSource ? 1 : 0);
        command.Parameters.AddWithValue("$scan", (object?)channel.LastScanAt?.UtcDateTime.ToString("O") ?? DBNull.Value);
        command.Parameters.AddWithValue("$count", channel.ItemCount);
        command.Parameters.AddWithValue("$lastMessage", channel.LastMessageId);
        command.ExecuteNonQuery();
    }

    /// <summary>Cerca elementi per parte del nome o del titolo.</summary>
    public IReadOnlyList<MediaItem> Search(string text, MediaKind? kind = null, int limit = 100)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, channel_id, message_id, parent_id, name, kind, size, duration_s, width, height,
                   title, year, season, episode, external_id, cover_path, updated_at
            FROM media_items
            WHERE ($text = '' OR name LIKE '%' || $text || '%' OR IFNULL(title,'') LIKE '%' || $text || '%')
              AND ($kind IS NULL OR kind = $kind)
            ORDER BY updated_at DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$text", text ?? "");
        command.Parameters.AddWithValue("$kind", kind is null ? DBNull.Value : kind.Value.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<MediaItem>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(Read(reader));
        }

        return results;
    }

    /// <summary>Statistiche sintetiche della libreria.</summary>
    public (int Items, long Bytes, int Channels) Statistics()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(*) FROM media_items),
                   (SELECT IFNULL(SUM(size),0) FROM media_items),
                   (SELECT COUNT(*) FROM channels WHERE is_source = 1);
            """;
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt64(1), reader.GetInt32(2)) : (0, 0, 0);
    }

    /// <summary>Tutti i canali noti, ordinati per titolo.</summary>
    public IReadOnlyList<Channel> ListChannels()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, access_hash, title, username, kind, is_source, last_scan_at, item_count, last_message_id
            FROM channels
            ORDER BY title COLLATE NOCASE;
            """;

        var results = new List<Channel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadChannel(reader));
        }

        return results;
    }

    private static MediaItem Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ChannelId = reader.GetInt64(1),
        MessageId = reader.GetInt32(2),
        ParentId = reader.IsDBNull(3) ? null : reader.GetInt64(3),
        Name = reader.GetString(4),
        Kind = Enum.TryParse<MediaKind>(reader.GetString(5), ignoreCase: true, out var kind) ? kind : MediaKind.Unknown,
        Size = reader.GetInt64(6),
        DurationSeconds = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        Width = reader.IsDBNull(8) ? null : reader.GetInt32(8),
        Height = reader.IsDBNull(9) ? null : reader.GetInt32(9),
        Title = reader.IsDBNull(10) ? null : reader.GetString(10),
        Year = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        Season = reader.IsDBNull(12) ? null : reader.GetInt32(12),
        Episode = reader.IsDBNull(13) ? null : reader.GetInt32(13),
        ExternalId = reader.IsDBNull(14) ? null : reader.GetString(14),
        CoverPath = reader.IsDBNull(15) ? null : reader.GetString(15),
        UpdatedAt = DateTimeOffset.TryParse(reader.GetString(16), out var stamp) ? stamp : DateTimeOffset.UtcNow,
    };

    private static Channel ReadChannel(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        AccessHash = reader.IsDBNull(1) ? null : reader.GetInt64(1),
        Title = reader.GetString(2),
        Username = reader.IsDBNull(3) ? null : reader.GetString(3),
        Kind = Enum.TryParse<ChannelKind>(reader.GetString(4), ignoreCase: true, out var kind) ? kind : ChannelKind.Channel,
        IsSource = reader.GetInt32(5) != 0,
        LastScanAt = reader.IsDBNull(6) ? null : DateTimeOffset.TryParse(reader.GetString(6), out var scan) ? scan : null,
        ItemCount = reader.GetInt32(7),
        LastMessageId = reader.GetInt32(8),
    };
}
