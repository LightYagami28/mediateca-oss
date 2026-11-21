namespace Mediateca.Media;

public static class LocalArtwork
{
    private static readonly string[] CoverNames = ["folder.jpg", "folder.png", "poster.jpg", "poster.png", "cover.jpg", "cover.png"];

    public static string? FindCover(string mediaFilePath)
    {
        if (mediaFilePath != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mediaFilePath);
            var directory = Path.GetDirectoryName(mediaFilePath);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            var baseName = Path.GetFileNameWithoutExtension(mediaFilePath);
            foreach (var candidate in new[] { $"{baseName}.jpg", $"{baseName}.png" }.Concat(CoverNames))
            {
                var full = Path.Combine(directory, candidate);
                if (File.Exists(full))
                {
                    return full;
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
