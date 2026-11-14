namespace Mediateca.Core.Domain;

/// <summary>Tipo di contenuto gestito dalla libreria.</summary>
public enum MediaKind
{
    Unknown = 0,
    Movie,
    Series,
    Episode,
    Music,
    Book,
    Document,
    Image,
    Archive,
}

/// <summary>
/// Un file presente in un canale Telegram, con i metadati che Mediateca usa per catalogarlo.
/// </summary>
/// <remarks>
/// La riga è autosufficiente: la posizione del file è la coppia canale/messaggio, il resto è
/// catalogo. Nessun percorso locale è obbligatorio, perché il contenuto vive su Telegram.
/// </remarks>
public sealed record MediaItem
{
    /// <summary>Identificativo interno della riga.</summary>
    public long Id { get; init; }

    /// <summary>Canale sorgente.</summary>
    public long ChannelId { get; init; }

    /// <summary>Identificativo del messaggio nel canale.</summary>
    public int MessageId { get; init; }

    /// <summary>Cartella logica all'interno della libreria.</summary>
    public long? ParentId { get; init; }

    /// <summary>Nome del file come mostrato nella UI.</summary>
    public required string Name { get; init; }

    /// <summary>Tipo di contenuto dedotto dall'estensione e dai metadati.</summary>
    public MediaKind Kind { get; init; } = MediaKind.Unknown;

    /// <summary>Dimensione in byte, quando nota.</summary>
    public long Size { get; init; }

    /// <summary>Durata in secondi per audio/video, quando nota.</summary>
    public int? DurationSeconds { get; init; }

    /// <summary>Larghezza e altezza per immagini/video, quando note.</summary>
    public int? Width { get; init; }

    /// <summary>Altezza in pixel, quando nota.</summary>
    public int? Height { get; init; }

    /// <summary>Titolo leggibile, quando i metadati lo forniscono.</summary>
    public string? Title { get; init; }

    /// <summary>Anno di uscita, quando noto.</summary>
    public int? Year { get; init; }

    /// <summary>Stagione per serie TV.</summary>
    public int? Season { get; init; }

    /// <summary>Episodio per serie TV.</summary>
    public int? Episode { get; init; }

    /// <summary>Identificativo esterno del provider (per esempio TMDB).</summary>
    public string? ExternalId { get; init; }

    /// <summary>Riferimento alla copertina, relativo alla libreria.</summary>
    public string? CoverPath { get; init; }

    /// <summary>Ultima modifica nota, in UTC.</summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Estensione normalizzata, senza punto, in minuscolo.</summary>
    public string Extension => Path.GetExtension(Name).TrimStart('.').ToLowerInvariant();

    /// <summary>True quando il file è riproducibile in streaming senza conversione.</summary>
    public bool IsStreamable => Kind is MediaKind.Movie or MediaKind.Series or MediaKind.Episode or MediaKind.Music;
}
