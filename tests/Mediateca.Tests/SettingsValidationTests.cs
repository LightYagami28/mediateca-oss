using System.Text.Json;
using Mediateca.Core.Abstractions;
using Mediateca.Core.Settings;

namespace Mediateca.Tests;

/// <summary>Test della convalida delle impostazioni e della lettura rigorosa.</summary>
public sealed class SettingsValidationTests
{
    [Fact]
    public void Validate_ImpostazioniPredefinite_SenzaProblemi()
    {
        var problems = new MediatecaSettings().Validate();
        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_ValoriNonValidi_SegnalaOgniCampo()
    {
        var settings = new MediatecaSettings
        {
            Language = "ita",
            ServerBindPort = 0,
            ServerPublicPort = 70000,
            PublicBaseUrl = "ftp://esempio.test",
            WebDavEnabled = true,
            WebDavRequireAuth = true,
            WebDavUser = "",
            BackupTime = "25:99",
            BackupRetention = 0,
            UiScale = 5,
            Theme = "blu",
            RootDirectory = "relativo",
            VideoExtensions = ["senzaPunto"],
        };

        var problems = settings.Validate();
        Assert.Equal(11, problems.Count);
        Assert.Contains(problems, p => p.Contains("ServerBindPort"));
        Assert.Contains(problems, p => p.Contains("BackupTime"));
        Assert.Contains(problems, p => p.Contains("VideoExtensions"));
    }

    [Fact]
    public void LoadStrict_JsonCorrotto_WrapsEccezioneOriginale()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{ json non valido");
            var store = new JsonSettingsStore(path);

            var exception = Assert.Throws<InvalidOperationException>(() => store.LoadStrict());
            Assert.IsType<JsonException>(exception.InnerException);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void LoadStrict_ValoriNonValidi_SollevaConDettagli()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"serverBindPort\": 0, \"backupTime\": \"x\"}");
            var store = new JsonSettingsStore(path);

            var exception = Assert.Throws<InvalidOperationException>(() => store.LoadStrict());
            Assert.Contains("ServerBindPort", exception.Message);
            Assert.Contains("BackupTime", exception.Message);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void Load_JsonCorrotto_RestituiscePredefiniti()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{ json non valido");
            var store = new JsonSettingsStore(path);

            var settings = store.Load();
            Assert.Equal(8083, settings.ServerBindPort);
            Assert.Equal("it", settings.Language);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void SaveValidated_ImpostazioniNonValide_NonScriveNulla()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsStore(path);

            Assert.Throws<InvalidOperationException>(() => store.SaveValidated(new MediatecaSettings { BackupRetention = 0 }));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void SaveValidated_ImpostazioniValide_RileggibiliDaLoadStrict()
    {
        var directory = NewTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var store = new JsonSettingsStore(path);
            var original = new MediatecaSettings { Language = "en", ServerBindPort = 9999, Theme = "dark" };

            store.SaveValidated(original);
            var loaded = store.LoadStrict();

            Assert.Equal(original.Language, loaded.Language);
            Assert.Equal(original.ServerBindPort, loaded.ServerBindPort);
            Assert.Equal(original.Theme, loaded.Theme);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string NewTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediateca-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Cleanup(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
