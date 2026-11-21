using Mediateca.Core.Domain;
using Mediateca.Data;
using Microsoft.Data.Sqlite;

namespace Mediateca.Tests;

/// <summary>Test di <see cref="SqliteMediaStore"/>: chiave canale/messaggio, ricerca e statistiche.</summary>
public sealed class MediaStoreTests
{
    /// <summary>Senza il canale la foreign key rifiuta l'inserimento; dopo <c>UpsertChannel</c> passa.</summary>
    [Fact]
    public void Upsert_RichiedeUnCanaleEsistente()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            var exception = Assert.Throws<SqliteException>(() => store.Upsert(Item(42, 1, "orfano.mp4")));
            Assert.Contains("FOREIGN KEY", exception.Message);

            store.UpsertChannel(Canale(42, "Arrivato"));
            Assert.True(store.Upsert(Item(42, 1, "orfano.mp4")) > 0);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>L'upsert è idempotente sulla coppia canale/messaggio e aggiorna i campi.</summary>
    [Fact]
    public void Upsert_ÈIdempotenteSullaCoppiaCanaleMessaggio()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(1, "Canale"));

            var first = store.Upsert(Item(1, 10, "prima.mkv", size: 100));
            var second = store.Upsert(Item(1, 10, "seconda.mkv", size: 250, titolo: "Titolo aggiornato"));

            Assert.Equal(first, second);

            var trovato = Assert.Single(store.Search("seconda"));
            Assert.Equal(250, trovato.Size);
            Assert.Equal("Titolo aggiornato", trovato.Title);
            Assert.Equal(1, store.Statistics().Items);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Messaggi diversi nello stesso canale producono righe diverse.</summary>
    [Fact]
    public void Upsert_CreaRigheDistintePerMessaggiDiversi()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(1, "Canale"));

            var first = store.Upsert(Item(1, 1, "a.mp4"));
            var second = store.Upsert(Item(1, 2, "b.mp4"));

            Assert.NotEqual(first, second);
            Assert.Equal(2, store.Statistics().Items);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>La ricerca trova per nome e per titolo, ignora le maiuscole e filtra per tipo.</summary>
    [Fact]
    public void Search_TrovaPerNomeTipoETitolo()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(1, "Canale"));
            store.Upsert(Item(1, 1, "Vacanze Romantiche.mkv", MediaKind.Movie, 100));
            store.Upsert(Item(1, 2, "foto-mare.jpg", MediaKind.Image, 50));
            store.Upsert(Item(1, 3, "documento.bin", MediaKind.Document, 10, titolo: "Relazione Annuale"));

            Assert.Single(store.Search("vacanze"));
            Assert.Single(store.Search("romantiche"));
            Assert.Single(store.Search("relazione"));

            var immagini = Assert.Single(store.Search("", MediaKind.Image));
            Assert.Equal("foto-mare.jpg", immagini.Name);

            Assert.Equal(3, store.Search("").Count);
            Assert.Single(store.Search("", null, limit: 1));
            Assert.Empty(store.Search("inesistente"));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>I risultati sono ordinati dal più recente.</summary>
    [Fact]
    public void Search_OrdinaDalPiuRecente()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(1, "Canale"));
            store.Upsert(Item(1, 1, "vecchio.mkv", updated: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            store.Upsert(Item(1, 2, "nuovo.mkv", updated: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)));

            var results = store.Search("");

            Assert.Equal(2, results.Count);
            Assert.Equal("nuovo.mkv", results[0].Name);
            Assert.Equal("vecchio.mkv", results[1].Name);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Le statistiche contano elementi, byte totali e soli canali sorgente.</summary>
    [Fact]
    public void Statistics_ContaItemByteECanaliSorgente()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(1, "Sorgente", sorgente: true));
            store.UpsertChannel(Canale(2, "Secondario"));
            store.Upsert(Item(1, 1, "a.mp4", size: 100));
            store.Upsert(Item(1, 2, "b.mp4", size: 250));
            store.Upsert(Item(2, 3, "c.mp4", size: 7));

            var stats = store.Statistics();

            Assert.Equal(3, stats.Items);
            Assert.Equal(357, stats.Bytes);
            Assert.Equal(1, stats.Channels);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>Un secondo <c>UpsertChannel</c> aggiorna la riga esistente.</summary>
    [Fact]
    public void UpsertChannel_AggiornaLaRigaEsistente()
    {
        var (database, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(5, "Prima"));
            store.UpsertChannel(new Channel { Id = 5, Title = "Dopo", IsSource = true, ItemCount = 4, LastMessageId = 99 });

            using var connection = database.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT title, is_source, item_count, last_message_id FROM channels WHERE id = 5;";
            using var reader = command.ExecuteReader();

            Assert.True(reader.Read());
            Assert.Equal("Dopo", reader.GetString(0));
            Assert.Equal(1, reader.GetInt32(1));
            Assert.Equal(4, reader.GetInt32(2));
            Assert.Equal(99, reader.GetInt32(3));
            Assert.False(reader.Read());
        }
        finally
        {
            Cleanup(directory);
        }
    }

    /// <summary>La lista dei canali rispetta l'ordine alfabetico e conserva i flag.</summary>
    [Fact]
    public void ListChannels_RestituisceTuttiICanaliInOrdineAlfabetico()
    {
        var (_, store, directory) = CreateStore();
        try
        {
            store.UpsertChannel(Canale(2, "Zeta", sorgente: true));
            store.UpsertChannel(Canale(1, "Alfa"));
            store.UpsertChannel(Canale(3, "Medio", sorgente: true));

            var channels = store.ListChannels();

            Assert.Equal(3, channels.Count);
            Assert.Equal(["Alfa", "Medio", "Zeta"], channels.Select(c => c.Title));
            Assert.Equal(2, channels.Count(c => c.IsSource));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static (MediatecaDatabase Database, SqliteMediaStore Store, string Directory) CreateStore()
    {
        var directory = NewTempDirectory();
        var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
        database.Migrate();
        return (database, new SqliteMediaStore(database), directory);
    }

    private static Channel Canale(long id, string titolo, bool sorgente = false) => new()
    {
        Id = id,
        Title = titolo,
        IsSource = sorgente,
    };

    private static MediaItem Item(
        long channelId,
        int messageId,
        string name,
        MediaKind kind = MediaKind.Movie,
        long size = 100,
        string? titolo = null,
        DateTimeOffset? updated = null) => new()
        {
            ChannelId = channelId,
            MessageId = messageId,
            Name = name,
            Kind = kind,
            Size = size,
            Title = titolo,
            UpdatedAt = updated ?? DateTimeOffset.UtcNow,
        };

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        if (directory != null)
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

                    Directory.Delete(directory, recursive: true);
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
        else
        {
            throw new ArgumentNullException();
        }
    }
}
