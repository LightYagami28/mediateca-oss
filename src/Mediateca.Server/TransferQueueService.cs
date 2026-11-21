using Mediateca.Data;
using Microsoft.Extensions.Logging;

namespace Mediateca.Server;

public sealed class TransferQueueService
{
    private const int ProgressIntervalBytes = 4 * 1024 * 1024;

    private readonly TransferStore _store;
    private readonly SqliteMediaStore _media;
    private readonly IMediaFileSource _source;
    private readonly string _downloadRoot;
    private readonly ILogger<TransferQueueService> _logger;

    public TransferQueueService(TransferStore store, SqliteMediaStore media, IMediaFileSource source, string downloadRoot, ILogger<TransferQueueService> logger)
    {
        _store = store;
        _media = media;
        _source = source;
        _downloadRoot = Path.GetFullPath(downloadRoot);
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;
        foreach (var job in _store.List("queued"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _store.MarkRunning(job.Id);
            try
            {
                await RunAsync(job, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _store.Fail(job.Id, "annullato");
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Trasferimento {JobId} fallito", job.Id);
                _store.Fail(job.Id, exception.Message);
            }

            processed++;
        }

        return processed;
    }

    private async Task RunAsync(TransferJob job, CancellationToken cancellationToken)
    {
        if (job.ItemId is null)
        {
            _store.Fail(job.Id, "elemento assente");
            return;
        }

        var item = _media.Find(job.ItemId.Value);
        if (item is null)
        {
            _store.Fail(job.Id, "elemento non trovato nel catalogo");
            return;
        }

        var media = await _source.OpenReadAsync(item, cancellationToken).ConfigureAwait(false);
        if (media is null)
        {
            _store.Fail(job.Id, "sorgente non disponibile");
            return;
        }

        var destination = Path.GetFullPath(Path.Combine(_downloadRoot, item.Name));
        if (!destination.StartsWith(_downloadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            _store.Fail(job.Id, "nome destinazione non valido");
            return;
        }

        Directory.CreateDirectory(_downloadRoot);
        await using var stream = media.Stream;
        await using var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);

        var buffer = new byte[64 * 1024];
        long total = 0;
        long sinceProgress = 0;
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            sinceProgress += read;
            if (sinceProgress >= ProgressIntervalBytes)
            {
                _store.UpdateProgress(job.Id, total, media.Length);
                sinceProgress = 0;
            }
        }

        await file.FlushAsync(cancellationToken).ConfigureAwait(false);
        _store.Complete(job.Id);
        _logger.LogInformation("Trasferimento {JobId} completato: {Destination}", job.Id, destination);
    }
}
