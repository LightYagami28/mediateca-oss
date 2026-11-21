using System.Text.Json;

namespace Mediateca.Ui.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;

    public SettingsService(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    public UiOptions Load(UiOptions fallback)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return fallback;
            }

            var document = JsonSerializer.Deserialize<SettingsDocument>(File.ReadAllText(_path), JsonOptions);
            return document is null ? fallback : Merge(document, fallback);
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    private static T Value<T>(T? candidate, T fallback) where T : struct => candidate ?? fallback;

    private static T Reference<T>(T? value, T fallback) where T : class => value ?? fallback;

    private static UiOptions Merge(SettingsDocument document, UiOptions fallback)
    {
        if (document != null)
        {
            if (fallback != null)
            {
                if (document != null)
                {
                    if (fallback != null)
                    {
                        return new UiOptions
                        {
                            Theme = Reference(document.Theme, fallback.Theme),
                            WindowWidth = Value(document.WindowWidth, fallback.WindowWidth),
                            WindowHeight = Value(document.WindowHeight, fallback.WindowHeight),
                            SampleCatalogRows = Value(document.SampleCatalogRows, fallback.SampleCatalogRows),
                            ServerBindAddress = Reference(document.ServerBindAddress, fallback.ServerBindAddress),
                            ServerPort = Value(document.ServerPort, fallback.ServerPort),
                            ServerApiToken = ProtectedSecrets.Decrypt(Reference(document.ServerApiTokenProtected, string.Empty)),
                            WebDavPasswordHash = Reference(document.WebDavPasswordHash, fallback.WebDavPasswordHash),
                            WebDavEnabled = Value(document.WebDavEnabled, fallback.WebDavEnabled),
                            SearchDebounceMs = Value(document.SearchDebounceMs, fallback.SearchDebounceMs),
                            BotToken = ProtectedSecrets.Decrypt(Reference(document.BotTokenProtected, string.Empty)),
                            MtprotoApiId = Value(document.MtprotoApiId, fallback.MtprotoApiId),
                            MtprotoApiHash = ProtectedSecrets.Decrypt(Reference(document.MtprotoApiHashProtected, string.Empty)),
                            MtprotoSessionPath = Reference(document.MtprotoSessionPath, fallback.MtprotoSessionPath),
                            MtprotoCacheDirectory = Reference(document.MtprotoCacheDirectory, fallback.MtprotoCacheDirectory),
                        };
                    }
                }

                throw new ArgumentNullException(nameof(document));
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

    public void Save(UiOptions options)
    {
        if (options != null)
        {
            ArgumentNullException.ThrowIfNull(options);

            var document = new SettingsDocument
            {
                Theme = options.Theme,
                WindowWidth = options.WindowWidth,
                WindowHeight = options.WindowHeight,
                SampleCatalogRows = options.SampleCatalogRows,
                ServerBindAddress = options.ServerBindAddress,
                ServerPort = options.ServerPort,
                ServerApiTokenProtected = ProtectedSecrets.Encrypt(options.ServerApiToken),
                WebDavPasswordHash = options.WebDavPasswordHash,
                WebDavEnabled = options.WebDavEnabled,
                SearchDebounceMs = options.SearchDebounceMs,
                BotTokenProtected = ProtectedSecrets.Encrypt(options.BotToken),
                MtprotoApiId = options.MtprotoApiId,
                MtprotoApiHashProtected = ProtectedSecrets.Encrypt(options.MtprotoApiHash),
                MtprotoSessionPath = options.MtprotoSessionPath,
                MtprotoCacheDirectory = options.MtprotoCacheDirectory,
            };

            var json = JsonSerializer.Serialize(document, JsonOptions);
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private sealed class SettingsDocument
    {
        public string? Theme { get; init; }

        public double? WindowWidth { get; init; }

        public double? WindowHeight { get; init; }

        public int? SampleCatalogRows { get; init; }

        public string? ServerBindAddress { get; init; }

        public int? ServerPort { get; init; }

        public string? ServerApiTokenProtected { get; init; }

        public string? WebDavPasswordHash { get; init; }

        public bool? WebDavEnabled { get; init; }

        public int? SearchDebounceMs { get; init; }

        public string? BotTokenProtected { get; init; }

        public int? MtprotoApiId { get; init; }

        public string? MtprotoApiHashProtected { get; init; }

        public string? MtprotoSessionPath { get; init; }

        public string? MtprotoCacheDirectory { get; init; }
    }
}
