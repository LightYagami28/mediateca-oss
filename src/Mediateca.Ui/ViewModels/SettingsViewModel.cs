using System.Globalization;
using System.Net;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediateca.Core.Security;
using Mediateca.Ui.Services;
using Microsoft.Extensions.Options;

namespace Mediateca.Ui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly double _windowWidth;
    private readonly double _windowHeight;
    private readonly int _sampleCatalogRows;
    private readonly int _searchDebounceMs;
    private readonly string _currentWebDavHash;
    private readonly string _mtprotoSessionPath;
    private readonly string _mtprotoCacheDirectory;

    public SettingsViewModel(SettingsService settings, IOptions<UiOptions> options, ThemeService theme)
    {
        _settings = settings;
        _theme = theme;
        var current = options.Value;
        _windowWidth = current.WindowWidth;
        _windowHeight = current.WindowHeight;
        _sampleCatalogRows = current.SampleCatalogRows;
        _searchDebounceMs = current.SearchDebounceMs;
        _currentWebDavHash = current.WebDavPasswordHash;
        _mtprotoSessionPath = current.MtprotoSessionPath;
        _mtprotoCacheDirectory = current.MtprotoCacheDirectory;
        IsLightTheme = current.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        ServerBindAddress = current.ServerBindAddress;
        ServerPort = current.ServerPort.ToString(CultureInfo.InvariantCulture);
        ServerApiToken = current.ServerApiToken;
        WebDavEnabled = current.WebDavEnabled;
        BotToken = current.BotToken;
        MtprotoApiId = current.MtprotoApiId.ToString(CultureInfo.InvariantCulture);
        MtprotoApiHash = current.MtprotoApiHash;
    }

    [ObservableProperty]
    public partial bool IsLightTheme { get; set; }

    [ObservableProperty]
    public partial string ServerBindAddress { get; set; } = "127.0.0.1";

    [ObservableProperty]
    public partial string ServerPort { get; set; } = "8083";

    [ObservableProperty]
    public partial string ServerApiToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WebDavPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool WebDavEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string BotToken { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MtprotoApiId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MtprotoApiHash { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(ServerPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            StatusText = "Porta non valida: serve un numero tra 1 e 65535.";
            return;
        }

        if (!IPAddress.TryParse(ServerBindAddress, out _))
        {
            StatusText = "Indirizzo di bind non valido.";
            return;
        }

        var apiId = 0;
        if (MtprotoApiId.Trim().Length > 0
            && (!int.TryParse(MtprotoApiId, NumberStyles.Integer, CultureInfo.InvariantCulture, out apiId) || apiId < 0))
        {
            StatusText = "ApiId non valido: serve un numero positivo.";
            return;
        }

        var hash = string.IsNullOrWhiteSpace(WebDavPassword)
            ? _currentWebDavHash
            : PasswordHasher.Hash(WebDavPassword);

        var updated = new UiOptions
        {
            Theme = IsLightTheme ? "Light" : "Dark",
            WindowWidth = _windowWidth,
            WindowHeight = _windowHeight,
            SampleCatalogRows = _sampleCatalogRows,
            ServerBindAddress = ServerBindAddress.Trim(),
            ServerPort = port,
            ServerApiToken = ServerApiToken.Trim(),
            WebDavPasswordHash = hash,
            WebDavEnabled = WebDavEnabled,
            SearchDebounceMs = _searchDebounceMs,
            BotToken = BotToken.Trim(),
            MtprotoApiId = apiId,
            MtprotoApiHash = MtprotoApiHash.Trim(),
            MtprotoSessionPath = _mtprotoSessionPath,
            MtprotoCacheDirectory = _mtprotoCacheDirectory,
        };

        _settings.Save(updated);
        _theme.Set(IsLightTheme ? ThemeVariant.Light : ThemeVariant.Dark);
        WebDavPassword = string.Empty;
        StatusText = $"Salvato ({DateTime.Now:HH:mm:ss}) — riavvia per applicare al server e al bot.";
    }
}
