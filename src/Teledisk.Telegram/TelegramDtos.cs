using System.Text.Json;
using System.Text.Json.Serialization;

namespace Teledisk.Telegram;

public sealed record CatalogHit
{
    public required string Title { get; init; }

    public long SizeBytes { get; init; }

    public required string Kind { get; init; }
}

public sealed record BotStats(long Items, long Bytes, int Channels);

public sealed record InlineKeyboardButton
{
    public required string Text { get; init; }

    public required string CallbackData { get; init; }
}

public sealed record BotCallbackQuery
{
    public required string Id { get; init; }

    public long FromId { get; init; }

    public string? Data { get; init; }

    public long? ChatId { get; init; }
}

public interface IBotCatalog
{
    Task<IReadOnlyList<CatalogHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default);
}

public interface IBotStatistics
{
    Task<BotStats> ReadAsync(CancellationToken cancellationToken = default);
}

public static class TelegramDtos
{
    public sealed class ReplyMarkup
    {
        [JsonPropertyName("inline_keyboard")]
        public List<List<KeyboardButton>>? InlineKeyboard { get; set; }
    }

    public sealed class KeyboardButton
    {
        [JsonPropertyName("text")] public string Text { get; set; } = "";

        [JsonPropertyName("callback_data")] public string CallbackData { get; set; } = "";
    }

    public sealed class InvoicePrice
    {
        [JsonPropertyName("label")] public string Label { get; set; } = "";

        [JsonPropertyName("amount")] public int Amount { get; set; }
    }

    public sealed class WebAppResult
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "article";

        [JsonPropertyName("id")] public string Id { get; set; } = "1";

        [JsonPropertyName("title")] public string Title { get; set; } = "";

        [JsonPropertyName("description")] public string Description { get; set; } = "";

        [JsonPropertyName("url")] public string Url { get; set; } = "";

        [JsonPropertyName("input_message_content")]
        public InputMessageContent? InputMessageContent { get; set; }
    }

    public sealed class InputMessageContent
    {
        [JsonPropertyName("message_text")] public string MessageText { get; set; } = "";
    }
}
