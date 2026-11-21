using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mediateca.Telegram;

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
    public async Task<bool> SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default) =>
        await SendMessageAsync(chatId, text, null, cancellationToken);

    /// <summary>Invia un messaggio di testo con una tastiera inline facoltativa (<c>sendMessage</c>).</summary>
    public async Task<bool> SendMessageAsync(long chatId, string text, IReadOnlyList<InlineKeyboardButton>? buttons, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var body = JsonSerializer.Serialize(new SendMessageDto
        {
            ChatId = chatId,
            Text = text,
            ReplyMarkup = buttons is null or { Count: 0 } ? null : new TelegramDtos.ReplyMarkup
            {
                InlineKeyboard = buttons.Select(button => new List<TelegramDtos.KeyboardButton>
                {
                    new() { Text = button.Text, CallbackData = button.CallbackData },
                }).ToList(),
            },
        }, JsonOptions);
        var sent = await CallAsync<MessageDto>("sendMessage", query: null, jsonBody: body, cancellationToken);
        return sent is not null;
    }

    /// <summary>Conferma una pressione su un bottone inline (<c>answerCallbackQuery</c>).</summary>
    public async Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, string? text, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(callbackQueryId);
        var body = JsonSerializer.Serialize(new AnswerCallbackQueryDto { CallbackQueryId = callbackQueryId, Text = text }, JsonOptions);
        return await CallAsync<bool>("answerCallbackQuery", query: null, jsonBody: body, cancellationToken);
    }

    /// <summary>Crea un link di pagamento in Telegram Stars (<c>createInvoiceLink</c>).</summary>
    public async Task<string?> CreateInvoiceLinkAsync(string title, string description, string payload, int stars, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentOutOfRangeException.ThrowIfLessThan(stars, 1);

        var body = JsonSerializer.Serialize(new CreateInvoiceLinkDto
        {
            Title = title,
            Description = description,
            Payload = payload,
            Currency = "XTR",
            Prices = [new TelegramDtos.InvoicePrice { Label = title, Amount = stars }],
        }, JsonOptions);
        return await CallAsync<string>("createInvoiceLink", query: null, jsonBody: body, cancellationToken);
    }

    /// <summary>Risponde a una query di MiniApp con un articolo cliccabile (<c>answerWebAppQuery</c>).</summary>
    public async Task<bool> AnswerWebAppQueryAsync(string webAppQueryId, string title, string description, string url, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(webAppQueryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var body = JsonSerializer.Serialize(new AnswerWebAppQueryDto
        {
            WebAppQueryId = webAppQueryId,
            Result = new TelegramDtos.WebAppResult
            {
                Title = title,
                Description = description,
                Url = url,
                InputMessageContent = new TelegramDtos.InputMessageContent { MessageText = url },
            },
        }, JsonOptions);
        return await CallAsync<bool>("answerWebAppQuery", query: null, jsonBody: body, cancellationToken);
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
                Message = ToBotMessage(update.Message),
            });
        }

        return result;
    }

    /// <summary>Imposta il bottone del menu della chat, tipicamente verso la MiniApp (<c>setChatMenuButton</c>).</summary>
    public async Task<bool> SetChatMenuButtonAsync(string text, string webAppUrl, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return false;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(webAppUrl);
        var body = JsonSerializer.Serialize(new SetChatMenuButtonDto
        {
            MenuButton = new MenuButtonWebAppDto
            {
                Text = text,
                WebApp = new WebAppInfoDto { Url = webAppUrl },
            },
        }, JsonOptions);
        return await CallAsync<bool>("setChatMenuButton", query: null, jsonBody: body, cancellationToken);
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
            throw new TelegramApiException(method, envelope.ErrorCode ?? ErrorCodeOf(response), envelope.Description)
            {
                RetryAfterSeconds = envelope.Parameters?.RetryAfter,
            };
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
        [JsonPropertyName("parameters")] public ResponseParametersDto? Parameters { get; set; }
    }

    private static BotMessage? ToBotMessage(MessageDto? message)
    {
        if (message is null)
        {
            return null;
        }

        DateTimeOffset? date = message.Date > 0
            ? DateTimeOffset.FromUnixTimeSeconds(message.Date)
            : null;

        return new BotMessage
        {
            MessageId = message.MessageId,
            ChatId = message.Chat?.Id ?? 0,
            FromId = message.From?.Id,
            FromUsername = message.From?.Username,
            Text = message.Text,
            Date = date,
        };
    }

    private sealed class ResponseParametersDto
    {
        [JsonPropertyName("retry_after")] public int? RetryAfter { get; set; }
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
        [JsonPropertyName("reply_markup")] public TelegramDtos.ReplyMarkup? ReplyMarkup { get; set; }
    }

    private sealed class AnswerCallbackQueryDto
    {
        [JsonPropertyName("callback_query_id")] public string CallbackQueryId { get; set; } = "";
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class CreateInvoiceLinkDto
    {
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("payload")] public string Payload { get; set; } = "";
        [JsonPropertyName("provider_token")] public string ProviderToken { get; set; } = "";
        [JsonPropertyName("currency")] public string Currency { get; set; } = "";
        [JsonPropertyName("prices")] public List<TelegramDtos.InvoicePrice> Prices { get; set; } = [];
    }

    private sealed class AnswerWebAppQueryDto
    {
        [JsonPropertyName("web_app_query_id")] public string WebAppQueryId { get; set; } = "";
        [JsonPropertyName("result")] public TelegramDtos.WebAppResult? Result { get; set; }
    }

    private sealed class SetChatMenuButtonDto
    {
        [JsonPropertyName("menu_button")] public MenuButtonWebAppDto? MenuButton { get; set; }
    }

    private sealed class MenuButtonWebAppDto
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "web_app";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
        [JsonPropertyName("web_app")] public WebAppInfoDto? WebApp { get; set; }
    }

    private sealed class WebAppInfoDto
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }
}
