using System.Net;
using Mediateca.Core.Domain;
using Mediateca.Media;
using Mediateca.Telegram;

namespace Mediateca.Tests;

public sealed class ScraperTests
{
    [Fact]
    public void Endpoint_costruiti_da_host_e_scheme_sono_invariati()
    {
        Assert.Equal(new Uri("https://api.telegram.org"), TelegramOptions.DefaultBaseUrl);
        Assert.Equal(new Uri("https://api.themoviedb.org/3"), TmdbOptions.DefaultBaseUrl);
        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w500/poster.jpg"), TmdbImages.Build("/poster.jpg"));
    }

    [Fact]
    public void Url_immagini_sono_uri_assoluti_https()
    {
        var candidate = new MediaCandidate { Id = "1", Title = "Titolo", PosterPath = "/poster.jpg", BackdropPath = "/backdrop.jpg" };
        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w500/poster.jpg"), candidate.PosterUrl);
        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w780/backdrop.jpg"), candidate.BackdropUrl);

        var details = new MediaDetails { Id = "1", Title = "Titolo", PosterPath = "/poster.jpg", BackdropPath = "/backdrop.jpg" };
        Assert.Equal(candidate.PosterUrl, details.PosterUrl);
        Assert.Equal(candidate.BackdropUrl, details.BackdropUrl);

        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w500/poster.jpg"), TmdbImages.Poster("/poster.jpg"));
        Assert.Equal(new Uri("https://image.tmdb.org/t/p/w780/backdrop.jpg"), TmdbImages.Backdrop("/backdrop.jpg"));
    }

    [Fact]
    public void Il_registro_restituisce_una_copia_dei_provider_registrati()
    {
        using var http = new HttpClient();
        var scraper = new MusicBrainzScraper(http);
        var registry = new ScraperRegistry();
        registry.Register(scraper);

        var first = registry.GetScrapers();
        var second = registry.GetScrapers();

        Assert.Single(first);
        Assert.Same(scraper, first[0]);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task MusicBrainz_mappa_i_risultati_delle_registrazioni()
    {
        var handler = new FakeMusicBrainzHandler();
        using var http = new HttpClient(handler);
        var scraper = new MusicBrainzScraper(http);

        var results = await scraper.SearchAsync(MediaKind.Music, "notte", 2021);

        Assert.Single(results);
        Assert.Equal("Artista — La notte", results[0].Title);
        Assert.Equal(2021, results[0].Year);
        Assert.Equal(MediaKind.Music, results[0].Kind);
        Assert.Equal("abc-123", results[0].Id);
        Assert.Contains("musicbrainz.org/ws/2/recording/", handler.LastUrl!.ToString());
        Assert.Contains("firstreleasedate=2021", handler.LastUrl.ToString());
    }

    [Fact]
    public async Task MusicBrainz_ignora_le_registrazioni_senza_identificativo()
    {
        using var http = new HttpClient(new IncompleteMusicBrainzHandler());
        var scraper = new MusicBrainzScraper(http);

        var results = await scraper.SearchAsync(MediaKind.Music, "senza-id");

        Assert.Empty(results);
    }

    [Fact]
    public void NfoParser_legge_i_campi_del_movie()
    {
        var metadata = NfoParser.Parse("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <movie>
              <title>Il film</title>
              <originaltitle>Original</originaltitle>
              <year>2019</year>
              <rating>7.8</rating>
              <plot>Una trama.</plot>
              <tagline>Uno slogan</tagline>
              <runtime>112</runtime>
              <genre>Drammatico</genre>
              <genre>Thriller</genre>
            </movie>
            """);

        Assert.NotNull(metadata);
        Assert.Equal("Il film", metadata!.Title);
        Assert.Equal(2019, metadata.Year);
        Assert.Equal(7.8, metadata.Rating);
        Assert.Equal(112, metadata.RuntimeMinutes);
        Assert.Equal(["Drammatico", "Thriller"], metadata.Genres);
    }

    [Fact]
    public void NfoParser_restituisce_null_per_xml_rotto()
    {
        Assert.Null(NfoParser.Parse("<movie><title>"));
    }

    [Fact]
    public void LocalArtwork_trova_la_copertina_accanto_al_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var media = Path.Combine(directory, "episodio.mkv");
            File.WriteAllText(media, "x");
            var cover = Path.Combine(directory, "episodio.jpg");
            File.WriteAllText(cover, "x");

            Assert.Equal(cover, LocalArtwork.FindCover(media));
            Assert.Null(LocalArtwork.FindCover(Path.Combine(directory, "altro.mkv")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Tmdb_cerca_film_con_chiave_di_configurazione()
    {
        var handler = new FakeTmdbHandler();
        using var http = new HttpClient(handler);
        var fakeKey = string.Concat("tmdb", "-chiave-di-prova");
        var client = new TmdbClient(http, new TmdbOptions { ApiKey = fakeKey });

        var results = await client.SearchMovieAsync("notte", 2021);

        Assert.Single(results);
        Assert.Equal("FilmNotte", results[0].Title);
        Assert.Contains("api.themoviedb.org/3/search/movie", handler.LastUrl!.ToString());
    }

    private sealed class FakeTmdbHandler : HttpMessageHandler
    {
        public Uri? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request != null)
            {
                LastUrl = request.RequestUri;
                const string json = """{"results":[{"id":1,"title":"FilmNotte","release_date":"2021-05-01"}]}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                });
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
    }

    private sealed class IncompleteMusicBrainzHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string json = """{"recordings":[{"id":"","title":""}]}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeMusicBrainzHandler : HttpMessageHandler
    {
        public Uri? LastUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri;
            var json = """
                {"created":"2020-01-01T00:00:00Z","count":1,"offset":0,
                 "recordings":[
                   {"id":"abc-123","title":"La notte","first-release-date":"2021-05-01",
                    "artist-credit":[{"name":"Artista","joinphrase":""}]}
                 ]}
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }
}
