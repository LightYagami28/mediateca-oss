using System.Globalization;
using System.Text;
using Mediateca.Core.Domain;

namespace Mediateca.Telegram;

public sealed class BotCommandDispatcher
{
    private readonly TelegramBotClient _client;
    private readonly IBotCatalog _catalog;
    private readonly IBotStatistics _statistics;
    private readonly IBotUserDirectory? _users;

    public BotCommandDispatcher(TelegramBotClient client, IBotCatalog catalog, IBotStatistics statistics, IBotUserDirectory? users = null)
    {
        _client = client;
        _catalog = catalog;
        _statistics = statistics;
        _users = users;
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

        BotUser? sender = null;
        if (_users is not null && update.Message.FromId is { } fromId)
        {
            sender = await _users.TouchAsync(fromId, update.Message.FromUsername, cancellationToken).ConfigureAwait(false);
            if (sender.Role == BotUserRole.Blocked)
            {
                return;
            }
        }

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

            case "/users":
            case "/block":
            case "/unblock":
            case "/admin":
                await HandleAdminAsync(chatId, sender, command, argument, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleAdminAsync(long chatId, BotUser? sender, string command, string argument, CancellationToken cancellationToken)
    {
        if (_users is null || sender is not { Role: BotUserRole.Admin })
        {
            await _client.SendMessageAsync(chatId, "Comando riservato agli amministratori.", cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (command == "/users")
        {
            await SendUsersAsync(chatId, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!long.TryParse(argument, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var target))
        {
            await _client.SendMessageAsync(chatId, $"Uso: {command} <id utente>", cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        if (target == sender.Id)
        {
            await _client.SendMessageAsync(chatId, "Non puoi cambiare il tuo ruolo.", cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var role = command switch
        {
            "/block" => BotUserRole.Blocked,
            "/admin" => BotUserRole.Admin,
            _ => BotUserRole.User,
        };
        var id = target.ToString(CultureInfo.InvariantCulture);
        var changed = await _users.SetRoleAsync(target, role, cancellationToken).ConfigureAwait(false);
        var reply = changed ? $"Utente {id}: {RoleLabel(role)}." : $"Utente {id} sconosciuto.";
        await _client.SendMessageAsync(chatId, reply, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task SendUsersAsync(long chatId, CancellationToken cancellationToken)
    {
        var users = await _users!.ListAsync(20, cancellationToken).ConfigureAwait(false);
        var builder = new StringBuilder();
        foreach (var user in users)
        {
            builder.Append("• ").Append(user.Id.ToString(CultureInfo.InvariantCulture));
            if (user.Username is { Length: > 0 } username)
            {
                builder.Append(" @").Append(username);
            }

            builder.Append(" — ").Append(RoleLabel(user.Role)).Append('\n');
        }

        var text = builder.Length == 0 ? "Nessun utente." : builder.ToString().TrimEnd();
        await _client.SendMessageAsync(chatId, text, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string RoleLabel(BotUserRole role) => role switch
    {
        BotUserRole.Admin => "amministratore",
        BotUserRole.Blocked => "bloccato",
        _ => "utente",
    };

    private static string HelpText =>
        "Mediateca — nessuna funzione bloccata.\n" +
        "/catalog — prime voci del catalogo\n" +
        "/search <testo> — cerca nel catalogo\n" +
        "/stats — statistiche della libreria\n" +
        "/users, /block <id>, /unblock <id>, /admin <id> — gestione utenti (amministratori)";

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
