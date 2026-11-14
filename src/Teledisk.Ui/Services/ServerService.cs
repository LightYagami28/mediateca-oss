using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Teledisk.Data;
using Teledisk.Licensing;
using Teledisk.Server;

namespace Teledisk.Ui.Services;

public sealed class ServerService : IHostedService, IAsyncDisposable
{
    private readonly ServerOptions _options;
    private readonly TelediskDatabase _database;
    private readonly LicenseAuthority _license;
    private readonly LogBuffer _logs;
    private WebApplication? _app;

    public ServerService(IOptions<UiOptions> options, TelediskDatabase database, LogBuffer logs)
    {
        var ui = options.Value;
        var bindAddress = ui.ServerBindAddress;
        var webDavEnabled = ui.WebDavEnabled;
        var isLoopback = IPAddress.TryParse(bindAddress, out var parsed) && IPAddress.IsLoopback(parsed);
        var logger = logs.CreateLogger("server");

        if (!isLoopback && string.IsNullOrEmpty(ui.ServerApiToken))
        {
            logger.LogWarning("Bind {Bind} senza token API: il server resta in ascolto solo su 127.0.0.1.", bindAddress);
            bindAddress = "127.0.0.1";
        }

        if (!isLoopback && webDavEnabled && string.IsNullOrEmpty(ui.WebDavPasswordHash))
        {
            logger.LogWarning("WebDAV senza password e bind {Bind}: l'area WebDAV resta disattivata.", bindAddress);
            webDavEnabled = false;
        }

        _options = new ServerOptions
        {
            BindAddress = bindAddress,
            Port = ui.ServerPort,
            ApiToken = ui.ServerApiToken,
            WebDavEnabled = webDavEnabled,
            WebDavPasswordHash = ui.WebDavPasswordHash,
        };
        _database = database;
        _license = LicenseAuthority.CreateDefault();
        _logs = logs;
    }

    public event EventHandler? StatusChanged;

    public bool IsRunning => _app is not null;

    public string Url { get; private set; } = string.Empty;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _app = TelediskServer.Build(_options, _database, _license);
        await _app.StartAsync(cancellationToken).ConfigureAwait(false);
        Url = _app.Urls.FirstOrDefault() ?? $"http://{_options.BindAddress}:{_options.Port}";
        _logs.CreateLogger("server").LogInformation("Server HTTP attivo su {Url}", Url);
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_app is null)
        {
            return;
        }

        var app = _app;
        _app = null;
        await app.StopAsync(cancellationToken).ConfigureAwait(false);
        await app.DisposeAsync().ConfigureAwait(false);
        Url = string.Empty;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None).ConfigureAwait(false);
}
