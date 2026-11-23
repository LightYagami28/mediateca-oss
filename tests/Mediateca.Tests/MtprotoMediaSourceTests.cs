using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Mtproto;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediateca.Tests;

public sealed class MtprotoMediaSourceTests
{
    private const int ApiId = 12345;
    private const string ApiHash = "hash-di-prova";

    [Fact]
    public async Task Senza_credenziali_la_lettura_restituisce_null()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = NewSource(directory, apiId: 0, cache: Path.Combine(directory, "cache"));

            Assert.False(source.IsConfigured);
            Assert.Null(await source.OpenReadAsync(NewItem(), cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    [Fact]
    public async Task Senza_cartella_cache_la_lettura_restituisce_null()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = NewSource(directory, ApiId, cache: string.Empty);

            Assert.True(source.IsConfigured);
            Assert.Null(await source.OpenReadAsync(NewItem(), cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    [Fact]
    public async Task File_assente_e_canale_sconosciuto_restituiscono_null()
    {
        var directory = NewTempDirectory();
        try
        {
            var cache = Path.Combine(directory, "cache");
            await using var source = NewSource(directory, ApiId, cache);

            Assert.Null(await source.OpenReadAsync(NewItem(), cancellationToken: TestContext.Current.CancellationToken));
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    [Fact]
    public async Task File_gia_in_cache_viene_aperto_con_la_dimensione_corretta()
    {
        var directory = NewTempDirectory();
        try
        {
            var cache = Path.Combine(directory, "cache");
            Directory.CreateDirectory(cache);
            await File.WriteAllBytesAsync(Path.Combine(cache, "7-42-film.mkv"), [1, 2, 3, 4], cancellationToken: TestContext.Current.CancellationToken);
            await using var source = NewSource(directory, ApiId, cache);

            var media = await source.OpenReadAsync(NewItem(), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(media);
            Assert.Equal(4, media.Length);
            Assert.Equal("video/x-matroska", media.ContentType);
            await media.Stream.DisposeAsync();
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    [Theory]
    [InlineData("film.mp4", "video/mp4")]
    [InlineData("film.avi", "video/x-msvideo")]
    [InlineData("film.flac", "audio/flac")]
    [InlineData("film.mp3", "audio/mpeg")]
    [InlineData("film.m4a", "audio/mp4")]
    [InlineData("film.jpg", "image/jpeg")]
    [InlineData("film.jpeg", "image/jpeg")]
    [InlineData("film.png", "image/png")]
    [InlineData("film.epub", "application/epub+zip")]
    [InlineData("film.pdf", "application/pdf")]
    [InlineData("film.bin", "application/octet-stream")]
    public async Task Il_content_type_deriva_dall_estensione_del_file(string name, string expected)
    {
        var directory = NewTempDirectory();
        try
        {
            var cache = Path.Combine(directory, "cache");
            Directory.CreateDirectory(cache);
            await File.WriteAllBytesAsync(Path.Combine(cache, "7-42-" + name), [1], cancellationToken: TestContext.Current.CancellationToken);
            await using var source = NewSource(directory, ApiId, cache);

            var media = await source.OpenReadAsync(NewItem(name), cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(media);
            Assert.Equal(expected, media.ContentType);
            await media.Stream.DisposeAsync();
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    [Fact]
    public async Task L_importazione_di_un_canale_senza_credenziali_non_parte()
    {
        var directory = NewTempDirectory();
        try
        {
            await using var source = NewSource(directory, apiId: 0, cache: Path.Combine(directory, "cache"));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => source.ImportChannelAsync("canale", cancellationToken: TestContext.Current.CancellationToken));

            Assert.Contains("MTProto non configurato", error.Message);
        }
        finally
        {
            await Cleanup(directory);
        }
    }

    private static TelegramMediaSource NewSource(string directory, int apiId, string cache) =>
        new(new MtprotoOptions
        {
            ApiId = apiId,
            ApiHash = apiId > 0 ? ApiHash : string.Empty,
            SessionPath = Path.Combine(directory, "sessione.dat"),
            CacheDirectory = cache,
        }, NewDatabase(directory), NullLogger<TelegramMediaSource>.Instance);

    private static MediatecaDatabase NewDatabase(string directory)
    {
        var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
        database.Migrate();
        return database;
    }

    private static MediaItem NewItem(string name = "film.mkv") =>
        new() { ChannelId = 7, MessageId = 42, Name = name, Kind = MediaKind.Movie, Size = 4 };

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task Cleanup(string directory)
    {
        SqliteConnection.ClearAllPools();
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
                await Task.Delay(50);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(50);
            }
        }
    }
}
