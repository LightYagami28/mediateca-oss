using System.Text.Json;

namespace Teledisk.Ui.Services;

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
            if (document is null)
            {
                return fallback;
            }

            return new UiOptions
            {
                Theme = document.Theme ?? fallback.Theme,
                WindowWidth = document.WindowWidth ?? fallback.WindowWidth,
                WindowHeight = document.WindowHeight ?? fallback.WindowHeight,
                SampleCatalogRows = document.SampleCatalogRows ?? fallback.SampleCatalogRows,
                ServerBindAddress = document.ServerBindAddress ?? fallback.ServerBindAddress,
                ServerPort = document.ServerPort ?? fallback.ServerPort,
                ServerApiToken = ProtectedSecrets.Decrypt(document.ServerApiTokenProtected ?? string.Empty),
                WebDavEnabled = document.WebDavEnabled ?? fallback.WebDavEnabled,
                SearchDebounceMs = document.SearchDebounceMs ?? fallback.SearchDebounceMs,
            };
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

    public void Save(UiOptions options)
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
            WebDavEnabled = options.WebDavEnabled,
            SearchDebounceMs = options.SearchDebounceMs,
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

    private sealed class SettingsDocument
    {
        public string? Theme { get; init; }

        public double? WindowWidth { get; init; }

        public double? WindowHeight { get; init; }

        public int? SampleCatalogRows { get; init; }

        public string? ServerBindAddress { get; init; }

        public int? ServerPort { get; init; }

        public string? ServerApiTokenProtected { get; init; }

        public bool? WebDavEnabled { get; init; }

        public int? SearchDebounceMs { get; init; }
    }
}
