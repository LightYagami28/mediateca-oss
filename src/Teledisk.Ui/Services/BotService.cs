using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Teledisk.Data;
using Teledisk.Telegram;

namespace Teledisk.Ui.Services;

public sealed class BotService : IHostedService
{
    private readonly UiOptions _options;
    private readonly TelediskDatabase _database;
    private readonly LogBuffer _logs;
    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    public BotService(IOptions<UiOptions> options, TelediskDatabase database, LogBuffer logs)
    {
        _options = options.Value;
        _database = database;
        _logs = logs;
    }

    public bool IsConfigured => _options.BotToken.Length > 0;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            _logs.CreateLogger("bot").LogInformation("Bot Telegram non configurato: token assente.");
            return Task.CompletedTask;
        }

        var client = new TelegramBotClient(
            new HttpClient(),
            new TelegramOptions { BotToken = _options.BotToken });
        var dispatcher = new BotCommandDispatcher(client, new BotCatalogAdapter(_database), new BotStatisticsAdapter(_database));
        var polling = new BotPollingService(client, _logs.CreateLogger("bot"));
        _cancellation = new CancellationTokenSource();
        _loop = Task.Run(() => polling.PollAsync(dispatcher.HandleAsync, _cancellation.Token), CancellationToken.None);
        _logs.CreateLogger("bot").LogInformation("Bot Telegram avviato con long polling.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cancellation?.Cancel();
        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _loop = null;
    }
}
