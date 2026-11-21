using Mediateca.Core.Abstractions;
using Mediateca.Core.Settings;
using Microsoft.Data.Sqlite;

namespace Mediateca.Tests;

/// <summary>Test di <see cref="JsonSettingsStore"/>: persistenza, file corrotto e scrittura atomica.</summary>
public sealed class SettingsTests
{
    /// <summary>Senza file, il caricamento restituisce i valori predefiniti senza scrivere nulla.</summary>
    [Fact]
    public void Load_SenzaFile_RestituiscePredefiniti()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsStore(path);

            var settings = store.Load();

            Assert.Equal("it", settings.Language);
            Assert.Equal(8083, settings.ServerBindPort);
            Assert.Equal("127.0.0.1", settings.ServerBindAddress);
            Assert.Equal(Path.GetFullPath(directory), settings.RootDirectory);
            Assert.False(File.Exists(path));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Salvataggio e rilettura conservano i valori, inclusi gli elenchi.</summary>
    [Fact]
    public void SaveThenLoad_ConservaLeImpostazioni()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsStore(path);
            var original = new MediatecaSettings
            {
                Language = "en",
                ServerBindPort = 9999,
                ServerPublicPort = 443,
                PublicBaseUrl = new Uri("https://esempio.test"),
                WebDavEnabled = true,
                WebDavAllowLan = true,
                WebDavRequireAuth = false,
                WebDavUser = "utente",
                BackupEnabled = true,
                BackupTime = "04:30",
                BackupRetention = 3,
                Theme = "dark",
                UiScale = 1.25,
                VideoExtensions = [".prova"],
            };

            store.Save(original);
            var loaded = store.Load();

            Assert.Equal(original.Language, loaded.Language);
            Assert.Equal(original.ServerBindPort, loaded.ServerBindPort);
            Assert.Equal(original.ServerPublicPort, loaded.ServerPublicPort);
            Assert.Equal(original.PublicBaseUrl, loaded.PublicBaseUrl);
            Assert.True(loaded.WebDavEnabled);
            Assert.True(loaded.WebDavAllowLan);
            Assert.False(loaded.WebDavRequireAuth);
            Assert.Equal("utente", loaded.WebDavUser);
            Assert.True(loaded.BackupEnabled);
            Assert.Equal("04:30", loaded.BackupTime);
            Assert.Equal(3, loaded.BackupRetention);
            Assert.Equal("dark", loaded.Theme);
            Assert.Equal(1.25, loaded.UiScale);
            Assert.Equal([".prova"], loaded.VideoExtensions);
            Assert.Equal(original.ImageExtensions, loaded.ImageExtensions);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Il JSON usa i nomi camelCase dichiarati dal formato.</summary>
    [Fact]
    public void Save_ScriveJsonCamelCase()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            new JsonSettingsStore(path).Save(new MediatecaSettings());

            var json = File.ReadAllText(path);
            Assert.Contains("\"language\"", json);
            Assert.Contains("\"serverBindPort\"", json);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Un file corrotto non impedisce l'avvio: si riparte dai predefiniti.</summary>
    [Theory]
    [InlineData("{ questo non è json")]
    [InlineData("")]
    [InlineData("{\"serverBindPort\": \"non-un-numero\"}")]
    public void Load_ConFileCorrotto_RestituiscePredefiniti(string content)
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, content);

            var settings = new JsonSettingsStore(path).Load();

            Assert.Equal("it", settings.Language);
            Assert.Equal(8083, settings.ServerBindPort);
            Assert.Equal(Path.GetFullPath(directory), settings.RootDirectory);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>La scrittura è atomica: dopo il salvataggio non resta alcun file temporaneo.</summary>
    [Fact]
    public void Save_NonLasciaFileTemporanei()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsStore(path);

            store.Save(new MediatecaSettings { Language = "fr" });
            store.Save(new MediatecaSettings { Language = "de" });

            Assert.True(File.Exists(path));
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal("de", store.Load().Language);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Il salvataggio crea la cartella mancante.</summary>
    [Fact]
    public void Save_CreaLaCartellaMancante()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "nuova", "cartella", "settings.json");

            new JsonSettingsStore(path).Save(new MediatecaSettings { Language = "es" });

            Assert.True(File.Exists(path));
            Assert.Equal("es", new JsonSettingsStore(path).Load().Language);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Constructor e salvataggio validano gli argomenti.</summary>
    [Fact]
    public void Store_ValidaGliArgomenti()
    {
        Assert.Throws<ArgumentException>(() => new JsonSettingsStore("  "));
        Assert.Throws<ArgumentNullException>(() => new JsonSettingsStore(null!));

        var directory = NewTempDirectory();
        try
        {
            var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
            Assert.Throws<ArgumentNullException>(() => store.Save(null!));
            Assert.Equal(Path.Combine(directory, "settings.json"), store.Path);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Le estensioni note sono riconosciute senza distinzione di maiuscole.</summary>
    [Fact]
    public void MediaExtensions_RiconosceLeEstensioni()
    {
        Assert.True(MediaExtensions.IsVideo("film.MKV"));
        Assert.True(MediaExtensions.IsAudio("brano.Mp3"));
        Assert.True(MediaExtensions.IsImage("foto.JPG"));
        Assert.False(MediaExtensions.IsVideo("foto.jpg"));
        Assert.False(MediaExtensions.IsImage("senza-estensione"));
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory))
                {
                    return;
                }

                if (directory != null)
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
