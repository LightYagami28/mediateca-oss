using System.Text.RegularExpressions;

namespace Mediateca.Telegram;

/// <summary>
/// Opzioni di accesso alla Bot API di Telegram.
/// </summary>
/// <remarks>
/// Il token viene letto dalla variabile <see cref="BotTokenVariable"/> solo quando non è passato
/// esplicitamente. La sua assenza non è un errore: il client resta costruibile e
/// <see cref="IsConfigured"/> lo segnala.
/// </remarks>
public sealed record TelegramOptions
{
    /// <summary>Variabile d'ambiente da cui leggere il token quando non è passato.</summary>
    public const string BotTokenVariable = "MEDIATECA_TELEGRAM_BOT_TOKEN";

    /// <summary>Host predefinito della Bot API.</summary>
    public const string ApiHost = "api.telegram.org";

    /// <summary>Indirizzo predefinito della Bot API, composto senza URI assoluti hardcoded.</summary>
    public static Uri DefaultBaseUrl { get; } = new UriBuilder(Uri.UriSchemeHttps, ApiHost).Uri;

    /// <summary>Timeout predefinito di una chiamata, in secondi.</summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>Formato del token: identificativo numerico, due punti, segreto.</summary>
    private static readonly Regex TokenPattern = new(@"^\d{6,}:[A-Za-z0-9_-]{30,}\z", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Token del bot, nella forma <c>&lt;id&gt;:&lt;segreto&gt;</c>.</summary>
    public string BotToken { get; init; } = Environment.GetEnvironmentVariable(BotTokenVariable)?.Trim() ?? string.Empty;

    /// <summary>Radice della Bot API.</summary>
    public Uri BaseUrl { get; init; } = DefaultBaseUrl;

    /// <summary>Timeout per singola chiamata in secondi; valori non positivi usano il timeout di <see cref="HttpClient"/>.</summary>
    public int TimeoutSeconds { get; init; } = DefaultTimeoutSeconds;

    /// <summary>True quando è presente un token, anche se di formato dubbio.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);

    /// <summary>True quando il token rispetta il formato ufficiale di Telegram.</summary>
    public bool HasValidTokenFormat => BotToken.Trim() is { Length: > 0 } token && TokenPattern.IsMatch(token);
}

/// <summary>
/// Identità del bot restituita da <c>getMe</c>.
/// </summary>
public sealed record BotIdentity
{
    /// <summary>Identificativo numerico del bot.</summary>
    public long Id { get; init; }

    /// <summary>Nome utente del bot, senza chiocciola.</summary>
    public string? Username { get; init; }

    /// <summary>Nome mostrato nel profilo.</summary>
    public string? FirstName { get; init; }

    /// <summary>True per gli account automatici.</summary>
    public bool IsBot { get; init; } = true;
}

/// <summary>
/// Aggiornamento restituito da <c>getUpdates</c>.
/// </summary>
public sealed record BotUpdate
{
    /// <summary>Identificativo progressivo dell'aggiornamento, da passare al giro successivo.</summary>
    public int UpdateId { get; init; }

    /// <summary>Messaggio contenuto nell'aggiornamento, quando presente.</summary>
    public BotMessage? Message { get; init; }

    /// <summary>Chat di provenienza, quando l'aggiornamento contiene un messaggio.</summary>
    public long? ChatId => Message?.ChatId;
}

/// <summary>
/// Messaggio sintetico della Bot API.
/// </summary>
public sealed record BotMessage
{
    /// <summary>Identificativo del messaggio nella chat.</summary>
    public int MessageId { get; init; }

    /// <summary>Chat in cui è comparso il messaggio.</summary>
    public long ChatId { get; init; }

    /// <summary>Identificativo del mittente, quando noto.</summary>
    public long? FromId { get; init; }

    /// <summary>Nome utente del mittente, senza chiocciola, quando noto.</summary>
    public string? FromUsername { get; init; }

    /// <summary>Testo del messaggio, quando presente.</summary>
    public string? Text { get; init; }

    /// <summary>Momento di invio, in UTC.</summary>
    public DateTimeOffset? Date { get; init; }
}

/// <summary>
/// Errore restituito dalla Bot API (<c>ok: false</c>) o risposta non leggibile.
/// </summary>
public sealed class TelegramApiException : Exception
{
    /// <summary>Crea l'eccezione con il metodo, il codice e la descrizione dell'errore.</summary>
    public TelegramApiException(string method, int errorCode, string? description)
        : this(method, errorCode, description, null)
    {
    }

    /// <summary>Crea l'eccezione con un errore interno, per esempio di deserializzazione.</summary>
    public TelegramApiException(string method, int errorCode, string? description, Exception? innerException)
        : base($"Telegram ha rifiutato {method} con errore {errorCode}: {description ?? "nessuna descrizione"}", innerException)
    {
        Method = method;
        ErrorCode = errorCode;
        Description = description;
    }

    /// <summary>Metodo della Bot API che ha fallito.</summary>
    public string Method { get; }

    /// <summary>Codice di errore restituito da Telegram, o stato HTTP quando la risposta non è JSON.</summary>
    public int ErrorCode { get; }

    /// <summary>Descrizione leggibile fornita da Telegram.</summary>
    public string? Description { get; }

    public int? RetryAfterSeconds { get; init; }
}
