using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Mediateca.Data;

/// <summary>Lettura dei timestamp SQLite con format provider invariante.</summary>
internal static class SqliteTimestamps
{
    internal static DateTimeOffset? ReadNullable(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
        || !DateTimeOffset.TryParse(reader.GetString(ordinal), CultureInfo.InvariantCulture, out var value)
            ? null
            : value;

    internal static DateTimeOffset Read(SqliteDataReader reader, int ordinal, DateTimeOffset fallback) =>
        ReadNullable(reader, ordinal) ?? fallback;
}
