using System.Globalization;
using Mediateca.Core.Domain;

namespace Mediateca.Server;

public sealed record MediaStream(Stream Stream, long Length, string ContentType);

public interface IMediaFileSource
{
    Task<MediaStream?> OpenReadAsync(MediaItem item, CancellationToken cancellationToken = default);
}

public sealed class LocalMediaFileSource(string root) : IMediaFileSource
{
    private readonly string _root = Path.GetFullPath(root);

    public Task<MediaStream?> OpenReadAsync(MediaItem item, CancellationToken cancellationToken = default)
    {
        if (item != null)
        {
            ArgumentNullException.ThrowIfNull(item);

            foreach (var candidate in Candidates(item))
            {
                var full = Path.GetFullPath(candidate);
                if (!IsBelowRoot(full) || !File.Exists(full))
                {
                    continue;
                }

                var stream = new FileStream(
                    full,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 64 * 1024,
                    useAsync: true);
                return Task.FromResult<MediaStream?>(new MediaStream(stream, stream.Length, ContentTypeFor(item.Extension)));
            }

            return Task.FromResult<MediaStream?>(null);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private IEnumerable<string> Candidates(MediaItem item)
    {
        if (item != null)
        {
            yield return Path.Combine(_root, item.ChannelId.ToString(CultureInfo.InvariantCulture), item.Name);
            yield return Path.Combine(_root, item.Name);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private bool IsBelowRoot(string path) =>
        path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
        || string.Equals(path, _root, StringComparison.OrdinalIgnoreCase);

    private static string ContentTypeFor(string extension) => extension switch
    {
        "mkv" => "video/x-matroska",
        "mp4" => "video/mp4",
        "avi" => "video/x-msvideo",
        "webm" => "video/webm",
        "flac" => "audio/flac",
        "mp3" => "audio/mpeg",
        "m4a" => "audio/mp4",
        "ogg" => "audio/ogg",
        "wav" => "audio/wav",
        "jpg" => "image/jpeg",
        "jpeg" => "image/jpeg",
        "png" => "image/png",
        "gif" => "image/gif",
        "webp" => "image/webp",
        "epub" => "application/epub+zip",
        "pdf" => "application/pdf",
        "zip" => "application/zip",
        _ => "application/octet-stream",
    };
}
