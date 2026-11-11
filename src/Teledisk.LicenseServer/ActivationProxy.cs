using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Teledisk.LicenseServer;

/// <summary>
/// Minimal HTTP proxy that terminates TLS only for the activation host and tunnels everything else.
/// </summary>
/// <remarks>
/// The original client talks to <c>https://teledisk.net</c> with a hardcoded URL and no override,
/// and the hosts file needs administrator rights. Both problems disappear if the client is pointed
/// at this proxy through the standard <c>HTTPS_PROXY</c> variable, which <c>requests</c> honours:
/// the proxy answers the CONNECT for the activation host itself and passes every other host
/// through untouched, so Telegram, TMDB and the rest keep working normally.
/// </remarks>
public sealed class ActivationProxy : IAsyncDisposable
{
    private static readonly string[] InterceptedHosts = ["teledisk.net", "www.teledisk.net"];

    private readonly ActivationService _service;
    private readonly X509Certificate2 _certificate;
    private readonly Action<string> _log;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    /// <summary>Creates the proxy.</summary>
    public ActivationProxy(ActivationService service, X509Certificate2 certificate, Action<string> log)
    {
        _service = service;
        _certificate = certificate;
        _log = log;
    }

    /// <summary>Bound port, valid after <see cref="StartAsync"/>.</summary>
    public int Port { get; private set; }

    /// <summary>Starts listening on the loopback interface.</summary>
    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _cts = new CancellationTokenSource();
        _ = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            _ = Task.Run(() => HandleClientAsync(client), CancellationToken.None);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            client.NoDelay = true;
            try
            {
                var stream = client.GetStream();
                var (requestLine, headers, leftover) = await ReadHeadAsync(stream).ConfigureAwait(false);
                if (requestLine is null)
                {
                    return;
                }

                var parts = requestLine.Split(' ');
                if (parts.Length < 3)
                {
                    await WriteAsync(stream, 200, "OK", "application/json", """{"success":true,"key":"x","expired":"2099-12-31"}""").ConfigureAwait(false);
                    return;
                }

                var method = parts[0];
                var target = parts[1];

                if (method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
                {
                    var authority = target.Split(':');
                    var host = authority[0];
                    var upstreamPort = authority.Length > 1 && int.TryParse(authority[1], out var parsed) ? parsed : 443;

                    if (InterceptedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
                    {
                        _log($"{method} {target} -> intercepted (TLS terminated locally)");
                        await WriteRawAsync(stream, "HTTP/1.1 200 Connection established\r\n\r\n").ConfigureAwait(false);

                        using var tls = new SslStream(stream, leaveInnerStreamOpen: false);
                        await tls.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false, checkCertificateRevocation: false)
                            .ConfigureAwait(false);

                        await ServeHttpAsync(tls, leftover).ConfigureAwait(false);
                        return;
                    }

                    _log($"{method} {target} -> tunnel");
                    await TunnelAsync(stream, host, upstreamPort, leftover).ConfigureAwait(false);
                    return;
                }

                // Plain HTTP request: only the activation host is answered locally.
                var headerHost = headers.FirstOrDefault(h => h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)).Value;
                if (headerHost is not null && InterceptedHosts.Contains(headerHost.Split(':')[0], StringComparer.OrdinalIgnoreCase))
                {
                    await ServeHttpAsync(stream, leftover, requestLine, headers).ConfigureAwait(false);
                    return;
                }

                if (headerHost is not null)
                {
                    await TunnelAsync(stream, headerHost.Split(':')[0], 80, leftover, requestLine, headers).ConfigureAwait(false);
                    return;
                }

                await WriteAsync(stream, 404, "Not Found", "application/json", """{"success":false,"code":"not_found"}""").ConfigureAwait(false);
            }
            catch (Exception exc)
            {
                _log($"connection error: {exc.GetType().Name}: {exc.Message}");
            }
        }
    }

    private async Task ServeHttpAsync(
        Stream stream,
        byte[] leftover,
        string? firstLine = null,
        Dictionary<string, string>? firstHeaders = null)
    {
        string? requestLine = firstLine;
        Dictionary<string, string>? headers = firstHeaders;
        var body = Array.Empty<byte>();

        if (requestLine is null)
        {
            var (line, parsedHeaders, rest) = await ReadHeadAsync(stream).ConfigureAwait(false);
            requestLine = line;
            headers = parsedHeaders;
            var contentLength = ContentLength(parsedHeaders);
            body = await ReadBodyAsync(stream, rest, contentLength).ConfigureAwait(false);
        }
        else if (headers is not null)
        {
            var contentLength = ContentLength(headers);
            body = await ReadBodyAsync(stream, leftover, contentLength).ConfigureAwait(false);
        }

        if (requestLine is null)
        {
            return;
        }

        var path = requestLine.Split(' ').ElementAtOrDefault(1) ?? "/";
        _log($"  {requestLine}");
        foreach (var header in headers ?? [])
        {
            _log($"    {header.Key}: {header.Value}");
        }

        if (body.Length > 0)
        {
            var text = Encoding.UTF8.GetString(body);
            _log($"    body({body.Length}): {text[..Math.Min(700, text.Length)]}");
        }

        var (status, json) = _service.Handle(path, body);
        await WriteAsync(stream, status, status == 200 ? "OK" : "Not Found", "application/json", json).ConfigureAwait(false);
    }

    private async Task TunnelAsync(
        Stream clientStream,
        string host,
        int port,
        byte[] leftover,
        string? firstLine = null,
        Dictionary<string, string>? headers = null)
    {
        using var upstream = new TcpClient();
        await upstream.ConnectAsync(host, port).ConfigureAwait(false);
        var upstreamStream = upstream.GetStream();

        if (firstLine is null)
        {
            await WriteRawAsync(clientStream, "HTTP/1.1 200 Connection established\r\n\r\n").ConfigureAwait(false);
        }
        else
        {
            var head = new StringBuilder(firstLine + "\r\n");
            foreach (var header in headers!)
            {
                head.Append($"{header.Key}: {header.Value}\r\n");
            }

            head.Append("\r\n");
            var headBytes = Encoding.ASCII.GetBytes(head.ToString());
            await upstreamStream.WriteAsync(headBytes).ConfigureAwait(false);
        }

        if (leftover.Length > 0)
        {
            await upstreamStream.WriteAsync(leftover).ConfigureAwait(false);
        }

        var clientToUpstream = clientStream.CopyToAsync(upstreamStream);
        var upstreamToClient = upstreamStream.CopyToAsync(clientStream);
        await Task.WhenAny(clientToUpstream, upstreamToClient).ConfigureAwait(false);
    }

    private static int ContentLength(Dictionary<string, string> headers) =>
        headers.TryGetValue("Content-Length", out var raw) && int.TryParse(raw, out var value) ? value : 0;

    private static async Task<byte[]> ReadBodyAsync(Stream stream, byte[] already, int contentLength)
    {
        if (contentLength <= 0)
        {
            return already;
        }

        var buffer = new byte[contentLength];
        var copied = Math.Min(already.Length, contentLength);
        Array.Copy(already, buffer, copied);

        while (copied < contentLength)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(copied, contentLength - copied)).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            copied += read;
        }

        return buffer;
    }

    private static async Task<(string? Line, Dictionary<string, string> Headers, byte[] Leftover)> ReadHeadAsync(Stream stream)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var buffer = new List<byte>(1024);
        var one = new byte[1];

        while (buffer.Count < 64 * 1024)
        {
            var read = await stream.ReadAsync(one).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            buffer.Add(one[0]);
            var count = buffer.Count;
            if (count >= 4 && buffer[count - 4] == 13 && buffer[count - 3] == 10 && buffer[count - 2] == 13 && buffer[count - 1] == 10)
            {
                break;
            }
        }

        var text = Encoding.ASCII.GetString(buffer.ToArray());
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var requestLine = lines.FirstOrDefault();

        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator > 0)
            {
                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return (requestLine, headers, []);
    }

    private static Task WriteAsync(Stream stream, int status, string reason, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        return WriteRawAsync(stream, head + body);
    }

    private static Task WriteRawAsync(Stream stream, string text) =>
        stream.WriteAsync(Encoding.ASCII.GetBytes(text)).AsTask();

    /// <summary>Stops listening.</summary>
    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
