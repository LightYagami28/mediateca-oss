using Teledisk.Core.Domain;

namespace Teledisk.Ui.Models;

public static class KindStyle
{
    public static string Label(MediaKind kind) => kind switch
    {
        MediaKind.Movie => "Film",
        MediaKind.Series => "Serie",
        MediaKind.Episode => "Episodio",
        MediaKind.Music => "Musica",
        MediaKind.Book => "Libro",
        MediaKind.Document => "Documento",
        MediaKind.Image => "Immagine",
        MediaKind.Archive => "Archivio",
        _ => "Altro",
    };

    public static string Color(MediaKind kind) => kind switch
    {
        MediaKind.Movie => "#F29937",
        MediaKind.Series => "#6C8BFF",
        MediaKind.Episode => "#6C8BFF",
        MediaKind.Music => "#E86AF0",
        MediaKind.Book => "#3FD08A",
        MediaKind.Document => "#F2B84B",
        MediaKind.Image => "#35D0BA",
        _ => "#8A94A6",
    };
}
