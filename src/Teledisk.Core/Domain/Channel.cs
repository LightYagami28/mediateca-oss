namespace Teledisk.Core.Domain;

/// <summary>Canale o gruppo Telegram usato come sorgente della libreria.</summary>
public sealed record Channel
{
    /// <summary>Identificativo del peer Telegram, già normalizzato senza marcatori pubblici.</summary>
    public required long Id { get; init; }

    /// <summary>Identificativo di accesso, quando il peer ne espone uno.</summary>
    public long? AccessHash { get; init; }

    /// <summary>Titolo del canale.</summary>
    public required string Title { get; init; }

    /// <summary>Username pubblico, senza chiocciola, quando presente.</summary>
    public string? Username { get; init; }

    /// <summary>Tipo di peer osservato.</summary>
    public ChannelKind Kind { get; init; } = ChannelKind.Channel;

    /// <summary>True quando il canale è usato come sorgente della libreria.</summary>
    public bool IsSource { get; init; }

    /// <summary>Ultima scansione completata, in UTC.</summary>
    public DateTimeOffset? LastScanAt { get; init; }

    /// <summary>Numero di file catalogati dall'ultima scansione.</summary>
    public int ItemCount { get; init; }

    // Messaggio più alto osservato, usato per ripartire in modo incrementale.
    public int LastMessageId { get; init; }
}

/// <summary>Tipo di peer Telegram.</summary>
public enum ChannelKind
{
    Channel = 0,
    Supergroup,
    Group,
    Chat,
    Saved,
}
