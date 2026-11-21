namespace Mediateca.Telegram;

public sealed class TelegramBotPool
{
    private const int TooManyRequests = 429;

    private readonly TelegramBotClient[] _clients;
    private readonly DateTimeOffset[] _availableAt;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private int _next;

    public TelegramBotPool(IEnumerable<TelegramBotClient> clients, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(clients);
        _clients = [.. clients.Where(static client => client.IsConfigured)];
        if (_clients.Length == 0)
        {
            throw new ArgumentException("Il pool richiede almeno un bot configurato.", nameof(clients));
        }

        _availableAt = new DateTimeOffset[_clients.Length];
        _time = timeProvider ?? TimeProvider.System;
    }

    public int Count => _clients.Length;

    public int AvailableCount
    {
        get
        {
            var now = _time.GetUtcNow();
            lock (_gate)
            {
                return _availableAt.Count(until => until <= now);
            }
        }
    }

    public async Task<T> ExecuteAsync<T>(Func<TelegramBotClient, CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        TelegramApiException? lastLimit = null;
        for (var attempt = 0; attempt < _clients.Length; attempt++)
        {
            var index = Acquire();
            if (index < 0)
            {
                break;
            }

            try
            {
                return await action(_clients[index], cancellationToken).ConfigureAwait(false);
            }
            catch (TelegramApiException error) when (error.ErrorCode == TooManyRequests)
            {
                lastLimit = error;
                Suspend(index, TimeSpan.FromSeconds(Math.Max(1, error.RetryAfterSeconds ?? 1)));
            }
        }

        throw new InvalidOperationException("Tutti i bot del pool sono fermi per il limite di frequenza di Telegram.", lastLimit);
    }

    private int Acquire()
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            for (var offset = 0; offset < _clients.Length; offset++)
            {
                var index = (_next + offset) % _clients.Length;
                if (_availableAt[index] <= now)
                {
                    _next = (index + 1) % _clients.Length;
                    return index;
                }
            }

            return -1;
        }
    }

    private void Suspend(int index, TimeSpan delay)
    {
        var until = _time.GetUtcNow() + delay;
        lock (_gate)
        {
            if (until > _availableAt[index])
            {
                _availableAt[index] = until;
            }
        }
    }
}
