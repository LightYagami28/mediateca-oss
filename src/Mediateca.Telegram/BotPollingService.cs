using Microsoft.Extensions.Logging;

namespace Mediateca.Telegram;

public sealed class BotPollingService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(2);

    private readonly TelegramBotClient _client;
    private readonly ILogger _logger;

    public BotPollingService(TelegramBotClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PollAsync(Func<BotUpdate, CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        if (handler != null)
        {
            ArgumentNullException.ThrowIfNull(handler);

            var offset = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                IReadOnlyList<BotUpdate> updates;
                try
                {
                    updates = await _client.GetUpdatesAsync(offset, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "getUpdates fallito: nuovo tentativo tra poco");
                    if (!await DelayAsync(ErrorDelay, cancellationToken).ConfigureAwait(false))
                    {
                        break;
                    }

                    continue;
                }

                offset = await HandleUpdatesAsync(updates, offset, handler, cancellationToken).ConfigureAwait(false);
                if (updates.Count == 0 && !await DelayAsync(PollInterval, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private async Task<int> HandleUpdatesAsync(
        IReadOnlyList<BotUpdate> updates,
        int offset,
        Func<BotUpdate, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            try
            {
                await handler(update, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Handler fallito per l'aggiornamento {UpdateId}", update.UpdateId);
            }

            offset = Math.Max(offset, update.UpdateId + 1);
        }

        return offset;
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
