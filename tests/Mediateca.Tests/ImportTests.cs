using Mediateca.Data;
using Mediateca.Server;

namespace Mediateca.Tests;

public sealed class ImportTests
{
    [Fact]
    public async Task L_import_aggiunge_i_file_e_resta_idempotente()
    {
        var directory = NewTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "import");
            Directory.CreateDirectory(Path.Combine(folder, "sub"));
            File.WriteAllBytes(Path.Combine(folder, "a.mkv"), new byte[1000]);
            File.WriteAllBytes(Path.Combine(folder, "sub", "b.epub"), new byte[500]);
            File.WriteAllText(Path.Combine(folder, "vuoto.txt"), "");

            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            var service = new LocalImportService(database);

            var first = await service.ImportFolderAsync(folder);
            Assert.Equal(2, first.Added);
            Assert.Equal(1, first.Skipped);
            Assert.Equal(1500, first.Bytes);

            var second = await service.ImportFolderAsync(folder);
            Assert.Equal(0, second.Added);
            Assert.Equal(2, second.AlreadyPresent);

            var store = new SqliteMediaStore(database);
            var channels = store.ListChannels();
            Assert.Single(channels);
            Assert.Equal("Import locale", channels[0].Title);

            var items = store.Search("", null, 10);
            Assert.Equal(2, items.Count);
            Assert.Contains(items, item => item.Name == "a.mkv");
            Assert.Contains(items, item => item.Name == "sub/b.epub");
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task Il_file_importato_e_servibile_in_streaming_dalla_sorgente_locale()
    {
        var directory = NewTempDirectory();
        try
        {
            var folder = Path.Combine(directory, "media");
            Directory.CreateDirectory(folder);
            var bytes = Enumerable.Range(0, 4096).Select(static index => (byte)(index % 251)).ToArray();
            File.WriteAllBytes(Path.Combine(folder, "film.mkv"), bytes);

            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            await new LocalImportService(database).ImportFolderAsync(folder);

            var store = new SqliteMediaStore(database);
            var item = store.Search("", null, 10).Single();
            var source = new LocalMediaFileSource(folder);
            var media = await source.OpenReadAsync(item);

            Assert.NotNull(media);
            await using var stream = media!.Stream;
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            Assert.Equal(bytes, memory.ToArray());
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
