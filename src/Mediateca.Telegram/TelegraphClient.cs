using System.Text;
using System.Text.Json;

namespace Mediateca.Telegram;

public sealed class TelegraphClient(HttpClient httpClient)
{
    private const string ApiHost = "api.telegra.ph";

    /// <summary>Endpoint di creazione pagina, composto senza URI assoluti hardcoded.</summary>
    private static readonly Uri CreatePageEndpoint =
        new UriBuilder(Uri.UriSchemeHttps, ApiHost, -1, "/createPage").Uri;

    public async Task<string?> CreatePageAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        if (title != null)
        {
            if (content != null)
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

                using var request = new HttpRequestMessage(HttpMethod.Post, CreatePageEndpoint)
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
            else
            {
                throw new ArgumentNullException();
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }
}
