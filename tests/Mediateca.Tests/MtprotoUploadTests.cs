using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Mtproto;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediateca.Tests;

public sealed class MtprotoUploadTests
{
    [Fact]
    public async Task Senza_credenziali_l_upload_non_parte()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = NewSource(directory, apiId: 0);

            await Assert.ThrowsAsync<InvalidOperationException>(() => source.UploadAsync(1, Path.Combine(directory, "film.mkv")));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task L_upload_richiede_un_file_esistente()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = NewSource(directory, apiId: 12345);

            await Assert.ThrowsAsync<FileNotFoundException>(() => source.UploadAsync(1, Path.Combine(directory, "assente.mkv")));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task L_upload_richiede_un_canale_importato_con_access_hash()
    {
        var directory = NewTempDirectory();
        try
        {
            var file = Path.Combine(directory, "film.mkv");
            File.WriteAllBytes(file, new byte[16]);
            var database = NewDatabase(directory);
            new SqliteMediaStore(database).UpsertChannel(new Channel { Id = 7, Title = "Senza hash", Kind = ChannelKind.Channel });
            await using var source = new TelegramMediaSource(Options(directory, 12345), database, NullLogger<TelegramMediaSource>.Instance);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => source.UploadAsync(7, file));

            Assert.Contains("importalo prima", error.Message);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static TelegramMediaSource NewSource(string directory, int apiId) =>
        new(Options(directory, apiId), NewDatabase(directory), NullLogger<TelegramMediaSource>.Instance);

    private static MtprotoOptions Options(string directory, int apiId) => new()
    {
        ApiId = apiId,
        ApiHash = apiId > 0 ? "hash" : string.Empty,
        SessionPath = Path.Combine(directory, "sessione.dat"),
        CacheDirectory = Path.Combine(directory, "cache"),
    };

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
