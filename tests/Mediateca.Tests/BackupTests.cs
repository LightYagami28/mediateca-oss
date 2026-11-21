using System.Net;
using System.Text;
using System.Text.Json;
using Mediateca.Backup;

namespace Mediateca.Tests;

public sealed class BackupTests
{
    [Fact]
    public async Task LocalBackupTarget_crea_l_archivio_ed_esclude_i_backup()
    {
        var directory = NewTempDirectory();
        try
        {
            var source = Path.Combine(directory, "dati");
            Directory.CreateDirectory(Path.Combine(source, LocalBackupTarget.BackupsFolderName));
            File.WriteAllText(Path.Combine(source, "mediateca.db"), "catalogo");
            File.WriteAllText(Path.Combine(source, LocalBackupTarget.BackupsFolderName, "vecchio.zip"), "x");

            var result = await new LocalBackupTarget(2).CreateAsync(source, Path.Combine(source, LocalBackupTarget.BackupsFolderName));

            using var archive = System.IO.Compression.ZipFile.OpenRead(result.ArchivePath);
            Assert.Equal(["mediateca.db"], archive.Entries.Select(static entry => entry.FullName));
            Assert.Equal(64, result.Sha256.Length);
            Assert.Null(result.RemoteId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GoogleDrive_carica_l_archivio_e_applica_la_retention()
    {
        var directory = NewTempDirectory();
        try
        {
            var source = Path.Combine(directory, "dati");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "mediateca.db"), "catalogo");
            var handler = new FakeDriveHandler(existingArchives: 4);
            using var http = new HttpClient(handler);
            var options = new GoogleDriveOptions
            {
                ClientId = "client",
                ClientSecret = "secret",
                RefreshToken = "refresh",
                FolderId = "cartella",
                MaxBackups = 2,
            };

            var result = await new GoogleDriveBackupTarget(http, options).CreateAsync(source, Path.Combine(directory, "backups"));

            Assert.Equal("nuovo-id", result.RemoteId);
            Assert.Equal(2, result.RemovedOldArchives);
            Assert.Equal("grant_type=refresh_token", handler.TokenBody!.Split('&').Single(static part => part.StartsWith("grant_type", StringComparison.Ordinal)));

            var metadata = JsonElement.Parse(handler.StartBody!);
            Assert.Equal(result.FileName, metadata.GetProperty("name").GetString());
            Assert.Equal("cartella", metadata.GetProperty("parents")[0].GetString());
            Assert.Equal(new FileInfo(result.ArchivePath).Length, handler.UploadedBytes);
            Assert.Contains("'cartella' in parents", handler.ListQuery);
            Assert.Equal(["vecchio-1", "vecchio-2"], handler.Deleted);
            Assert.All(handler.AuthorizedRequests, static header => Assert.Equal("Bearer accesso", header));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task GoogleDrive_senza_credenziali_non_tocca_la_rete()
    {
        var handler = new FakeDriveHandler(existingArchives: 0);
        using var http = new HttpClient(handler);

        var target = new GoogleDriveBackupTarget(http, new GoogleDriveOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => target.CreateAsync(".", "."));
        Assert.Empty(handler.AuthorizedRequests);
        Assert.Null(handler.TokenBody);
    }

    [Fact]
    public async Task GoogleDrive_segnala_il_rifiuto_del_token()
    {
        var directory = NewTempDirectory();
        try
        {
            var handler = new FakeDriveHandler(existingArchives: 0) { RejectToken = true };
            using var http = new HttpClient(handler);
            var options = new GoogleDriveOptions { ClientId = "c", ClientSecret = "s", RefreshToken = "r" };

            var error = await Assert.ThrowsAsync<GoogleDriveException>(() => new GoogleDriveBackupTarget(http, options).CreateAsync(directory, Path.Combine(directory, "backups")));

            Assert.Equal(400, error.StatusCode);
            Assert.Equal("token OAuth", error.Operation);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeDriveHandler(int existingArchives) : HttpMessageHandler
    {
        public bool RejectToken { get; init; }

        public string? TokenBody { get; private set; }

        public string? StartBody { get; private set; }

        public long UploadedBytes { get; private set; }

        public string ListQuery { get; private set; } = "";

        public List<string> Deleted { get; } = [];

        public List<string> AuthorizedRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request != null)
            {
                var uri = request.RequestUri!;
                if (request.Headers.Authorization is { } authorization)
                {
                    AuthorizedRequests.Add(authorization.ToString());
                }

                if (uri.Host == "oauth2.googleapis.com")
                {
                    TokenBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                    return RejectToken
                        ? Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")
                        : Json(HttpStatusCode.OK, """{"access_token":"accesso","expires_in":3599,"token_type":"Bearer"}""");
                }

                if (request.Method == HttpMethod.Post && uri.AbsolutePath == "/upload/drive/v3/files")
                {
                    StartBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.Location = new Uri("https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable&upload_id=sessione");
                    return response;
                }

                if (request.Method == HttpMethod.Put)
                {
                    UploadedBytes = (await request.Content!.ReadAsByteArrayAsync(cancellationToken)).LongLength;
                    return Json(HttpStatusCode.OK, """{"id":"nuovo-id","name":"archivio.zip"}""");
                }

                if (request.Method == HttpMethod.Get && uri.AbsolutePath == "/drive/v3/files")
                {
                    ListQuery = Uri.UnescapeDataString(uri.Query);
                    var files = new StringBuilder("""{"files":[{"id":"nuovo-id","name":"mediateca-backup-2026-11-14_10-00-00.zip"}""");
                    for (var index = 0; index < existingArchives - 1; index++)
                    {
                        files.Append($$""",{"id":"vecchio-{{index}}","name":"mediateca-backup-2026-11-0{{index + 1}}_10-00-00.zip"}""");
                    }

                    files.Append("]}");
                    return Json(HttpStatusCode.OK, files.ToString());
                }

                if (request.Method == HttpMethod.Delete)
                {
                    Deleted.Add(uri.Segments[^1]);
                    return new HttpResponseMessage(HttpStatusCode.NoContent);
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            else
            {
                throw new ArgumentNullException();
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
