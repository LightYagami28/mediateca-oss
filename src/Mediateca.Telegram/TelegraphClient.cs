using System.Text;
using System.Text.Json;

namespace Mediateca.Telegram;

public sealed class TelegraphClient(HttpClient httpClient)
{
    public async Task<string?> CreatePageAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var body = JsonSerializer.Serialize(new
        {
            access_token = string.Empty,
            title,
            author_name = "Mediateca",
            content = new[]
            {
                new { tag = "p", children = new[] { content } },
            },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.telegra.ph/createPage")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.TryGetProperty("result", out var result)
               && result.TryGetProperty("url", out var url)
            ? url.GetString()
            : null;
    }
}
