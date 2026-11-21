using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace Mediateca.Ui.Services;

public sealed record LogEntry(DateTimeOffset At, string Level, string Message)
{
    public string TimeText => At.ToLocalTime().ToString("HH:mm:ss");
}

public sealed class LogBuffer : ILoggerProvider
{
    public const int DefaultCapacity = 500;

    private readonly Lock _gate = new();
    private readonly Queue<LogEntry> _entries = new();

    public LogBuffer(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public int Capacity { get; }

    public ILogger CreateLogger(string categoryName) => new BufferLogger(this);

    public void Dispose()
    {
    }

    private void Add(DateTimeOffset at, string level, string message)
    {
        var entry = new LogEntry(at, level, message);
        lock (_gate)
        {
            if (Entries.Count >= Capacity && Entries.Count > 0)
            {
                Entries.RemoveAt(0);
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
            Entries.Add(entry);
        }
    }

    private sealed class BufferLogger(LogBuffer buffer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter != null)
            {
                if (state != null)
                {
                    if (exception is not null)
                    {
                        buffer.Add(DateTimeOffset.Now, logLevel.ToString(), formatter(state, exception) + " | " + exception.Message);
                        return;
                    }

                    buffer.Add(DateTimeOffset.Now, logLevel.ToString(), formatter(state, exception));
                }
                else
                {
                    throw new ArgumentNullException();
                }
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
    }
}
