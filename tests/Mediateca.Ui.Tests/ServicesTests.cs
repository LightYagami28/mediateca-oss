using Mediateca.Core.Domain;
using Mediateca.Core.Security;
using Mediateca.Data;
using Mediateca.Ui.Services;
using Mediateca.Ui.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Mediateca.Ui.Tests;

public class ServicesTests
{
    [Fact]
    public async Task SqliteCatalogSource_Legge_il_catalogo_reale()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            var store = new SqliteMediaStore(database);
            store.UpsertChannel(new Channel { Id = 1, Title = "Canale Uno", IsSource = true });
            store.UpsertChannel(new Channel { Id = 2, Title = "Canale Due" });
            store.Upsert(new MediaItem { ChannelId = 1, MessageId = 10, Name = "film.mkv", Kind = MediaKind.Movie, Size = 1000 });
            store.Upsert(new MediaItem { ChannelId = 2, MessageId = 11, Name = "libro.epub", Kind = MediaKind.Book, Size = 500 });

            var source = new SqliteCatalogSource(database);
            var rows = await source.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, rows.Count);
            var film = rows.Single(row => row.Name == "film.mkv");
            Assert.Equal("Canale Uno", film.Channel);
            Assert.Equal("Film", film.KindLabel);

            var found = await source.SearchAsync("libro", 10, TestContext.Current.CancellationToken);
            Assert.Single(found);
            Assert.Equal("Canale Due", found[0].Channel);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void SettingsService_Salva_e_ricarica_senza_il_token_in_chiaro()
    {
        var path = Path.Combine(NewTempDirectory(), "settings.json");
        var service = new SettingsService(path);
        var options = new UiOptions
        {
            Theme = "Light",
            ServerPort = 9123,
            ServerBindAddress = "0.0.0.0",
            ServerApiToken = "segreto-di-prova-123",
            WebDavEnabled = false,
        };

        service.Save(options);

        var onDisk = File.ReadAllText(path);
        Assert.DoesNotContain("segreto-di-prova-123", onDisk);

        var loaded = service.Load(new UiOptions());
        Assert.Equal("Light", loaded.Theme);
        Assert.Equal(9123, loaded.ServerPort);
        Assert.Equal("0.0.0.0", loaded.ServerBindAddress);
        Assert.Equal("segreto-di-prova-123", loaded.ServerApiToken);
        Assert.False(loaded.WebDavEnabled);
    }

    [Fact]
    public void SettingsService_Un_file_corrotto_ricade_sui_valori_predefiniti()
    {
        var path = Path.Combine(NewTempDirectory(), "settings.json");
        File.WriteAllText(path, "{ questo non è json valido");

        var loaded = new SettingsService(path).Load(new UiOptions { ServerPort = 4242 });

        Assert.Equal(4242, loaded.ServerPort);
    }

    [Fact]
    public void LogBuffer_rispetta_la_capacita_e_ordina_per_tempo()
    {
        var buffer = new LogBuffer(capacity: 3);
        var logger = buffer.CreateLogger("test");

        logger.LogInformation("uno");
        logger.LogWarning("due");
        logger.LogError("tre");
        logger.LogInformation("quattro");

        Assert.Equal(3, buffer.Entries.Count);
        Assert.Equal(["due", "tre", "quattro"], buffer.Entries.Select(entry => entry.Message));
    }

    [Fact]
    public void ChannelsViewModel_carica_i_canali()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            new SqliteMediaStore(database).UpsertChannel(new Channel { Id = 1, Title = "Sorgente", IsSource = true });

            var viewModel = new ChannelsViewModel(database, NullLogger<ChannelsViewModel>.Instance);
            viewModel.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Single(viewModel.Rows);
            Assert.Equal("Sorgente", viewModel.Rows[0].Title);
            Assert.Equal("Sorgente", viewModel.Rows[0].SourceText);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void SettingsService_La_password_webdav_resta_solo_come_hash()
    {
        var path = Path.Combine(NewTempDirectory(), "settings.json");
        var service = new SettingsService(path);
        var viewModel = new SettingsViewModel(
            service,
            Options.Create(new UiOptions()),
            new ThemeService());
        viewModel.WebDavPassword = "password-segreta";

        viewModel.SaveCommand.Execute(null);

        var onDisk = File.ReadAllText(path);
        Assert.DoesNotContain("password-segreta", onDisk);
        var loaded = service.Load(new UiOptions());
        Assert.True(PasswordHasher.Verify("password-segreta", loaded.WebDavPasswordHash));
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-ui-tests", Guid.NewGuid().ToString("N"));
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
