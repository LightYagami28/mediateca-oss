using Mediateca.Data;
using Mediateca.Telegram;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mediateca.Ui.Services;

public sealed class BotService : IHostedService
{
    private readonly UiOptions _options;
    private readonly MediatecaDatabase _database;
    private readonly LogBuffer _logs;
    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    public BotService(IOptions<UiOptions> options, MediatecaDatabase database, LogBuffer logs)
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
        var dispatcher = new BotCommandDispatcher(client, new BotCatalogAdapter(_database), new BotStatisticsAdapter(_database), new BotUserDirectoryAdapter(_database, _options.BotAdminIds));
        var polling = new BotPollingService(client, _logs.CreateLogger("bot"));
        _cancellation = new CancellationTokenSource();
        _loop = Task.Run(() => polling.PollAsync(dispatcher.HandleAsync, _cancellation.Token), CancellationToken.None);
        _logs.CreateLogger("bot").LogInformation("Bot Telegram avviato con long polling.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cancellation is not null)
        {
            await _cancellation.CancelAsync().ConfigureAwait(false);
        }
        if (_loop != null)
        {
            try
            {
                await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                _logs.CreateLogger("bot").LogInformation(exception, "Arresto del bot Telegram interrotto prima della fine del polling.");
            }
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _loop = null;
    }
}
