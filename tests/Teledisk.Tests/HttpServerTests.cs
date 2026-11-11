using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Teledisk.Core.Domain;
using Teledisk.Data;
using Teledisk.Licensing;
using Teledisk.Server;

namespace Teledisk.Tests;

/// <summary>Test del server HTTP locale: health, protezione delle API e area WebDAV.</summary>
public sealed class HttpServerTests
{
    /// <summary><c>/health</c> risponde 200 con il JSON di stato e la versione dello schema.</summary>
    [Fact]
    public async Task Health_RispondeOkConJson()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var app = TelediskServer.Build(
                new ServerOptions { WebDavEnabled = false },
                database,
                LicenseAuthority.CreateDefault());

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var response = await http.GetAsync($"{baseUrl}/health");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);

                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("\"status\":\"ok\"", body);
                Assert.Contains("\"schema\":1", body);
                Assert.Contains("\"tier\":", body);
            }
            finally
            {
                await StopAsync(app);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Il token protegge le API dati ma non <c>/health</c>.</summary>
    [Fact]
    public async Task ApiToken_ProteggeLeApiDati()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var store = new SqliteMediaStore(database);
            store.UpsertChannel(new Channel { Id = 1, Title = "Sorgente", IsSource = true });
            store.Upsert(new MediaItem { ChannelId = 1, MessageId = 1, Name = "Film.mkv", Kind = MediaKind.Movie, Size = 1200 });

            var app = TelediskServer.Build(
                new ServerOptions { ApiToken = "token-di-prova", WebDavEnabled = false },
                database,
                LicenseAuthority.CreateDefault());

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var health = await http.GetAsync($"{baseUrl}/health");
                Assert.Equal(HttpStatusCode.OK, health.StatusCode);

                using var anonymous = await http.GetAsync($"{baseUrl}/api/stats");
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/stats");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-di-prova");
                using var authorized = await http.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
                var body = await authorized.Content.ReadAsStringAsync();
                Assert.Contains("\"items\":1", body);
                Assert.Contains("\"channels\":1", body);
            }
            finally
            {
                await StopAsync(app);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>L'area WebDAV chiede le credenziali Basic quando è configurata la password.</summary>
    [Fact]
    public async Task WebDav_RichiedeAutenticazioneBasic()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var app = TelediskServer.Build(
                new ServerOptions
                {
                    WebDavEnabled = true,
                    WebDavRequireAuth = true,
                    WebDavUser = "utente",
                    WebDavPassword = "parola",
                },
                database,
                LicenseAuthority.CreateDefault());

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var anonymous = await http.SendAsync(new HttpRequestMessage(HttpMethod.Options, $"{baseUrl}/dav/"));
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
                Assert.Contains("Basic realm=\"Teledisk\"", anonymous.Headers.WwwAuthenticate.ToString());

                using var request = new HttpRequestMessage(HttpMethod.Options, $"{baseUrl}/dav/");
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("utente:parola")));
                using var authorized = await http.SendAsync(request);

                Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
                Assert.Equal("1", authorized.Headers.GetValues("DAV").Single());
            }
            finally
            {
                await StopAsync(app);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary><see cref="TelediskServer.RunAsync"/> serve <c>/health</c> fino alla cancellazione.</summary>
    [Fact]
    public async Task RunAsync_ServeHealthFinoAllaCancellazione()
    {
        var directory = NewTempDirectory();
        var previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        var port = FreeLoopbackPort();
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://127.0.0.1:{port}");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            var database = Migrated(directory);
            var run = TelediskServer.RunAsync(
                new ServerOptions { BindAddress = "127.0.0.1", Port = port, WebDavEnabled = false },
                database,
                LicenseAuthority.CreateDefault(),
                cancellation.Token);

            using var http = new HttpClient();
            string? body = null;
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (body is null && DateTime.UtcNow < deadline)
            {
                try
                {
                    using var response = await http.GetAsync($"http://127.0.0.1:{port}/health");
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        body = await response.Content.ReadAsStringAsync();
                        break;
                    }
                }
                catch (HttpRequestException)
                {
                    // Il listener non è ancora pronto: si riprova.
                }

                await Task.Delay(50);
            }

            Assert.NotNull(body);
            Assert.Contains("\"status\":\"ok\"", body);

            cancellation.Cancel();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (OperationCanceledException)
            {
                // Atteso quando il token annulla l'attesa della chiusura.
            }

            Assert.True(run.IsCompleted);
        }
        finally
        {
            cancellation.Cancel();
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousUrls);
            Cleanup(directory);
        }
    }

    /// <summary>Avvia l'app su una porta loopback dinamica e restituisce la base URL reale.</summary>
    private static async Task<string> StartAsync(WebApplication app)
    {
        var previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0");
        try
        {
            await app.StartAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousUrls);
        }

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var url = app.Urls.FirstOrDefault(static candidate => !candidate.EndsWith(":0", StringComparison.Ordinal));
            if (url is not null)
            {
                return url.TrimEnd('/');
            }

            await Task.Delay(20);
        }

        return app.Urls.First().TrimEnd('/');
    }

    private static async Task StopAsync(WebApplication app)
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }

    private static TelediskDatabase Migrated(string directory)
    {
        var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
        database.Migrate();
        return database;
    }

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "teledisk-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }

                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }
}
