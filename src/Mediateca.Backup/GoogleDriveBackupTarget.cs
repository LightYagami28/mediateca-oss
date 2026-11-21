using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Mediateca.Backup;

public sealed record GoogleDriveOptions
{
    public const string ClientIdVariable = "MEDIATECA_GDRIVE_CLIENT_ID";
    public const string ClientSecretVariable = "MEDIATECA_GDRIVE_CLIENT_SECRET";
    public const string RefreshTokenVariable = "MEDIATECA_GDRIVE_REFRESH_TOKEN";
    public const string FolderIdVariable = "MEDIATECA_GDRIVE_FOLDER_ID";

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public string FolderId { get; init; } = string.Empty;

    public int MaxBackups { get; init; } = 14;

    public Uri TokenEndpoint { get; init; } = new("https://oauth2.googleapis.com/token");

    public Uri ApiBase { get; init; } = new("https://www.googleapis.com/drive/v3/");

    public Uri UploadBase { get; init; } = new("https://www.googleapis.com/upload/drive/v3/");

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) && !string.IsNullOrWhiteSpace(RefreshToken);

    public static GoogleDriveOptions FromEnvironment() => new()
    {
        ClientId = Read(ClientIdVariable),
        ClientSecret = Read(ClientSecretVariable),
        RefreshToken = Read(RefreshTokenVariable),
        FolderId = Read(FolderIdVariable),
    };

    private static string Read(string name) => Environment.GetEnvironmentVariable(name)?.Trim() ?? "";
}

public sealed class GoogleDriveBackupTarget : IBackupTarget
{
    private const string ArchiveMimeType = "application/zip";

    private const string BearerScheme = "Bearer";

    private readonly HttpClient _http;
    private readonly GoogleDriveOptions _options;
    private readonly LocalBackupTarget _local;

    public GoogleDriveBackupTarget(HttpClient httpClient, GoogleDriveOptions options, LocalBackupTarget? local = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _local = local ?? new LocalBackupTarget(options.MaxBackups);
    }

    public string Name => "google-drive";

    public async Task<BackupResult> CreateAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException(
                $"Google Drive non configurato: servono {GoogleDriveOptions.ClientIdVariable}, {GoogleDriveOptions.ClientSecretVariable} e {GoogleDriveOptions.RefreshTokenVariable}.");
        }

        var archive = await _local.CreateAsync(sourceDirectory, destinationDirectory, cancellationToken).ConfigureAwait(false);
        var accessToken = await RequestAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var remoteId = await UploadAsync(archive, accessToken, cancellationToken).ConfigureAwait(false);
        var removed = await ApplyRetentionAsync(accessToken, cancellationToken).ConfigureAwait(false);

        return archive with { RemoteId = remoteId, RemovedOldArchives = archive.RemovedOldArchives + removed };
    }

    private async Task<string> RequestAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["refresh_token"] = _options.RefreshToken,
            ["grant_type"] = "refresh_token",
        });

        using var response = await _http.PostAsync(_options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, "token OAuth", cancellationToken).ConfigureAwait(false);
        var token = await response.Content.ReadFromJsonAsync<TokenDto>(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(token?.AccessToken)
            ? throw new GoogleDriveException("token OAuth", 0, "risposta senza access_token")
            : token.AccessToken;
    }

    private async Task<string> UploadAsync(BackupResult archive, string accessToken, CancellationToken cancellationToken)
    {
        var metadata = new FileMetadataDto
        {
            Name = archive.FileName,
            MimeType = ArchiveMimeType,
            Parents = string.IsNullOrWhiteSpace(_options.FolderId) ? null : [_options.FolderId],
        };

        using var start = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.UploadBase, "files?uploadType=resumable"))
        {
            Content = JsonContent.Create(metadata),
        };
        start.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);
        start.Headers.Add("X-Upload-Content-Type", ArchiveMimeType);
        start.Headers.Add("X-Upload-Content-Length", archive.SizeBytes.ToString(CultureInfo.InvariantCulture));

        Uri session;
        using (var response = await _http.SendAsync(start, cancellationToken).ConfigureAwait(false))
        {
            await EnsureSuccessAsync(response, "avvio upload", cancellationToken).ConfigureAwait(false);
            session = response.Headers.Location ?? throw new GoogleDriveException("avvio upload", (int)response.StatusCode, "header Location assente");
        }

        await using var file = new FileStream(archive.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        using var put = new HttpRequestMessage(HttpMethod.Put, session) { Content = new StreamContent(file) };
        put.Content.Headers.ContentType = new MediaTypeHeaderValue(ArchiveMimeType);
        put.Content.Headers.ContentLength = file.Length;
        put.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);

        using var uploaded = await _http.SendAsync(put, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(uploaded, "upload", cancellationToken).ConfigureAwait(false);
        var created = await uploaded.Content.ReadFromJsonAsync<DriveFileDto>(cancellationToken).ConfigureAwait(false);
        return created?.Id ?? throw new GoogleDriveException("upload", (int)uploaded.StatusCode, "risposta senza id del file");
    }

    private async Task<int> ApplyRetentionAsync(string accessToken, CancellationToken cancellationToken)
    {
        if (_options.MaxBackups < 1)
        {
            return 0;
        }

        var prefix = string.IsNullOrWhiteSpace(_local.ArchivePrefix) ? LocalBackupTarget.DefaultArchivePrefix : _local.ArchivePrefix;
        var query = $"name contains '{Escape(prefix)}-' and mimeType = '{ArchiveMimeType}' and trashed = false";
        if (!string.IsNullOrWhiteSpace(_options.FolderId))
        {
            query = $"'{Escape(_options.FolderId)}' in parents and " + query;
        }

        var listUri = new Uri(_options.ApiBase,
            "files?q=" + Uri.EscapeDataString(query) +
            "&orderBy=" + Uri.EscapeDataString("createdTime desc") +
            "&fields=" + Uri.EscapeDataString("files(id,name,createdTime)") +
            "&pageSize=1000");

        using var list = new HttpRequestMessage(HttpMethod.Get, listUri);
        list.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);
        using var listed = await _http.SendAsync(list, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(listed, "elenco backup", cancellationToken).ConfigureAwait(false);
        var page = await listed.Content.ReadFromJsonAsync<FileListDto>(cancellationToken).ConfigureAwait(false);

        var removed = 0;
        foreach (var stale in (page?.Files ?? []).Where(file => file.Name.StartsWith(prefix + "-", StringComparison.Ordinal)).Skip(_options.MaxBackups))
        {
            using var delete = new HttpRequestMessage(HttpMethod.Delete, new Uri(_options.ApiBase, "files/" + Uri.EscapeDataString(stale.Id)));
            delete.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, accessToken);
            using var deleted = await _http.SendAsync(delete, cancellationToken).ConfigureAwait(false);
            if (deleted.IsSuccessStatusCode)
            {
                removed++;
            }
        }

        return removed;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new GoogleDriveException(operation, (int)response.StatusCode, detail.Length > 300 ? detail[..300] : detail);
    }

    private sealed record TokenDto([property: JsonPropertyName("access_token")] string? AccessToken);

    private sealed record FileMetadataDto
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("mimeType")]
        public required string MimeType { get; init; }

        [JsonPropertyName("parents")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public IReadOnlyList<string>? Parents { get; init; }
    }

    private sealed record DriveFileDto(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record FileListDto([property: JsonPropertyName("files")] IReadOnlyList<DriveFileDto>? Files);
}

public sealed class GoogleDriveException(string operation, int statusCode, string detail)
    : Exception($"Google Drive ha rifiutato {operation} con stato {statusCode}: {detail}")
{
    public string Operation { get; } = operation;

    public int StatusCode { get; } = statusCode;
}
