using System.Net;
using System.Text;
using Mediateca.Telegram;

namespace Mediateca.Tests;

public sealed class TelegramBotPoolTests
{
    private static readonly string[] Tokens = ["111111111:" + new string('a', 32), "222222222:" + new string('b', 32), "333333333:" + new string('c', 32)];

    [Fact]
    public async Task Distribuisce_le_chiamate_a_turno()
    {
        var handler = new PoolHandler();
        using var http = new HttpClient(handler);
        var pool = new TelegramBotPool(Clients(http));

        for (var call = 0; call < 4; call++)
        {
            await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token));
        }

        Assert.Equal(["111111111", "222222222", "333333333", "111111111"], handler.Bots);
    }

    [Fact]
    public async Task Sospende_il_bot_limitato_e_lo_riprende_dopo_retry_after()
    {
        var handler = new PoolHandler();
        handler.Limited.Add("111111111");
        var time = new ManualTime();
        using var http = new HttpClient(handler);
        var pool = new TelegramBotPool(Clients(http), time);

        Assert.True(await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token)));
        Assert.Equal(["111111111", "222222222"], handler.Bots);
        Assert.Equal(2, pool.AvailableCount);

        handler.Limited.Clear();
        await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token));
        await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token));
        Assert.DoesNotContain("111111111", handler.Bots.Skip(2));

        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(3, pool.AvailableCount);
        await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token));
        await pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token));
        Assert.Contains("111111111", handler.Bots.Skip(4));
    }

    [Fact]
    public async Task Senza_bot_liberi_segnala_il_limite()
    {
        var handler = new PoolHandler();
        handler.Limited.UnionWith(["111111111", "222222222", "333333333"]);
        using var http = new HttpClient(handler);
        var pool = new TelegramBotPool(Clients(http), new ManualTime());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => pool.ExecuteAsync((client, token) => client.SendMessageAsync(1, "x", token)));

        var limit = Assert.IsType<TelegramApiException>(error.InnerException);
        Assert.Equal(429, limit.ErrorCode);
        Assert.Equal(30, limit.RetryAfterSeconds);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(3, handler.Bots.Count);
    }

    [Fact]
    public void Rifiuta_un_pool_senza_bot_configurati()
    {
        using var http = new HttpClient(new PoolHandler());

        Assert.Throws<ArgumentException>(() => new TelegramBotPool([new TelegramBotClient(http, new TelegramOptions { BotToken = "" })]));
    }

    private static IEnumerable<TelegramBotClient> Clients(HttpClient http) =>
        Tokens.Select(token => new TelegramBotClient(http, new TelegramOptions { BotToken = token }));

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }

    private sealed class PoolHandler : HttpMessageHandler
    {
        public List<string> Bots { get; } = [];

        public HashSet<string> Limited { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request != null)
            {
                var bot = request.RequestUri!.AbsolutePath.Split('/')[1]["bot".Length..].Split(':')[0];
                Bots.Add(bot);
                var body = Limited.Contains(bot)
                    ? """{"ok":false,"error_code":429,"description":"Too Many Requests: retry after 30","parameters":{"retry_after":30}}"""
                    : """{"ok":true,"result":{"message_id":1,"chat":{"id":1},"date":1700000000,"text":"x"}}""";
                return Task.FromResult(new HttpResponseMessage(Limited.Contains(bot) ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }
            else
            {
                throw new ArgumentNullException();
            }
        }
    }
}
