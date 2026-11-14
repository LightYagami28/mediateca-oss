using Microsoft.Extensions.Options;
using Teledisk.Core.Domain;
using Teledisk.Data;
using Teledisk.Server;
using Teledisk.Ui.Services;

namespace Teledisk.Ui.Tests;

public class BotServiceTests
{
    [Fact]
    public async Task Senza_token_il_bot_resta_inattivo_e_lo_segnala_nel_log()
    {
        var directory = NewTempDirectory();
        var buffer = new LogBuffer();
        var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
        database.Migrate();
        var service = new BotService(Options.Create(new UiOptions()), database, buffer);

        await service.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(service.IsConfigured);
        Assert.Contains(buffer.Entries, entry => entry.Message.Contains("non configurato", StringComparison.OrdinalIgnoreCase));
        await service.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task La_sorgente_composta_prova_tutte_le_sorgenti_in_ordine()
    {
        var directory = NewTempDirectory();
        try
        {
            var localRoot = Path.Combine(directory, "media");
            Directory.CreateDirectory(localRoot);
            File.WriteAllBytes(Path.Combine(localRoot, "locale.bin"), new byte[32]);

            var database = new TelediskDatabase(Path.Combine(directory, "teledisk.db"));
            database.Migrate();
            var store = new SqliteMediaStore(database);
            store.UpsertChannel(new Channel { Id = 1, Title = "Sorgente" });
            var localItem = store.Upsert(new MediaItem { ChannelId = 1, MessageId = 1, Name = "locale.bin", Size = 32 });
            var remoteItem = store.Upsert(new MediaItem { ChannelId = 1, MessageId = 2, Name = "remoto.bin", Size = 32 });

            var composite = new CompositeMediaFileSource(new LocalMediaFileSource(localRoot));

            var local = await composite.OpenReadAsync(store.Find(localItem)!, TestContext.Current.CancellationToken);
            Assert.NotNull(local);
            await local!.Stream.DisposeAsync();

            var remote = await composite.OpenReadAsync(store.Find(remoteItem)!, TestContext.Current.CancellationToken);
            Assert.Null(remote);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Le_impostazioni_proteggono_bot_e_mtproto_prima_di_scrivere()
    {
        var directory = NewTempDirectory();
        var path = Path.Combine(directory, "settings.json");
        var service = new SettingsService(path);

        service.Save(new UiOptions
        {
            BotToken = "123456789:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            MtprotoApiId = 12345,
            MtprotoApiHash = "hash-segreto-mtproto",
        });

        var onDisk = File.ReadAllText(path);
        Assert.DoesNotContain("123456789:AAAA", onDisk);
        Assert.DoesNotContain("hash-segreto-mtproto", onDisk);
        Assert.Contains("12345", onDisk);

        var loaded = service.Load(new UiOptions());
        Assert.Equal("123456789:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", loaded.BotToken);
        Assert.Equal("hash-segreto-mtproto", loaded.MtprotoApiHash);
        Assert.Equal(12345, loaded.MtprotoApiId);
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
