using System.Security.Cryptography;
using System.Text;
using Teledisk.Core.Domain;

namespace Teledisk.Data;

public sealed record ImportReport(int Added, int Skipped, long Bytes, int AlreadyPresent);

public sealed class LocalImportService(TelediskDatabase database)
{
    public const long LocalChannelId = -1;

    public const string LocalChannelTitle = "Import locale";

    private readonly SqliteMediaStore _store = new(database);

    public Task<ImportReport> ImportFolderAsync(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var root = Path.GetFullPath(folder);

        _store.UpsertChannel(new Channel
        {
            Id = LocalChannelId,
            Title = LocalChannelTitle,
            Kind = ChannelKind.Chat,
            IsSource = false,
        });

        var added = 0;
        var skipped = 0;
        var alreadyPresent = 0;
        long bytes = 0;

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = new FileInfo(file);
            if (info.Length == 0)
            {
                skipped++;
                continue;
            }

            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            var messageId = StableId(relative);
            if (_store.Exists(LocalChannelId, messageId))
            {
                alreadyPresent++;
                continue;
            }

            _store.Upsert(new MediaItem
            {
                ChannelId = LocalChannelId,
                MessageId = messageId,
                Name = relative,
                Size = info.Length,
                UpdatedAt = new DateTimeOffset(info.LastWriteTimeUtc),
            });
            added++;
            bytes += info.Length;
        }

        return Task.FromResult(new ImportReport(added, skipped, bytes, alreadyPresent));
    }

    private static int StableId(string relativePath)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(relativePath));
        return BitConverter.ToInt32(hash, 0) & 0x7FFFFFFF;
    }
}
