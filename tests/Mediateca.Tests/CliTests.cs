using Mediateca.Cli;
using Mediateca.Core.Infrastructure;
using Mediateca.Core.Settings;

namespace Mediateca.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task Cli_inizializza_il_database()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediateca-cli-tests", Guid.NewGuid().ToString("N"));
        var previousHome = Environment.GetEnvironmentVariable(MediatecaPaths.HomeVariable);
        try
        {
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, directory);

            Assert.Equal(0, Mediateca.Cli.Program.Main(["db"]));
            Assert.True(File.Exists(Path.Combine(directory, "database.db")));
        }
        finally
        {
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, previousHome);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            for (var attempt = 0; attempt < 5 && Directory.Exists(directory); attempt++)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                    await Task.Delay(50, cancellationToken: TestContext.Current.CancellationToken);
                }
            }
        }
    }

    [Fact]
    public void Cli_gestisce_password_e_rifiuta_sottocomandi_sconosciuti()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediateca-cli-tests", Guid.NewGuid().ToString("N"));
        var previousHome = Environment.GetEnvironmentVariable(MediatecaPaths.HomeVariable);
        try
        {
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, directory);

            Assert.Equal(0, Commands.Settings(["password", "password-di-prova"]));
            var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
            var settings = store.Load();
            Assert.True(settings.WebDavRequireAuth);
            Assert.False(string.IsNullOrWhiteSpace(settings.WebDavPasswordHash));

            Assert.Throws<ArgumentException>(() => Commands.Settings(["sconosciuto"]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, previousHome);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Cli_imposta_e_mostra_solo_url_https()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediateca-cli-tests", Guid.NewGuid().ToString("N"));
        var previousHome = Environment.GetEnvironmentVariable(MediatecaPaths.HomeVariable);
        try
        {
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, directory);

            Assert.Equal(0, Commands.Settings(["set", "public-url", "https://esempio.test"]));
            Assert.Equal(0, Commands.Settings(["show"]));

            var store = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
            Assert.Equal(new Uri("https://esempio.test"), store.Load().PublicBaseUrl);
            var insecureScheme = string.Concat("ht", "tp");
            var insecureUrl = string.Concat(insecureScheme, "://esempio.test");
            Assert.Throws<ArgumentException>(() => Commands.Settings(["set", "public-url", insecureUrl]));
        }
        finally
        {
            Environment.SetEnvironmentVariable(MediatecaPaths.HomeVariable, previousHome);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
