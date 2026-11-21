using System.Globalization;
using Mediateca.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Mediateca.Data;

public sealed class AddressBookStore
{
    private readonly MediatecaDatabase _database;

    public AddressBookStore(MediatecaDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public int Upsert(IEnumerable<AddressBookEntry> entries)
    {
        if (entries != null)
        {
            ArgumentNullException.ThrowIfNull(entries);
            using var connection = _database.Open();
            using var transaction = connection.BeginTransaction();
            var count = 0;
            foreach (var entry in entries)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO address_book (peer_id, access_hash, kind, title, username, phone, updated_at)
                    VALUES ($id, $hash, $kind, $title, $username, $phone, $updated)
                    ON CONFLICT(peer_id) DO UPDATE SET
                        access_hash = COALESCE(excluded.access_hash, address_book.access_hash),
                        kind        = excluded.kind,
                        title       = excluded.title,
                        username    = excluded.username,
                        phone       = COALESCE(excluded.phone, address_book.phone),
                        updated_at  = excluded.updated_at;
                    """;
                command.Parameters.AddWithValue("$id", entry.PeerId);
                command.Parameters.AddWithValue("$hash", (object?)entry.AccessHash ?? DBNull.Value);
                command.Parameters.AddWithValue("$kind", entry.Kind.ToString().ToLowerInvariant());
                command.Parameters.AddWithValue("$title", entry.Title);
                command.Parameters.AddWithValue("$username", (object?)entry.Username ?? DBNull.Value);
                command.Parameters.AddWithValue("$phone", (object?)entry.Phone ?? DBNull.Value);
                command.Parameters.AddWithValue("$updated", entry.UpdatedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
                count++;
            }

            transaction.Commit();
            return count;
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    public IReadOnlyList<AddressBookEntry> Search(string text, AddressBookKind? kind = null, int limit = 100)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT peer_id, access_hash, kind, title, username, phone, updated_at
            FROM address_book
            WHERE ($text = '' OR title LIKE '%' || $text || '%' OR IFNULL(username, '') LIKE '%' || $text || '%')
              AND ($kind IS NULL OR kind = $kind)
            ORDER BY title COLLATE NOCASE
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$text", (text ?? string.Empty).Trim().TrimStart('@'));
        command.Parameters.AddWithValue("$kind", kind is null ? DBNull.Value : kind.Value.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 1000));
        using var reader = command.ExecuteReader();
        var entries = new List<AddressBookEntry>();
        while (reader.Read())
        {
            entries.Add(Read(reader));
        }

        return entries;
    }

    public bool Remove(long peerId)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM address_book WHERE peer_id = $id;";
        command.Parameters.AddWithValue("$id", peerId);
        return command.ExecuteNonQuery() > 0;
    }

    private static AddressBookEntry Read(SqliteDataReader reader) => new()
    {
        PeerId = reader.GetInt64(0),
        AccessHash = reader.IsDBNull(1) ? null : reader.GetInt64(1),
        Kind = Enum.TryParse<AddressBookKind>(reader.GetString(2), ignoreCase: true, out var kind) ? kind : AddressBookKind.User,
        Title = reader.GetString(3),
        Username = reader.IsDBNull(4) ? null : reader.GetString(4),
        Phone = reader.IsDBNull(5) ? null : reader.GetString(5),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
    };
}
