using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Server;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediateca.Tests;

public sealed class TransferTests
{
    [Fact]
    public void TransferStore_Accoda_aggiorna_e_chiude_un_trasferimento()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            var media = new SqliteMediaStore(database);
            media.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var itemId = media.Upsert(new MediaItem { ChannelId = 1, MessageId = 1, Name = "uno.bin", Size = 100 });
            var store = new TransferStore(database);

            var id = store.Enqueue(itemId, "download");
            var queued = store.Find(id);
            Assert.NotNull(queued);
            Assert.Equal("queued", queued!.State);

            store.MarkRunning(id);
            store.UpdateProgress(id, 50, 100);
            store.Complete(id);

            var completed = store.Find(id);
            Assert.NotNull(completed);
            Assert.Equal("completed", completed!.State);
            Assert.Equal(100, completed.BytesDone);
            Assert.Single(store.List("completed"));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task TransferQueueService_scarica_il_file_e_segna_completato()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            var media = new SqliteMediaStore(database);
            media.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var bytes = Enumerable.Range(0, 300_000).Select(static index => (byte)(index % 251)).ToArray();
            var sourceRoot = Path.Combine(directory, "source");
            Directory.CreateDirectory(sourceRoot);
            File.WriteAllBytes(Path.Combine(sourceRoot, "film.bin"), bytes);
            var itemId = media.Upsert(new MediaItem { ChannelId = 1, MessageId = 3, Name = "film.bin", Size = bytes.Length });

            var store = new TransferStore(database);
            var jobId = store.Enqueue(itemId, "download");
            var service = new TransferQueueService(
                store,
                media,
                new LocalMediaFileSource(sourceRoot),
                Path.Combine(directory, "downloads"),
                NullLogger<TransferQueueService>.Instance);

            var processed = await service.ProcessPendingAsync();

            Assert.Equal(1, processed);
            var job = store.Find(jobId);
            Assert.Equal("completed", job!.State);
            var downloaded = Path.Combine(directory, "downloads", "film.bin");
            Assert.True(File.Exists(downloaded));
            Assert.Equal(bytes, await File.ReadAllBytesAsync(downloaded));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task TransferQueueService_segna_fallito_se_la_sorgente_manca()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            var media = new SqliteMediaStore(database);
            media.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var itemId = media.Upsert(new MediaItem { ChannelId = 1, MessageId = 4, Name = "assente.bin", Size = 10 });

            var store = new TransferStore(database);
            var jobId = store.Enqueue(itemId, "download");
            var service = new TransferQueueService(
                store,
                media,
                new LocalMediaFileSource(Path.Combine(directory, "vuota")),
                Path.Combine(directory, "downloads"),
                NullLogger<TransferQueueService>.Instance);

            await service.ProcessPendingAsync();

            Assert.Equal("failed", store.Find(jobId)!.State);
        }
        finally
        {
            Cleanup(directory);
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
