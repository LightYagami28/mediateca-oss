using System.Net;
using System.Text;
using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Telegram;

namespace Mediateca.Tests;

public sealed class BotUserTests
{
    private const long UnknownUserId = 77;

    private const string UnknownCommand = "/sconosciuto";

    [Fact]
    public void Lo_store_registra_gli_utenti_e_conserva_il_ruolo()
    {
        var directory = NewTempDirectory();
        try
        {
            var store = new BotUserStore(NewDatabase(directory), [10]);

            var admin = store.Touch(10, "capo", DateTimeOffset.Parse("2026-11-15T10:00:00Z"));
            var user = store.Touch(20, "ospite", DateTimeOffset.Parse("2026-11-15T11:00:00Z"));
            Assert.Equal(BotUserRole.Admin, admin.Role);
            Assert.Equal(BotUserRole.User, user.Role);

            Assert.True(store.SetRole(20, BotUserRole.Blocked));
            Assert.False(store.SetRole(99, BotUserRole.Blocked));

            var again = store.Touch(20, "ospite2", DateTimeOffset.Parse("2026-11-15T12:00:00Z"));
            Assert.Equal(BotUserRole.Blocked, again.Role);
            Assert.Equal("ospite2", again.Username);
            Assert.Equal(DateTimeOffset.Parse("2026-11-15T11:00:00Z"), again.FirstSeen);

            Assert.Equal([20L, 10L], store.List().Select(static entry => entry.Id));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public void L_amministratore_iniziale_torna_admin_anche_se_declassato()
    {
        var directory = NewTempDirectory();
        try
        {
            var database = NewDatabase(directory);
            new BotUserStore(database, [10]).Touch(10, null);
            new BotUserStore(database).SetRole(10, BotUserRole.User);

            Assert.Equal(BotUserRole.User, new BotUserStore(database).Touch(10, null).Role);
            Assert.Equal(BotUserRole.Admin, new BotUserStore(database, [10]).Touch(10, null).Role);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task Il_dispatcher_riserva_i_comandi_agli_amministratori_e_ignora_i_bloccati()
    {
        var directory = NewTempDirectory();
        try
        {
            var handler = new RecordingHandler();
            using var http = new HttpClient(handler);
            var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });
            var users = new StoreDirectory(new BotUserStore(NewDatabase(directory), [1]));
            var dispatcher = new BotCommandDispatcher(client, new EmptyCatalog(), new EmptyStatistics(), users);

            await dispatcher.HandleAsync(Message(2, "/users"), CancellationToken.None);
            Assert.Contains("riservato agli amministratori", handler.Bodies[^1]);

            await dispatcher.HandleAsync(Message(1, "/block 2"), CancellationToken.None);
            Assert.Contains("Utente 2: bloccato", handler.Bodies[^1]);

            await dispatcher.HandleAsync(Message(1, "/block 1"), CancellationToken.None);
            Assert.Contains("Non puoi cambiare il tuo ruolo", handler.Bodies[^1]);

            await dispatcher.HandleAsync(Message(1, "/admin 77"), CancellationToken.None);
            Assert.Contains("Utente 77 sconosciuto", handler.Bodies[^1]);

            var before = handler.Bodies.Count;
            await dispatcher.HandleAsync(Message(2, "/stats"), CancellationToken.None);
            Assert.Equal(before, handler.Bodies.Count);

            await dispatcher.HandleAsync(Message(UnknownUserId, UnknownCommand), CancellationToken.None);
            Assert.Equal(before, handler.Bodies.Count);

            await dispatcher.HandleAsync(Message(1, "/unblock 2"), CancellationToken.None);
            await dispatcher.HandleAsync(Message(1, "/users"), CancellationToken.None);
            Assert.Contains("@utente2", handler.Bodies[^1]);
            Assert.Contains("1 @utente1", handler.Bodies[^1]);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static BotUpdate Message(long from, string text) => new()
    {
        UpdateId = 1,
        Message = new BotMessage { MessageId = 1, ChatId = from, FromId = from, FromUsername = $"utente{from}", Text = text },
    };

    private static MediatecaDatabase NewDatabase(string directory)
    {
        var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
        database.Migrate();
        return database;
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "mediateca-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Cleanup(string directory)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private sealed class StoreDirectory(BotUserStore store) : IBotUserDirectory
    {
        public Task<BotUser> TouchAsync(long userId, string? username, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.Touch(userId, username));

        public Task<bool> SetRoleAsync(long userId, BotUserRole role, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.SetRole(userId, role));

        public Task<IReadOnlyList<BotUser>> ListAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.List(limit));
    }

    private sealed class EmptyCatalog : IBotCatalog
    {
        public Task<IReadOnlyList<CatalogHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogHit>>([]);
    }

    private sealed class EmptyStatistics : IBotStatistics
    {
        public Task<BotStats> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BotStats(0, 0, 0));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ok":true,"result":{"message_id":1,"chat":{"id":1},"date":1700000000,"text":"x"}}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
