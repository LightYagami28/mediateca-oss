using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Mtproto;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediateca.Tests;

public sealed class AddressBookTests
{
    [Fact]
    public void La_rubrica_aggiorna_le_voci_e_cerca_per_nome_o_username()
    {
        var directory = NewTempDirectory();
        try
        {
            var store = new AddressBookStore(NewDatabase(directory));

            Assert.Equal(3, store.Upsert(
            [
                new AddressBookEntry { PeerId = 1, AccessHash = 11, Title = "Mario Rossi", Username = "mario", Phone = "39000" },
                new AddressBookEntry { PeerId = 2, AccessHash = 22, Kind = AddressBookKind.Channel, Title = "Film HD", Username = "filmhd" },
                new AddressBookEntry { PeerId = 3, Kind = AddressBookKind.Bot, Title = "Mediateca Bot", Username = "mediatecabot" },
            ]));

            store.Upsert([new AddressBookEntry { PeerId = 1, Title = "Mario R.", Username = "mario" }]);

            var mario = Assert.Single(store.Search("@mario"));
            Assert.Equal("Mario R.", mario.Title);
            Assert.Equal(11, mario.AccessHash);
            Assert.Equal("39000", mario.Phone);

            Assert.Equal([2L], store.Search("", AddressBookKind.Channel).Select(static entry => entry.PeerId));
            Assert.Equal(["Film HD", "Mario R.", "Mediateca Bot"], store.Search("").Select(static entry => entry.Title));

            Assert.True(store.Remove(3));
            Assert.False(store.Remove(3));
            Assert.Equal(2, store.Search("").Count);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task L_import_della_rubrica_richiede_le_credenziali()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = new TelegramMediaSource(new MtprotoOptions { CacheDirectory = Path.Combine(directory, "cache") }, NewDatabase(directory), NullLogger<TelegramMediaSource>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(() => source.ImportAddressBookAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static MediatecaDatabase NewDatabase(string directory)
    {
        var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
        database.Migrate();
        return database;
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
