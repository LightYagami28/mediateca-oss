using System.Globalization;
using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Server;
using Microsoft.Extensions.Logging;
using TL;
using WTelegram;
using DomainChannel = Mediateca.Core.Domain.Channel;

namespace Mediateca.Mtproto;

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
    private readonly MediatecaDatabase _database;
    private readonly ILogger<TelegramMediaSource> _logger;
    private Client? _client;
    private bool _connected;

    public TelegramMediaSource(MtprotoOptions options, MediatecaDatabase database, ILogger<TelegramMediaSource> logger)
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

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var resolved = await client.Contacts_ResolveUsername(username).ConfigureAwait(false);
        if (resolved.UserOrChat is not TL.Channel channel)
        {
            throw new InvalidOperationException($"L'utente {username} non è un canale.");
        }

        var store = new SqliteMediaStore(_database);
        store.UpsertChannel(ToDomainChannel(channel, username));

        var (imported, bytes) = await ImportHistoryAsync(
            client, (InputPeer)channel, store, channel.ID, cancellationToken).ConfigureAwait(false);
        return new ImportSummary(imported, bytes);
    }

    private static DomainChannel ToDomainChannel(TL.Channel channel, string username) => new()
    {
        Id = channel.ID,
        AccessHash = channel.access_hash,
        Title = channel.Title,
        Username = channel.MainUsername ?? username,
        Kind = channel.IsGroup ? ChannelKind.Supergroup : ChannelKind.Channel,
        IsSource = true,
    };

    private static async Task<(int Imported, long Bytes)> ImportHistoryAsync(
        Client client,
        InputPeer peer,
        SqliteMediaStore store,
        long channelId,
        CancellationToken cancellationToken)
    {
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
                var (count, size) = CatalogMessage(store, channelId, message);
                imported += count;
                bytes += size;
            }

            if (history.Messages.Length < 100)
            {
                break;
            }
        }

        return (imported, bytes);
    }

    private static (int Imported, long Bytes) CatalogMessage(SqliteMediaStore store, long channelId, MessageBase message)
    {
        if (message is not Message mediaMessage || mediaMessage.media is null)
        {
            return (0, 0);
        }

        var name = MediaName(mediaMessage);
        if (name is null)
        {
            return (0, 0);
        }

        var size = mediaMessage.media switch
        {
            MessageMediaDocument { document: Document document } => document.size,
            _ => 0,
        };

        store.Upsert(Catalogued(channelId, mediaMessage.ID, name, size, mediaMessage.Date));
        return (1, size);
    }

    public async Task<MediaItem> UploadAsync(long channelId, string filePath, string? caption = null, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MTProto non configurato: servono ApiId e ApiHash.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var file = new FileInfo(filePath);
        if (!file.Exists)
        {
            throw new FileNotFoundException("File da caricare inesistente.", file.FullName);
        }

        var channel = FindChannel(channelId)
            ?? throw new InvalidOperationException($"Canale {channelId.ToString(CultureInfo.InvariantCulture)} sconosciuto o senza access hash: importalo prima.");

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var peer = new InputPeerChannel(channel.Id, channel.AccessHash!.Value);
        var uploaded = await client.UploadFileAsync(file.FullName, Progress).WaitAsync(cancellationToken).ConfigureAwait(false);
        var extension = file.Extension.TrimStart('.').ToLowerInvariant();
        var message = await client.SendMediaAsync(peer, caption ?? string.Empty, uploaded, ContentTypeFor(extension)).WaitAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Telegram non ha confermato il messaggio caricato.");

        var item = Catalogued(channel.Id, message.ID, file.Name, file.Length, message.Date);
        new SqliteMediaStore(_database).Upsert(item);
        _logger.LogInformation("File {Name} caricato nel canale {Channel}", file.Name, channel.Title);
        return item;

        void Progress(long sent, long total) => cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<int> ImportAddressBookAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("MTProto non configurato: servono ApiId e ApiHash.");
        }

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var entries = new List<AddressBookEntry>();

        var contacts = await client.Contacts_GetContacts().WaitAsync(cancellationToken).ConfigureAwait(false);
        foreach (var user in contacts.users.Values)
        {
            entries.Add(new AddressBookEntry
            {
                PeerId = user.ID,
                AccessHash = user.access_hash,
                Kind = user.IsBot ? AddressBookKind.Bot : AddressBookKind.User,
                Title = DisplayName(user),
                Username = user.MainUsername,
                Phone = string.IsNullOrWhiteSpace(user.phone) ? null : user.phone,
            });
        }

        var chats = await client.Messages_GetAllChats().WaitAsync(cancellationToken).ConfigureAwait(false);
        foreach (var chat in chats.chats.Values.Where(static chat => chat.IsActive))
        {
            entries.Add(chat switch
            {
                TL.Channel channel => new AddressBookEntry
                {
                    PeerId = channel.ID,
                    AccessHash = channel.access_hash,
                    Kind = channel.IsChannel ? AddressBookKind.Channel : AddressBookKind.Group,
                    Title = channel.Title,
                    Username = channel.MainUsername,
                },
                _ => new AddressBookEntry { PeerId = chat.ID, Kind = AddressBookKind.Group, Title = chat.Title },
            });
        }

        return new AddressBookStore(_database).Upsert(entries);
    }

    private static string DisplayName(User user)
    {
        var name = string.Join(' ', new[] { user.first_name, user.last_name }.Where(static part => !string.IsNullOrWhiteSpace(part)));
        return name.Length > 0 ? name : user.MainUsername ?? user.ID.ToString(CultureInfo.InvariantCulture);
    }

    private DomainChannel? FindChannel(long channelId) =>
        new SqliteMediaStore(_database).ListChannels().FirstOrDefault(candidate => candidate.Id == channelId && candidate.AccessHash is not null);

    private static MediaItem Catalogued(long channelId, int messageId, string name, long size, DateTime date)
    {
        var parsed = MediaNameParser.Parse(name);
        return new MediaItem
        {
            ChannelId = channelId,
            MessageId = messageId,
            Name = name,
            Kind = parsed.Kind,
            Title = parsed.Title,
            Year = parsed.Year,
            Season = parsed.Season,
            Episode = parsed.Episode,
            Size = size,
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)),
        };
    }

    private async Task<bool> DownloadAsync(MediaItem item, string destination, CancellationToken cancellationToken)
    {
        var channel = FindChannel(item.ChannelId);
        if (channel is null)
        {
            return false;
        }

        var client = await EnsureConnectedAsync().ConfigureAwait(false);
        var input = new InputChannel(channel.Id, channel.AccessHash!.Value);
        var messages = await client.Channels_GetMessages(input, [new InputMessageID { id = item.MessageId }]).ConfigureAwait(false);
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

    private async Task<Client> EnsureConnectedAsync()
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
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
            throw new InvalidOperationException("Connessione MTProto fallita.", exception);
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
            await _client.DisposeAsync().ConfigureAwait(false);
            _client = null;
        }
    }
}

public sealed record ImportSummary(int Imported, long Bytes);
