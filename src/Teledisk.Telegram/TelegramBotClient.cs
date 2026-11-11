using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.Telegram;

/// <summary>
/// Client essenziale della Bot API di Telegram (HTTPS, nessuna dipendenza MTProto).
/// </summary>
/// <remarks>
/// Copre le tre chiamate che servono alla console e ai test: identità del bot, invio di un
/// messaggio e lettura degli aggiornamenti. Il token non è obbligatorio alla costruzione: senza
/// token i metodi restituiscono il valore neutro (<c>null</c>, <c>false</c>, lista vuota) senza
/// toccare la rete. Un token presente ma malformato viene invece segnalato subito.
/// </remarks>
public sealed class TelegramBotClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly TelegramOptions _options;
    private readonly string _token;

    /// <summary>Crea il client sull'<see cref="HttpClient"/> iniettato.</summary>
    /// <param name="httpClient">Client HTTP condiviso dal chiamante.</param>
    /// <param name="options">Opzioni di accesso; con <c>null</c> valgono i valori predefiniti.</param>
    /// <exception cref="ArgumentException">Se il token è presente ma non rispetta il formato di Telegram.</exception>
    public TelegramBotClient(HttpClient httpClient, TelegramOptions? options = null)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new TelegramOptions();
        _token = _options.BotToken.Trim();

        if (_token.Length > 0 && !_options.HasValidTokenFormat)
        {
            throw new ArgumentException(
                $"Il token del bot non rispetta il formato '<id>:<segreto>' atteso da Telegram (vedi {TelegramOptions.BotTokenVariable}).",
                nameof(options));
        }
    }

    /// <summary>True quando il client ha un token utilizzabile.</summary>
    public bool IsConfigured => _token.Length > 0;

    /// <summary>Opzioni con cui il client è stato costruito.</summary>
    public TelegramOptions Options => _options;

    /// <summary>Legge l'identità del bot (<c>getMe</c>).</summary>
    /// <returns>L'identità, oppure <c>null</c> se il client non è configurato o la risposta è vuota.</returns>
    public async Task<BotIdentity?> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var user = await CallAsync<UserDto>("getMe", query: null, jsonBody: null, cancellationToken);
        return user is null || user.Id == 0
            ? null
            : new BotIdentity { Id = user.Id, Username = user.Username, FirstName = user.FirstName, IsBot = user.IsBot };
    }

    /// <summary>Invia un messaggio di testo (<c>sendMessage</c>).</summary>
    /// <param name="chatId">Identificativo della chat di destinazione.</param>
    /// <param name="text">Testo da inviare.</param>
    /// <returns>True se Telegram ha accettato il messaggio.</returns>
    public async Task<bool> SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var body = JsonSerializer.Serialize(new SendMessageDto { ChatId = chatId, Text = text }, JsonOptions);
        var sent = await CallAsync<MessageDto>("sendMessage", query: null, jsonBody: body, cancellationToken);
        return sent is not null;
    }

    /// <summary>Legge gli aggiornamenti in attesa (<c>getUpdates</c>).</summary>
    /// <param name="offset">Primo <c>update_id</c> da restituire; i precedenti vengono confermati.</param>
    /// <param name="limit">Numero massimo di aggiornamenti, da 1 a 100.</param>
    /// <returns>Gli aggiornamenti, o lista vuota se il client non è configurato.</returns>
    public async Task<IReadOnlyList<BotUpdate>> GetUpdatesAsync(int offset = 0, int limit = 100, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return [];
        }

        var safeOffset = Math.Max(0, offset);
        var safeLimit = Math.Clamp(limit, 1, 100);
        var updates = await CallAsync<List<UpdateDto>>("getUpdates", $"?offset={safeOffset}&limit={safeLimit}", jsonBody: null, cancellationToken);
        if (updates is null)
        {
            return [];
        }

        var result = new List<BotUpdate>(updates.Count);
        foreach (var update in updates)
        {
            result.Add(new BotUpdate
            {
                UpdateId = update.UpdateId,
                Message = update.Message is null ? null : new BotMessage
                {
                    MessageId = update.Message.MessageId,
                    ChatId = update.Message.Chat?.Id ?? 0,
                    FromId = update.Message.From?.Id,
                    FromUsername = update.Message.From?.Username,
                    Text = update.Message.Text,
                    Date = update.Message.Date > 0 ? DateTimeOffset.FromUnixTimeSeconds(update.Message.Date) : null,
                },
            });
        }

        return result;
    }

    /// <summary>Chiama un metodo della Bot API e restituisce il campo <c>result</c>.</summary>
    /// <exception cref="TelegramApiException">Se Telegram risponde <c>ok: false</c> o con un payload non leggibile.</exception>
    /// <exception cref="TimeoutException">Se la chiamata supera <see cref="TelegramOptions.TimeoutSeconds"/>.</exception>
    private async Task<T?> CallAsync<T>(string method, string? query, string? jsonBody, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(jsonBody is null ? HttpMethod.Get : HttpMethod.Post, BuildUrl(method, query));
        if (jsonBody is not null)
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        using var response = await SendAsync(request, method, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        ApiEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions);
        }
        catch (JsonException exc)
        {
            throw new TelegramApiException(method, ErrorCodeOf(response), "risposta non leggibile", exc);
        }

        if (envelope is null)
        {
            throw new TelegramApiException(method, ErrorCodeOf(response), "risposta vuota");
        }

        if (!envelope.Ok)
        {
            throw new TelegramApiException(method, envelope.ErrorCode ?? ErrorCodeOf(response), envelope.Description);
        }

        return envelope.Result;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string method, CancellationToken cancellationToken)
    {
        if (_options.TimeoutSeconds <= 0)
        {
            return await _http.SendAsync(request, cancellationToken);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            return await _http.SendAsync(request, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Telegram non ha risposto a {method} entro {_options.TimeoutSeconds} secondi.");
        }
    }

    private string BuildUrl(string method, string? query) =>
        $"{_options.BaseUrl.TrimEnd('/')}/bot{_token}/{method}{query}";

    private static int ErrorCodeOf(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        return status >= 400 ? status : 0;
    }

    private sealed class ApiEnvelope<T>
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error_code")] public int? ErrorCode { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("result")] public T? Result { get; set; }
    }

    private sealed class UserDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("is_bot")] public bool IsBot { get; set; }
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("username")] public string? Username { get; set; }
    }

    private sealed class MessageDto
    {
        [JsonPropertyName("message_id")] public int MessageId { get; set; }
        [JsonPropertyName("from")] public UserDto? From { get; set; }
        [JsonPropertyName("chat")] public ChatDto? Chat { get; set; }
        [JsonPropertyName("date")] public long Date { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class ChatDto
    {
        [JsonPropertyName("id")] public long Id { get; set; }
    }

    private sealed class UpdateDto
    {
        [JsonPropertyName("update_id")] public int UpdateId { get; set; }
        [JsonPropertyName("message")] public MessageDto? Message { get; set; }
    }

    private sealed class SendMessageDto
    {
        [JsonPropertyName("chat_id")] public long ChatId { get; set; }
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }
}
