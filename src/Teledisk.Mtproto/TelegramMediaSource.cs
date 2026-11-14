using System.Globalization;
using Microsoft.Extensions.Logging;
using Teledisk.Core.Domain;
using Teledisk.Data;
using Teledisk.Server;
using TL;
using WTelegram;
using DomainChannel = Teledisk.Core.Domain.Channel;

namespace Teledisk.Mtproto;

public sealed class MtprotoOptions
{
    public const string SectionName = "Mtproto";

    public int ApiId { get; set; }

    public string ApiHash { get; set; } = string.Empty;

    public string SessionPath { get; set; } = string.Empty;

    public string CacheDirectory { get; set; } = string.Empty;
}

public sealed class TelegramMediaSource : IMediaFileSource, IAsyncDisposable
{
    private readonly MtprotoOptions _options;
    private readonly TelediskDatabase _database;
    private readonly ILogger<TelegramMediaSource> _logger;
    private Client? _client;
    private bool _connected;

    public TelegramMediaSource(MtprotoOptions options, TelediskDatabase database, ILogger<TelegramMediaSource> logger)
    {
        _options = options;
        _database = database;
        _logger = logger;
        if (options.CacheDirectory.Length > 0)
        {
            Directory.CreateDirectory(options.CacheDirectory);
        }
    }

    public bool IsConfigured => _options.ApiId > 0 && _options.ApiHash.Length > 0;

    public async Task<MediaStream?> OpenReadAsync(MediaItem item, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured || _options.CacheDirectory.Length == 0)
        {
            return null;
        }

        var cached = CachePath(item);
        if (!File.Exists(cached))
        {
            var downloaded = await DownloadAsync(item, cached, cancellationToken).ConfigureAwait(false);
            if (!downloaded)
            {
                return null;
            }
        }

        var stream = new FileStream(cached, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, useAsync: true);
        return new MediaStream(stream, stream.Length, ContentTypeFor(item.Extension));
    }

    public async Task<ImportSummary> ImportChannelAsync(string username, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MTProto non configurato: servono ApiId e ApiHash.");
        }

        var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var resolved = await client.Contacts_ResolveUsername(username).ConfigureAwait(false);
        if (resolved.UserOrChat is not TL.Channel channel)
        {
            throw new InvalidOperationException($"L'utente {username} non è un canale.");
        }

        var store = new SqliteMediaStore(_database);
        store.UpsertChannel(new DomainChannel
        {
            Id = channel.ID,
            AccessHash = channel.access_hash,
            Title = channel.Title,
            Username = channel.MainUsername ?? username,
            Kind = channel.IsGroup ? ChannelKind.Supergroup : ChannelKind.Channel,
            IsSource = true,
        });

        var peer = (InputPeer)channel;
        var imported = 0;
        long bytes = 0;
        var lastMessageId = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var history = await client.Messages_GetHistory(peer, min_id: lastMessageId > 0 ? lastMessageId + 1 : 0, limit: 100).ConfigureAwait(false);
            if (history.Messages.Length == 0)
            {
                break;
            }

            foreach (var message in history.Messages)
            {
                lastMessageId = Math.Max(lastMessageId, message.ID);
                if (message is not Message mediaMessage || mediaMessage.media is null)
                {
                    continue;
                }

                var name = MediaName(mediaMessage);
                if (name is null)
                {
                    continue;
                }

                var size = mediaMessage.media switch
                {
                    MessageMediaDocument { document: Document document } => document.size,
                    _ => 0,
                };

                store.Upsert(new MediaItem
                {
                    ChannelId = channel.ID,
                    MessageId = mediaMessage.ID,
                    Name = name,
                    Size = size,
                    UpdatedAt = mediaMessage.Date,
                });
                imported++;
                bytes += size;
            }

            if (history.Messages.Length < 100)
            {
                break;
            }
        }

        return new ImportSummary(imported, bytes);
    }

    private async Task<bool> DownloadAsync(MediaItem item, string destination, CancellationToken cancellationToken)
    {
        var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        var store = new SqliteMediaStore(_database);
        var channel = store.ListChannels().FirstOrDefault(candidate => candidate.Id == item.ChannelId);
        if (channel is null || channel.AccessHash is null)
        {
            return false;
        }

        var peer = channel.Kind == ChannelKind.Supergroup
            ? (InputPeer)new InputPeerChannel(channel.Id, channel.AccessHash.Value)
            : new InputPeerChannel(channel.Id, channel.AccessHash.Value);
        var messages = await client.Messages_GetMessages([new InputMessageID { id = item.MessageId }]).ConfigureAwait(false);
        if (messages.Messages.Length == 0 || messages.Messages[0] is not Message message || message.media is null)
        {
            return false;
        }

        var temporary = destination + ".part";
        await using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true))
        {
            Task download;
            switch (message.media)
            {
                case MessageMediaDocument { document: Document document }:
                    download = client.DownloadFileAsync(document, output, thumbSize: null, Progress);
                    break;
                case MessageMediaPhoto { photo: Photo photo }:
                    download = client.DownloadFileAsync(photo, output, photo.LargestPhotoSize, Progress);
                    break;
                default:
                    throw new InvalidOperationException("Il messaggio non contiene media scaricabili.");
            }

            await download.WaitAsync(cancellationToken).ConfigureAwait(false);

            void Progress(long sent, long total) => cancellationToken.ThrowIfCancellationRequested();
        }

        File.Move(temporary, destination, overwrite: true);
        _logger.LogInformation("File Telegram {Name} salvato in cache", item.Name);
        return true;
    }

    private async Task<Client> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_connected && _client is not null)
        {
            return _client;
        }

        _client = new Client(_options.ApiId, _options.ApiHash, _options.SessionPath);
        try
        {
            await _client.ConnectAsync().ConfigureAwait(false);
            var user = await _client.LoginUserIfNeeded().ConfigureAwait(false);
            _logger.LogInformation("MTProto connesso come {User}", user?.username ?? user?.first_name ?? "utente");
            _connected = true;
            return _client;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Connessione MTProto fallita");
            _client.Dispose();
            _client = null;
            throw;
        }
    }

    private string CachePath(MediaItem item)
    {
        var key = item.ChannelId.ToString(CultureInfo.InvariantCulture) + "-" + item.MessageId.ToString(CultureInfo.InvariantCulture) + "-" + item.Name;
        var safe = string.Join('_', key.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.GetFullPath(Path.Combine(_options.CacheDirectory, safe));
    }

    private static string? MediaName(Message message) => message.media switch
    {
        MessageMediaDocument { document: Document { attributes: var attributes } } => attributes.OfType<DocumentAttributeFilename>().FirstOrDefault()?.file_name,
        MessageMediaPhoto => $"foto-{message.ID}.jpg",
        _ => null,
    };

    private static string ContentTypeFor(string extension) => extension switch
    {
        "mkv" => "video/x-matroska",
        "mp4" => "video/mp4",
        "avi" => "video/x-msvideo",
        "flac" => "audio/flac",
        "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4",
        "jpg" => "image/jpeg",
        "jpeg" => "image/jpeg",
        "png" => "image/png",
        "epub" => "application/epub+zip",
        "pdf" => "application/pdf",
        _ => "application/octet-stream",
    };

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            _client.Dispose();
            _client = null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}

public sealed record ImportSummary(int Imported, long Bytes);
