using System.Globalization;
using Mediateca.Core.Domain;
using Mediateca.Data;

namespace Mediateca.Server;

internal static class MediaApi
{
    private const int CopyBufferBytes = 64 * 1024;

    public static async Task Stream(HttpContext context, long id, SqliteMediaStore store, IMediaFileSource? source, ServerOptions options)
    {
        if (!MediatecaServer.Authorize(context, options))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var item = store.Find(id);
        if (item is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (source is null)
        {
            context.Response.StatusCode = StatusCodes.Status501NotImplemented;
            return;
        }

        await StreamItem(context, item, source).ConfigureAwait(false);
    }

    public static async Task StreamItem(HttpContext context, MediaItem item, IMediaFileSource source)
    {
        var media = await source.OpenReadAsync(item, context.RequestAborted).ConfigureAwait(false);
        if (media is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await using var stream = media.Stream;
        context.Response.ContentType = media.ContentType;
        context.Response.Headers["Accept-Ranges"] = "bytes";

        var rangeHeader = context.Request.Headers.Range.ToString();
        if (!string.IsNullOrEmpty(rangeHeader))
        {
            if (!TryParseRange(rangeHeader, media.Length, out var start, out var end))
            {
                context.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
                context.Response.Headers["Content-Range"] = $"bytes */{media.Length}";
                return;
            }

            context.Response.StatusCode = StatusCodes.Status206PartialContent;
            context.Response.Headers["Content-Range"] = $"bytes {start}-{end}/{media.Length}";
            context.Response.ContentLength = end - start + 1;
            if (context.Request.Method == HttpMethods.Head)
            {
                return;
            }

            stream.Seek(start, SeekOrigin.Begin);
            await CopyRangeAsync(stream, context.Response.Body, end - start + 1, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        context.Response.ContentLength = media.Length;
        if (context.Request.Method == HttpMethods.Head)
        {
            return;
        }

        await stream.CopyToAsync(context.Response.Body, context.RequestAborted).ConfigureAwait(false);
    }

    private static bool TryParseRange(string header, long length, out long start, out long end)
    {
        start = 0;
        end = length - 1;

        var trimmed = header.Trim();
        if (!trimmed.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) || length <= 0)
        {
            return false;
        }

        var specification = trimmed[6..].Split(',')[0].Trim();
        if (specification.StartsWith('-'))
        {
            if (!long.TryParse(specification[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffix) || suffix <= 0)
            {
                return false;
            }

            start = Math.Max(0, length - suffix);
            end = length - 1;
            return true;
        }

        var parts = specification.Split('-');
        if (parts.Length != 2 || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var first))
        {
            return false;
        }

        if (first >= length)
        {
            return false;
        }

        start = first;
        if (string.IsNullOrEmpty(parts[1]))
        {
            end = length - 1;
        }
        else
        {
            if (!long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var last))
            {
                return false;
            }

            end = Math.Min(last, length - 1);
        }

        return end >= start;
    }

    private static async Task CopyRangeAsync(Stream source, Stream destination, long bytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[CopyBufferBytes];
        while (bytes > 0)
        {
            var toRead = (int)Math.Min(buffer.Length, bytes);
            var read = await source.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            bytes -= read;
        }
    }
}
