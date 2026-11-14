using System.Net;
using Teledisk.Core.Domain;
using Teledisk.Media;

namespace Teledisk.Tests;

public sealed class ScraperTests
{
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
        Assert.Contains("musicbrainz.org/ws/2/recording/", handler.LastUrl);
        Assert.Contains("firstreleasedate=2021", handler.LastUrl);
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
        var directory = Path.Combine(Path.GetTempPath(), "teledisk-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FakeMusicBrainzHandler : HttpMessageHandler
    {
        public string LastUrl { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri!.ToString();
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
