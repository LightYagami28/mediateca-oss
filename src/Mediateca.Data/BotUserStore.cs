using System.Globalization;
using Mediateca.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Mediateca.Data;

public sealed class BotUserStore
{
    private readonly MediatecaDatabase _database;
    private readonly HashSet<long> _bootstrapAdmins;

    public BotUserStore(MediatecaDatabase database, IEnumerable<long>? bootstrapAdmins = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _bootstrapAdmins = [.. bootstrapAdmins ?? []];
    }

    public BotUser Touch(long userId, string? username, DateTimeOffset? now = null)
    {
        var seen = (now ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        using var connection = _database.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO bot_users (user_id, username, role, first_seen, last_seen)
                VALUES ($id, $username, CASE WHEN $bootstrap THEN 'admin' ELSE 'user' END, $seen, $seen)
                ON CONFLICT(user_id) DO UPDATE SET
                    username  = excluded.username,
                    last_seen = excluded.last_seen,
                    role      = CASE WHEN $bootstrap THEN 'admin' ELSE bot_users.role END;
                """;
            command.Parameters.AddWithValue("$id", userId);
            command.Parameters.AddWithValue("$username", (object?)username ?? DBNull.Value);
            command.Parameters.AddWithValue("$bootstrap", _bootstrapAdmins.Contains(userId));
            command.Parameters.AddWithValue("$seen", seen);
            command.ExecuteNonQuery();
        }

        return Find(connection, userId)!;
    }

    public BotUser? Find(long userId)
    {
        using var connection = _database.Open();
        return Find(connection, userId);
    }

    public bool SetRole(long userId, BotUserRole role)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE bot_users SET role = $role WHERE user_id = $id;";
        command.Parameters.AddWithValue("$role", role.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("$id", userId);
        return command.ExecuteNonQuery() > 0;
    }

    public IReadOnlyList<BotUser> List(int limit = 50)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT user_id, username, role, first_seen, last_seen FROM bot_users ORDER BY last_seen DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));
        using var reader = command.ExecuteReader();
        var users = new List<BotUser>();
        while (reader.Read())
        {
            users.Add(Read(reader));
        }

        return users;
    }

    private static BotUser? Find(SqliteConnection connection, long userId)
    {
        if (connection != null)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT user_id, username, role, first_seen, last_seen FROM bot_users WHERE user_id = $id;";
            command.Parameters.AddWithValue("$id", userId);
            using var reader = command.ExecuteReader();
            return reader.Read() ? Read(reader) : null;
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static BotUser Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Username = reader.IsDBNull(1) ? null : reader.GetString(1),
        Role = Enum.TryParse<BotUserRole>(reader.GetString(2), ignoreCase: true, out var role) ? role : BotUserRole.User,
        FirstSeen = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
        LastSeen = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
    };
}
