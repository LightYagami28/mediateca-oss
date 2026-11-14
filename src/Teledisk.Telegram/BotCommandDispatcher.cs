using System.Text;

namespace Teledisk.Telegram;

public sealed class BotCommandDispatcher
{
    private readonly TelegramBotClient _client;
    private readonly IBotCatalog _catalog;
    private readonly IBotStatistics _statistics;

    public BotCommandDispatcher(TelegramBotClient client, IBotCatalog catalog, IBotStatistics statistics)
    {
        _client = client;
        _catalog = catalog;
        _statistics = statistics;
    }

    public async Task HandleAsync(BotUpdate update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } text })
        {
            return;
        }

        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts.Length > 0 ? parts[0] : string.Empty;
        var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        var chatId = update.Message.ChatId;

        switch (command)
        {
            case "/start":
            case "/help":
                await _client.SendMessageAsync(chatId, HelpText, cancellationToken: cancellationToken).ConfigureAwait(false);
                break;

            case "/catalog":
                await SendCatalogAsync(chatId, string.Empty, cancellationToken).ConfigureAwait(false);
                break;

            case "/search":
                await SendCatalogAsync(chatId, argument, cancellationToken).ConfigureAwait(false);
                break;

            case "/stats":
                await SendStatsAsync(chatId, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private static string HelpText =>
        "Teledisk Community — nessuna funzione bloccata.\n" +
        "/catalog — prime voci del catalogo\n" +
        "/search <testo> — cerca nel catalogo\n" +
        "/stats — statistiche della libreria";

    private async Task SendCatalogAsync(long chatId, string query, CancellationToken cancellationToken)
    {
        var hits = await _catalog.SearchAsync(query, 10, cancellationToken).ConfigureAwait(false);
        if (hits.Count == 0)
        {
            await _client.SendMessageAsync(chatId, "Nessun elemento trovato.", cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var builder = new StringBuilder();
        foreach (var hit in hits)
        {
            builder.Append("• ").Append(hit.Title)
                .Append(" (").Append(FormatBytes(hit.SizeBytes)).Append(", ").Append(hit.Kind).Append(")\n");
        }

        await _client.SendMessageAsync(chatId, builder.ToString().TrimEnd(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task SendStatsAsync(long chatId, CancellationToken cancellationToken)
    {
        var stats = await _statistics.ReadAsync(cancellationToken).ConfigureAwait(false);
        var text = $"{stats.Items:N0} elementi · {FormatBytes(stats.Bytes)} · {stats.Channels} canali";
        await _client.SendMessageAsync(chatId, text, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : $"{value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} {units[unit]}";
    }
}
