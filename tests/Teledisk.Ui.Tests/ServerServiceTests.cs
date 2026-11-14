using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Teledisk.Data;
using Teledisk.Ui.Services;

namespace Teledisk.Ui.Tests;

public class ServerServiceTests
{
    [Fact]
    public async Task ServerService_espone_la_health_e_chiude_pulito()
    {
        var directory = Path.Combine(Path.GetTempPath(), "teledisk-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var port = FreePort();
            var service = new ServerService(
                Options.Create(new UiOptions { ServerBindAddress = "127.0.0.1", ServerPort = port }),
                database,
                new LogBuffer());

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
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
