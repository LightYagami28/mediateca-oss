using Mediateca.Core.Domain;

namespace Mediateca.Tests;

public sealed class MediaNameParserTests
{
    [Theory]
    [InlineData("The.Matrix.1999.1080p.BluRay.x264.mkv", "The Matrix", 1999)]
    [InlineData("Il Padrino (1972) [ITA-ENG].mp4", "Il Padrino", 1972)]
    [InlineData("2001 A Space Odyssey 1968.mkv", "2001 A Space Odyssey", 1968)]
    [InlineData("[Gruppo] Blade_Runner_2049_2017_2160p.mkv", "Blade Runner 2049", 2017)]
    [InlineData("Arrival.mkv", "Arrival", null)]
    public void Riconosce_titolo_e_anno_dei_film(string name, string title, int? year)
    {
        var parsed = MediaNameParser.Parse(name);

        Assert.Equal(MediaKind.Movie, parsed.Kind);
        Assert.Equal(title, parsed.Title);
        Assert.Equal(year, parsed.Year);
        Assert.Null(parsed.Season);
        Assert.Null(parsed.Episode);
    }

    [Theory]
    [InlineData("Breaking.Bad.S02E05.720p.HDTV.mkv", "Breaking Bad", 2, 5)]
    [InlineData("serie/Dark s1e10 ITA.mp4", "Dark", 1, 10)]
    [InlineData("Lost - 3x07 - Episodio.avi", "Lost", 3, 7)]
    public void Riconosce_stagione_ed_episodio(string name, string title, int season, int episode)
    {
        var parsed = MediaNameParser.Parse(name);

        Assert.Equal(MediaKind.Episode, parsed.Kind);
        Assert.Equal(title, parsed.Title);
        Assert.Equal(season, parsed.Season);
        Assert.Equal(episode, parsed.Episode);
    }

    [Theory]
    [InlineData("copertina.jpg", MediaKind.Image)]
    [InlineData("manuale.pdf", MediaKind.Document)]
    [InlineData("archivio.7z", MediaKind.Archive)]
    [InlineData("senza-estensione", MediaKind.Unknown)]
    public void Per_i_file_non_multimediali_restituisce_solo_il_tipo(string name, MediaKind kind)
    {
        var parsed = MediaNameParser.Parse(name);

        Assert.Equal(kind, parsed.Kind);
        Assert.Null(parsed.Title);
        Assert.Null(parsed.Year);
    }

    [Fact]
    public void Libri_e_musica_tengono_il_titolo()
    {
        Assert.Equal(new ParsedMediaName(MediaKind.Book, "Il nome della rosa", null, null, null), MediaNameParser.Parse("Il_nome_della_rosa.epub"));
        Assert.Equal(new ParsedMediaName(MediaKind.Music, "Artista - Brano", 2020, null, null), MediaNameParser.Parse("Artista - Brano (2020).flac"));
    }
}
