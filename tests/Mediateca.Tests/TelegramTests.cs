using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Mediateca.Telegram;

namespace Mediateca.Tests;

public sealed class TelegramTests
{
    [Fact]
    public async Task SendMessage_con_bottoni_include_la_tastiera_inline()
    {
        var handler = new FakeTelegramHandler();
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });

        var sent = await client.SendMessageAsync(42, "scegli", [new InlineKeyboardButton { Text = "Scarica", CallbackData = "dl:1" }]);

        Assert.True(sent);
        var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(42, body.RootElement.GetProperty("chat_id").GetInt64());
        var keyboard = body.RootElement.GetProperty("reply_markup").GetProperty("inline_keyboard")[0][0];
        Assert.Equal("Scarica", keyboard.GetProperty("text").GetString());
        Assert.Equal("dl:1", keyboard.GetProperty("callback_data").GetString());
    }

    [Fact]
    public async Task CreateInvoiceLink_chiede_il_pagamento_in_stars()
    {
        var handler = new FakeTelegramHandler();
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });

        var link = await client.CreateInvoiceLinkAsync("Abbonamento", "Un mese di streaming", "abb-1", 250);

        Assert.Equal("https://t.me/$invoicelink", link);
        var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("XTR", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal(250, body.RootElement.GetProperty("prices")[0].GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task AnswerWebAppQuery_invia_l_articolo_della_miniapp()
    {
        var handler = new FakeTelegramHandler();
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });

        var sent = await client.AnswerWebAppQueryAsync("qid-1", "Film", "Descrizione", "https://example.com/film");

        Assert.True(sent);
        var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("qid-1", body.RootElement.GetProperty("web_app_query_id").GetString());
        Assert.Equal("article", body.RootElement.GetProperty("result").GetProperty("type").GetString());
    }

    [Fact]
    public async Task Il_dispatcher_risponde_ai_comandi_con_il_catalogo()
    {
        var handler = new FakeTelegramHandler();
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });
        var dispatcher = new BotCommandDispatcher(client, new FakeCatalog(), new FakeStatistics());

        await dispatcher.HandleAsync(new BotUpdate
        {
            UpdateId = 1,
            Message = new BotMessage { MessageId = 1, ChatId = 42, Text = "/search luna" },
        }, CancellationToken.None);

        Assert.Contains("Film 1", handler.LastBody);
        Assert.Contains("Film 2", handler.LastBody);
    }

    [Fact]
    public async Task Il_polling_avanza_l_offset_e_ferma_sulla_cancellazione()
    {
        var handler = new FakeTelegramHandler();
        handler.EnqueueUpdates("""
            {"ok":true,"result":[
              {"update_id":104,"message":{"message_id":1,"chat":{"id":42},"date":1700000000,"text":"/start"}},
              {"update_id":105,"message":{"message_id":2,"chat":{"id":42},"date":1700000000,"text":"/stats"}}
            ]}
            """);
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });
        var polling = new BotPollingService(client, NullLogger.Instance);
        var handled = new List<BotUpdate>();

        using var cancellation = new CancellationTokenSource();
        handler.CancelAfterGetUpdates = (count) =>
        {
            if (count >= 2)
            {
                cancellation.Cancel();
            }
        };
        var run = polling.PollAsync((update, _) =>
        {
            lock (handled)
            {
                handled.Add(update);
            }

            return Task.CompletedTask;
        }, cancellation.Token);

        await run.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);

        Assert.True(run.IsCompleted);
        Assert.Equal(2, handled.Count);
        var updates = handler.Requests.Where(request => request.RequestUri!.AbsolutePath.Contains("getUpdates")).ToList();
        Assert.True(updates.Count >= 2);
        var last = updates[^1].RequestUri!.Query;
        Assert.Contains("offset=106", last);
    }

    private sealed class FakeTelegramHandler : HttpMessageHandler
    {
        private readonly Queue<string> _updates = new();

        public List<HttpRequestMessage> Requests { get; } = [];

        public Action<int>? CancelAfterGetUpdates { get; set; }

        public string? LastBody { get; private set; }

        public void EnqueueUpdates(string body) => _updates.Enqueue(body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            LastBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("getUpdates"))
            {
                CancelAfterGetUpdates?.Invoke(Requests.Count(candidate => candidate.RequestUri!.AbsolutePath.Contains("getUpdates")));
            }

            var json = path switch
            {
                _ when path.Contains("getUpdates") => _updates.Count > 0 ? _updates.Dequeue() : """{"ok":true,"result":[]}""",
                _ when path.Contains("getMe") => """{"ok":true,"result":{"id":123456789,"is_bot":true,"first_name":"T","username":"mediatecabot"}}""",
                _ when path.Contains("createInvoiceLink") => """{"ok":true,"result":"https://t.me/$invoicelink"}""",
                _ when path.Contains("answerWebAppQuery") => """{"ok":true,"result":true}""",
                _ when path.Contains("setChatMenuButton") => """{"ok":true,"result":true}""",
                _ => """{"ok":true,"result":{"message_id":1,"from":{"id":1,"is_bot":true},"chat":{"id":42},"date":1700000000,"text":"x"}}""",
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Telegraph_crea_una_pagina_e_restituisce_l_url()
    {
        var handler = new TelegraphFakeHandler();
        using var http = new HttpClient(handler);
        var client = new TelegraphClient(http);

        var url = await client.CreatePageAsync("Catalogo", "Elenco");

        Assert.Equal("https://telegra.ph/Catalogo-01-01", url);
        Assert.Contains("telegra.ph/createPage", handler.LastUrl);
        Assert.Contains("Mediateca", handler.LastBody);
    }

    [Fact]
    public async Task SetChatMenuButton_invia_il_bottone_della_miniapp()
    {
        var handler = new FakeTelegramHandler();
        using var http = new HttpClient(handler);
        var client = new TelegramBotClient(http, new TelegramOptions { BotToken = "123456789:AAA" + new string('x', 30) });

        var set = await client.SetChatMenuButtonAsync("Apri", "https://example.com/miniapp");

        Assert.True(set);
        var body = JsonDocument.Parse(handler.LastBody!);
        var button = body.RootElement.GetProperty("menu_button");
        Assert.Equal("web_app", button.GetProperty("type").GetString());
        Assert.Equal("https://example.com/miniapp", button.GetProperty("web_app").GetProperty("url").GetString());
    }

    private sealed class TelegraphFakeHandler : HttpMessageHandler
    {
        public string LastUrl { get; private set; } = "";

        public string LastBody { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri!.ToString();
            LastBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "";
            var json = """{"ok":true,"result":{"path":"Catalogo-01-01","url":"https://telegra.ph/Catalogo-01-01"}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeCatalog : IBotCatalog
    {
        public Task<IReadOnlyList<CatalogHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogHit>>(
            [
                new CatalogHit { Title = "Film 1", SizeBytes = 1000, Kind = "Film" },
                new CatalogHit { Title = "Film 2", SizeBytes = 2000, Kind = "Serie" },
            ]);
    }

    private sealed class FakeStatistics : IBotStatistics
    {
        public Task<BotStats> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BotStats(2, 3000, 1));
    }
}
