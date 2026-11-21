using Microsoft.Data.Sqlite;

namespace Mediateca.Data;

public sealed record TransferJob
{
    public required long Id { get; init; }

    public long? ItemId { get; init; }

    public required string Direction { get; init; }

    public required string State { get; init; }

    public long BytesTotal { get; init; }

    public long BytesDone { get; init; }

    public string? Error { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed class TransferStore(MediatecaDatabase database)
{
    public long Enqueue(long itemId, string direction)
    {
        if (direction != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(direction);
            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO transfer_jobs (item_id, direction, state, bytes_total, bytes_done, error, created_at, updated_at)
                VALUES ($item, $direction, 'queued', 0, 0, NULL, $now, $now)
                RETURNING id;
                """;
            command.Parameters.AddWithValue("$item", itemId);
            command.Parameters.AddWithValue("$direction", direction);
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            return Convert.ToInt64(command.ExecuteScalar());
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    public TransferJob? Find(long id)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, item_id, direction, state, bytes_total, bytes_done, error, created_at, updated_at
            FROM transfer_jobs WHERE id = $id LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? Read(reader) : null;
    }

    public IReadOnlyList<TransferJob> List(string? state = null)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = state is null
            ? """
              SELECT id, item_id, direction, state, bytes_total, bytes_done, error, created_at, updated_at
              FROM transfer_jobs ORDER BY id;
              """
            : """
              SELECT id, item_id, direction, state, bytes_total, bytes_done, error, created_at, updated_at
              FROM transfer_jobs WHERE state = $state ORDER BY id;
              """;
        if (state is not null)
        {
            command.Parameters.AddWithValue("$state", state);
        }

        var jobs = new List<TransferJob>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            jobs.Add(Read(reader));
        }

        return jobs;
    }

    public void MarkRunning(long id)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE transfer_jobs SET state = 'running', updated_at = $now WHERE id = $id AND state = 'queued';";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void UpdateProgress(long id, long bytesDone, long bytesTotal)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE transfer_jobs SET bytes_done = $done, bytes_total = $total, updated_at = $now WHERE id = $id;";
        command.Parameters.AddWithValue("$done", bytesDone);
        command.Parameters.AddWithValue("$total", bytesTotal);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Complete(long id)
    {
        using var connection = database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE transfer_jobs SET state = 'completed', bytes_done = bytes_total, updated_at = $now WHERE id = $id;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void Fail(long id, string error)
    {
        if (error != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE transfer_jobs SET state = 'failed', error = $error, updated_at = $now WHERE id = $id;";
            command.Parameters.AddWithValue("$error", error);
            command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static TransferJob Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ItemId = reader.IsDBNull(1) ? null : reader.GetInt64(1),
        Direction = reader.GetString(2),
        State = reader.GetString(3),
        BytesTotal = reader.GetInt64(4),
        BytesDone = reader.GetInt64(5),
        Error = reader.IsDBNull(6) ? null : reader.GetString(6),
        CreatedAt = SqliteTimestamps.Read(reader, 7, DateTimeOffset.UtcNow),
        UpdatedAt = SqliteTimestamps.Read(reader, 8, DateTimeOffset.UtcNow),
    };
}
