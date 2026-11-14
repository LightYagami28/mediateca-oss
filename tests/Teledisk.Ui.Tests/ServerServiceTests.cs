using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Teledisk.Core.Security;
using Teledisk.Data;
using Teledisk.Server;
using Teledisk.Ui.Services;

namespace Teledisk.Ui.Tests;

public class ServerServiceTests
{
    private static ServerService CreateService(UiOptions options, TelediskDatabase database, string mediaRoot)
    {
        return new ServerService(
            Options.Create(options),
            database,
            new LogBuffer(),
            new LocalMediaFileSource(mediaRoot));
    }
    [Fact]
    public async Task ServerService_espone_la_health_e_chiude_pulito()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var port = FreePort();
            var service = CreateService(new UiOptions { ServerBindAddress = "127.0.0.1", ServerPort = port }, database, directory);

            Assert.False(service.IsRunning);
            await service.StartAsync(TestContext.Current.CancellationToken);

            Assert.True(service.IsRunning);
            Assert.Contains($":{port}", service.Url);

            using var client = new HttpClient();
            var response = await client.GetAsync($"http://127.0.0.1:{port}/health", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"status\":\"ok\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

            await service.StopAsync(TestContext.Current.CancellationToken);
            Assert.False(service.IsRunning);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task Senza_token_il_bind_non_loopback_viene_declassato()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var port = FreePort();
            var service = CreateService(new UiOptions { ServerBindAddress = "0.0.0.0", ServerPort = port }, database, directory);

            await service.StartAsync(TestContext.Current.CancellationToken);

            Assert.Contains("127.0.0.1", service.Url);
            await service.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task Le_risposte_portano_gli_header_di_sicurezza_e_nascondono_kestrel()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var port = FreePort();
            var service = CreateService(new UiOptions { ServerBindAddress = "127.0.0.1", ServerPort = port }, database, directory);
            await service.StartAsync(TestContext.Current.CancellationToken);

            using var client = new HttpClient();
            var response = await client.GetAsync($"http://127.0.0.1:{port}/health", TestContext.Current.CancellationToken);

            Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
            Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
            Assert.False(response.Headers.Contains("Server"));

            var api = await client.GetAsync($"http://127.0.0.1:{port}/api/stats", TestContext.Current.CancellationToken);
            Assert.Equal("no-store", api.Headers.GetValues("Cache-Control").Single());

            await service.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task Dopo_dieci_fallimenti_basic_il_webdav_risponde_429()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var port = FreePort();
            var service = CreateService(new UiOptions
            {
                ServerBindAddress = "127.0.0.1",
                ServerPort = port,
                WebDavEnabled = true,
                WebDavPasswordHash = PasswordHasher.Hash("corretta"),
            }, database, directory);
            await service.StartAsync(TestContext.Current.CancellationToken);

            using var client = new HttpClient();
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var failed = await client.SendAsync(BuildBasicRequest(port), TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
            }

            var throttled = await client.SendAsync(BuildBasicRequest(port), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);

            await service.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static HttpRequestMessage BuildBasicRequest(int port)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/dav/prova");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("teledisk:sbagliata")));
        return request;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "teledisk-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

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
