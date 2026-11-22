using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;

namespace Mediateca.Tests;

/// <summary>Test del server HTTP locale: health, protezione delle API e area WebDAV.</summary>
public sealed class HttpServerTests
{
    private const string TestToken = "token-di-prova";

    private const string SetCookieHeader = "Set-Cookie";

    private const string NonLoopbackBind = "0.0.0.0";

    private const int FilmSize = 100;

    private const int SerieSize = 200;

    private static readonly string[] invalidTokens = ["corto", "token-di-prova-X"];

    /// <summary><c>/health</c> risponde 200 con il JSON di stato e la versione dello schema.</summary>
    [Fact]
    public async Task Health_RispondeOkConJson()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, WebDavEnabled = false },
                database);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var response = await http.GetAsync($"{baseUrl}/health");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);

                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("\"status\":\"ok\"", body);
                Assert.Contains($"\"schema\":{MediatecaDatabase.SchemaVersion}", body);
                Assert.DoesNotContain("tier", body);
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

    /// <summary>Gli header di sicurezza HTTPS sono presenti anche sulla risposta pubblica.</summary>
    [Fact]
    public async Task SecurityHeaders_PresentiSuHealth()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, WebDavEnabled = false },
                database);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var response = await http.GetAsync($"{baseUrl}/health");

                Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
                Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
                Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").First());
                Assert.Equal("max-age=31536000; includeSubDomains; preload",
                    response.Headers.GetValues("Strict-Transport-Security").First());

                var csp = response.Headers.GetValues("Content-Security-Policy").First();
                Assert.Contains("upgrade-insecure-requests", csp);
                Assert.DoesNotContain("http://", csp);
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

            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, ApiToken = "token-di-prova", WebDavEnabled = false },
                database);

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
            var app = MediatecaServer.Build(
                new ServerOptions
                {
                    UseHttps = false,
                    WebDavEnabled = true,
                    WebDavRequireAuth = true,
                    WebDavUser = "utente",
                    WebDavPassword = "parola",
                },
                database);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var anonymous = await http.SendAsync(new HttpRequestMessage(HttpMethod.Options, $"{baseUrl}/dav/"));
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
                Assert.Contains("Basic realm=\"Mediateca\"", anonymous.Headers.WwwAuthenticate.ToString());

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

    /// <summary><see cref="MediatecaServer.RunAsync"/> serve <c>/health</c> fino alla cancellazione.</summary>
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
            var run = MediatecaServer.RunAsync(
                new ServerOptions { UseHttps = false, BindAddress = "127.0.0.1", Port = port, WebDavEnabled = false },
                database,
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

    /// <summary>Lo streaming serve i byte con supporto Range: 200 pieno, 206 parziale, 416 fuori misura.</summary>
    [Fact]
    public async Task Stream_ServeByteConSupportoRange()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var store = new SqliteMediaStore(database);
            store.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var bytes = Enumerable.Range(0, 1024).Select(static index => (byte)(index % 256)).ToArray();
            File.WriteAllBytes(Path.Combine(directory, "media.bin"), bytes);
            var itemId = store.Upsert(new MediaItem
            {
                ChannelId = 1,
                MessageId = 7,
                Name = "media.bin",
                Size = bytes.Length,
            });
            var source = new LocalMediaFileSource(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, WebDavEnabled = true },
                database,
                fileSource: source);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var full = await http.GetAsync($"{baseUrl}/api/items/{itemId}/stream");
                Assert.Equal(HttpStatusCode.OK, full.StatusCode);
                Assert.Equal(bytes, await full.Content.ReadAsByteArrayAsync());
                Assert.Equal("bytes", full.Headers.GetValues("Accept-Ranges").Single());

                using var partialRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/items/{itemId}/stream");
                partialRequest.Headers.Range = new RangeHeaderValue(100, 199);
                using var partial = await http.SendAsync(partialRequest);
                Assert.Equal(HttpStatusCode.PartialContent, partial.StatusCode);
                Assert.Equal("bytes 100-199/1024", partial.Content.Headers.ContentRange!.ToString());
                Assert.Equal(bytes[100..200], await partial.Content.ReadAsByteArrayAsync());

                using var unsatisfiableRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/items/{itemId}/stream");
                unsatisfiableRequest.Headers.Range = new RangeHeaderValue(5000, 6000);
                using var unsatisfiable = await http.SendAsync(unsatisfiableRequest);
                Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, unsatisfiable.StatusCode);
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

    /// <summary>Un nome che punta fuori dalla radice non viene mai aperto.</summary>
    [Fact]
    public async Task Stream_NonEsceDallaRadiceDellaSorgente()
    {
        var directory = NewTempDirectory();
        try
        {
            var outside = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N") + ".txt");
            Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
            File.WriteAllText(outside, "segreto");

            var database = Migrated(directory);
            var store = new SqliteMediaStore(database);
            store.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var itemId = store.Upsert(new MediaItem
            {
                ChannelId = 1,
                MessageId = 8,
                Name = "..\\..\\" + Path.GetFileName(outside),
                Size = 7,
            });
            var source = new LocalMediaFileSource(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, WebDavEnabled = true },
                database,
                fileSource: source);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var response = await http.GetAsync($"{baseUrl}/api/items/{itemId}/stream");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            finally
            {
                await StopAsync(app);
                File.Delete(outside);
            }
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>La MiniApp è una pagina HTML che interroga le API del server.</summary>
    [Fact]
    public async Task Miniapp_ServeLaPaginaHtml()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { UseHttps = false, WebDavEnabled = false },
                database);

            try
            {
                var baseUrl = await StartAsync(app);
                using var http = new HttpClient();

                using var response = await http.GetAsync($"{baseUrl}/miniapp");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var body = await response.Content.ReadAsStringAsync();
                Assert.Contains("Mediateca", body);

                using (var css = await http.GetAsync($"{baseUrl}/miniapp.css"))
                {
                    Assert.Equal(HttpStatusCode.OK, css.StatusCode);
                }

                using (var script = await http.GetAsync($"{baseUrl}/miniapp.js"))
                {
                    Assert.Equal(HttpStatusCode.OK, script.StatusCode);
                    var scriptText = await script.Content.ReadAsStringAsync();
                    Assert.Contains("api/stats", scriptText);
                }
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

    /// <summary>Il server parla HTTPS con un certificato autofirmato generato all'avvio.</summary>
    [Fact]
    public async Task Https_ServeConCertificatoAutofirmato()
    {
        var directory = NewTempDirectory();
        var previousUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        var port = FreeLoopbackPort();
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", null);

        try
        {
            var database = Migrated(directory);
            var app = MediatecaServer.Build(
                new ServerOptions { BindAddress = "127.0.0.1", Port = port, WebDavEnabled = false },
                database);

            try
            {
                await app.StartAsync();
                Assert.Contains($"https://127.0.0.1:{port}", app.Urls);

                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                };
                using var http = new HttpClient(handler);
                using var response = await http.GetAsync($"https://127.0.0.1:{port}/health");

                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Contains("\"status\":\"ok\"", await response.Content.ReadAsStringAsync());
            }
            finally
            {
                await StopAsync(app);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", previousUrls);
            Cleanup(directory);
        }
    }

    /// <summary>Il POST su <c>/api/session</c> imposta un cookie Secure e il cookie autorizza le API.</summary>
    [Fact]
    public async Task ApiSession_ImpostaCookieSecureEAutorizza() =>
        await WithServerAsync(TokenOptions(), async (baseUrl, http) =>
        {
            using var session = await PostSessionAsync(http, baseUrl, TestToken);
            Assert.Equal(HttpStatusCode.NoContent, session.StatusCode);
            var cookie = Assert.Single(session.Headers.GetValues(SetCookieHeader));
            AssertCookieFlags(cookie);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/stats");
            request.Headers.Add("Cookie", cookie.Split(';')[0]);
            using var authorized = await http.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
        });

    /// <summary>Un token errato non genera il cookie, sia per lunghezza diversa sia a parità di lunghezza.</summary>
    [Fact]
    public async Task ApiSession_RifiutaTokenErrato() =>
        await WithServerAsync(TokenOptions(), async (baseUrl, http) =>
        {
            foreach (var supplied in invalidTokens)
            {
                using var response = await PostSessionAsync(http, baseUrl, supplied);
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                Assert.False(response.Headers.Contains(SetCookieHeader));
            }
        });

    /// <summary>Senza token configurato la sessione non è necessaria e non imposta cookie.</summary>
    [Fact]
    public async Task ApiSession_SenzaTokenConfiguratoNonImpostaCookie() =>
        await WithServerAsync(
            new ServerOptions { UseHttps = false, WebDavEnabled = false },
            async (baseUrl, http) =>
            {
                using var session = await PostSessionAsync(http, baseUrl, "qualsiasi");
                Assert.Equal(HttpStatusCode.NoContent, session.StatusCode);
                Assert.False(session.Headers.Contains(SetCookieHeader));
                using var stats = await http.GetAsync($"{baseUrl}/api/stats");
                Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
            });

    /// <summary>Un corpo JSON malformato produce 400 e non interrompe il server.</summary>
    [Fact]
    public async Task ApiSession_JsonMalformatoRestituisceBadRequest() =>
        await WithServerAsync(TokenOptions(), async (baseUrl, http) =>
        {
            using var response = await http.PostAsync(
                $"{baseUrl}/api/session",
                new StringContent("{non-json", Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.False(response.Headers.Contains(SetCookieHeader));
        });

    /// <summary><c>/api/items</c> applica filtri e limite, e richiede il token quando configurato.</summary>
    [Fact]
    public async Task ApiItems_FiltraERestituisceCatalogo() =>
        await WithServerAsync(TokenOptions(), async (baseUrl, http) =>
        {
            using var anonymous = await http.GetAsync($"{baseUrl}/api/items");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/items?q=Film&kind=Movie&limit=1");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestToken);
            using var authorized = await http.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
            var body = await authorized.Content.ReadAsStringAsync();
            Assert.Contains("Film.mkv", body);
            Assert.DoesNotContain("Serie.mkv", body);
        }, SeedCatalog);

    /// <summary>Un bind loopback non è soggetto ai vincoli di rete.</summary>
    [Fact]
    public void ApplyNonLoopbackGuards_LoopbackRestituisceLeStesseOpzioni()
    {
        var options = new ServerOptions { BindAddress = "localhost", WebDavEnabled = true };
        Assert.Same(options, MediatecaServer.ApplyNonLoopbackGuards(options));
    }

    /// <summary>Senza token API l'ascolto resta su loopback e senza credenziali il WebDAV si disattiva.</summary>
    [Fact]
    public void ApplyNonLoopbackGuards_SenzaTokenLimitaALoopbackEDisattivaWebDav()
    {
        var result = GuardResult(webDavEnabled: true);

        Assert.Equal("127.0.0.1", result.BindAddress);
        Assert.False(result.WebDavEnabled);
        Assert.True(result.WebDavRequireAuth);
    }

    /// <summary>Con token API il bind resta sulla rete, ma il WebDAV richiede credenziali.</summary>
    [Fact]
    public void ApplyNonLoopbackGuards_ConTokenMantieneBindEDisattivaWebDavSenzaCredenziali()
    {
        var result = GuardResult(token: TestToken, webDavEnabled: true);

        Assert.Equal(NonLoopbackBind, result.BindAddress);
        Assert.False(result.WebDavEnabled);
        Assert.True(result.WebDavRequireAuth);
    }

    /// <summary>L'hash WebDAV abilita l'area sulla rete e impone l'autenticazione.</summary>
    [Fact]
    public void ApplyNonLoopbackGuards_ConHashWebDavAbilitaArea()
    {
        var result = GuardResult(token: TestToken, webDavEnabled: true, passwordHash: "hash");

        Assert.Equal(NonLoopbackBind, result.BindAddress);
        Assert.True(result.WebDavEnabled);
        Assert.True(result.WebDavRequireAuth);
    }

    /// <summary>Anche la password in chiaro conta come credenziale WebDAV.</summary>
    [Fact]
    public void ApplyNonLoopbackGuards_ConPasswordWebDavAbilitaArea()
    {
        var result = GuardResult(token: TestToken, webDavEnabled: true, password: "parola");

        Assert.True(result.WebDavEnabled);
        Assert.True(result.WebDavRequireAuth);
    }

    /// <summary>Gli avvisi di guardia vengono scritti solo quando i vincoli sono stati applicati.</summary>
    [Fact]
    public async Task WriteGuardWarnings_SegnalaSoloIVincoliApplicati()
    {
        using var writer = new StringWriter();
        var guarded = GuardResult(webDavEnabled: true);

        await MediatecaServer.WriteGuardWarningsAsync(writer, NonLoopbackBind, guarded, webDavRequested: true);

        var output = writer.ToString();
        Assert.Contains($"bind {NonLoopbackBind} senza token API", output);
        Assert.Contains("senza credenziali WebDAV", output);
    }

    /// <summary>Senza vincoli non viene scritto alcun avviso.</summary>
    [Fact]
    public async Task WriteGuardWarnings_SenzaVincoliNonScriveNulla()
    {
        using var writer = new StringWriter();
        var options = new ServerOptions { BindAddress = "localhost", WebDavEnabled = true };

        await MediatecaServer.WriteGuardWarningsAsync(writer, "localhost", options, webDavRequested: true);

        Assert.Equal(string.Empty, writer.ToString());
    }

    private static ServerOptions TokenOptions() => new()
    {
        UseHttps = false,
        ApiToken = TestToken,
        WebDavEnabled = false,
    };

    private static ServerOptions GuardResult(
        string? token = null,
        bool webDavEnabled = false,
        string? passwordHash = null,
        string? password = null) =>
        MediatecaServer.ApplyNonLoopbackGuards(new ServerOptions
        {
            BindAddress = NonLoopbackBind,
            ApiToken = token ?? string.Empty,
            WebDavEnabled = webDavEnabled,
            WebDavPasswordHash = passwordHash ?? string.Empty,
            WebDavPassword = password ?? string.Empty,
        });

    private static Task<HttpResponseMessage> PostSessionAsync(HttpClient http, string baseUrl, string token) =>
        http.PostAsync(
            $"{baseUrl}/api/session",
            new StringContent($$"""{"token":"{{token}}"}""", Encoding.UTF8, "application/json"));

    private static void AssertCookieFlags(string cookie)
    {
        var lowercase = cookie.ToLowerInvariant();
        Assert.Contains("mediateca_token=" + TestToken, lowercase);
        Assert.Contains("secure", lowercase);
        Assert.Contains("httponly", lowercase);
        Assert.Contains("samesite=strict", lowercase);
        Assert.Contains("path=/api", lowercase);
    }

    private static void SeedCatalog(SqliteMediaStore store)
    {
        store.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
        store.Upsert(new MediaItem { ChannelId = 1, MessageId = 1, Name = "Film.mkv", Kind = MediaKind.Movie, Size = FilmSize });
        store.Upsert(new MediaItem { ChannelId = 1, MessageId = 2, Name = "Serie.mkv", Kind = MediaKind.Series, Size = SerieSize });
    }

    private static async Task WithServerAsync(
        ServerOptions options,
        Func<string, HttpClient, Task> use,
        Action<SqliteMediaStore>? seed = null)
    {
        var directory = NewTempDirectory();
        try
        {
            var database = Migrated(directory);
            seed?.Invoke(new SqliteMediaStore(database));
            await WithAppAsync(MediatecaServer.Build(options, database), use);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static async Task WithAppAsync(WebApplication app, Func<string, HttpClient, Task> use)
    {
        try
        {
            using var http = new HttpClient();
            await use(await StartAsync(app), http);
        }
        finally
        {
            await StopAsync(app);
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

    private static MediatecaDatabase Migrated(string directory)
    {
        var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
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
        var path = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
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
