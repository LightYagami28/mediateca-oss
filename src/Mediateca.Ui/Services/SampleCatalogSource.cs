using System.Security.Cryptography;
using Mediateca.Ui.Models;

namespace Mediateca.Ui.Services;

public sealed class SampleCatalogSource(int count = 5_000) : ICatalogSource
{
    private static readonly string[] Channels =
    [
        "Cinema Atlantide", " Archivio RAI", "Musica Classica", "Fumetti Grandi",
        "Documentari HD", "Audiolibri IT", "Lossless Audio", "Serie TV Premium",
    ];

    private static readonly string[] MovieTitles =
    [
        "Il secondo respiro", "Ombra lunga", "La citta' sospesa", "Notturnofahren",
        "Il custode delle maree", "Glasshouse", "Terra di mezzo", "L'ultima promessa",
    ];

    private static readonly string[] SeriesTitles =
    [
        "Rimorso", "Codice interno", "Il commissario", "Sesta stagione",
    ];

    public Task<IReadOnlyList<CatalogRow>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CatalogRow>>(BuildRows(cancellationToken));

    public Task<IReadOnlyList<CatalogRow>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        var rows = BuildRows(cancellationToken);
        var filtered = trimmed.Length == 0
            ? rows
            : rows.Where(row =>
                row.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                row.Channel.Contains(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();

        return Task.FromResult<IReadOnlyList<CatalogRow>>(filtered.Take(Math.Max(limit, 1)).ToList());
    }

    private List<CatalogRow> BuildRows(CancellationToken cancellationToken)
    {
        var rows = new List<CatalogRow>(count);

        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var channel = Channels[i % Channels.Length];
            var kindIndex = i % 5;
            var (kind, label, color, extension, name) = kindIndex switch
            {
                0 => (0, "Film", "#F29937", "mkv", $"{MovieTitles[i % MovieTitles.Length]} ({1998 + (i % 28)}).mkv"),
                1 => (1, "Serie", "#6C8BFF", "mp4", $"{SeriesTitles[i % SeriesTitles.Length]} S{1 + (i % 6):00}E{1 + (i % 12):00}.mp4"),
                2 => (2, "Musica", "#E86AF0", "flac", $"{MovieTitles[(i * 3) % MovieTitles.Length]} - {i % 12 + 1:00} track.flac"),
                3 => (3, "Libro", "#3FD08A", "epub", $"Volume {i % 40 + 1:00} - {MovieTitles[(i * 5) % MovieTitles.Length]}.epub"),
                _ => (4, "Documento", "#F2B84B", "pdf", $"Relazione {i % 90 + 1:00} - {MovieTitles[(i * 7) % MovieTitles.Length]}.pdf"),
            };

            rows.Add(new CatalogRow
            {
                Id = i + 1,
                Name = name,
                Extension = extension,
                KindLabel = label,
                KindColor = color,
                Channel = channel,
                Size = kind switch
                {
                    0 => 1_500_000_000L + NextInt64(0, 6_000_000_000),
                    1 => 400_000_000L + NextInt64(0, 1_200_000_000),
                    2 => 30_000_000L + NextInt64(0, 90_000_000),
                    3 => 2_000_000L + NextInt64(0, 12_000_000),
                    _ => 500_000L + NextInt64(0, 40_000_000),
                },
                DurationSeconds = kind is 0 or 1 or 2
                    ? 900 + RandomNumberGenerator.GetInt32(0, 5_400)
                    : null,
                Year = kind is 3 or 4 ? null : 1998 + (i % 28),
                UpdatedAt = DateTimeOffset.UtcNow.AddHours(-(i % 720)),
            });
        }

        return rows;
    }

    private static long NextInt64(long fromInclusive, long toExclusive)
    {
        var range = (ulong)(toExclusive - fromInclusive);
        var limit = ulong.MaxValue - (ulong.MaxValue % range);
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong candidate;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            candidate = BitConverter.ToUInt64(bytes);
        }
        while (candidate >= limit);

        return fromInclusive + (long)(candidate % range);
    }
}
