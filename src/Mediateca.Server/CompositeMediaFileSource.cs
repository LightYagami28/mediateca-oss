namespace Mediateca.Server;

public sealed class CompositeMediaFileSource(params IMediaFileSource[] sources) : IMediaFileSource
{
    private readonly IReadOnlyList<IMediaFileSource> _sources = sources.ToList();

    public async Task<MediaStream?> OpenReadAsync(Core.Domain.MediaItem item, CancellationToken cancellationToken = default)
    {
        if (item != null)
        {
            foreach (var source in _sources)
            {
                var media = await source.OpenReadAsync(item, cancellationToken).ConfigureAwait(false);
                if (media is not null)
                {
                    return media;
                }
            }

            return null;
        }
        else
        {
            throw new ArgumentNullException();
        }
    }
}
