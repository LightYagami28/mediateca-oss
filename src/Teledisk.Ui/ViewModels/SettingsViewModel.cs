using System.Globalization;
using System.Net;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Teledisk.Ui.Services;

namespace Teledisk.Ui.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly double _windowWidth;
    private readonly double _windowHeight;
    private readonly int _sampleCatalogRows;
    private readonly int _searchDebounceMs;

    public SettingsViewModel(SettingsService settings, IOptions<UiOptions> options, ThemeService theme)
    {
        _settings = settings;
        _theme = theme;
        var current = options.Value;
        _windowWidth = current.WindowWidth;
        _windowHeight = current.WindowHeight;
        _sampleCatalogRows = current.SampleCatalogRows;
        _searchDebounceMs = current.SearchDebounceMs;
        IsLightTheme = current.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase);
        ServerBindAddress = current.ServerBindAddress;
        ServerPort = current.ServerPort.ToString(CultureInfo.InvariantCulture);
        ServerApiToken = current.ServerApiToken;
        WebDavEnabled = current.WebDavEnabled;
    }

    [ObservableProperty]
    private bool _isLightTheme;

    [ObservableProperty]
    private string _serverBindAddress = "127.0.0.1";

    [ObservableProperty]
    private string _serverPort = "8083";

    [ObservableProperty]
    private string _serverApiToken = string.Empty;

    [ObservableProperty]
    private bool _webDavEnabled = true;

    [ObservableProperty]
    private string _statusText = string.Empty;

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

        var updated = new UiOptions
        {
            Theme = IsLightTheme ? "Light" : "Dark",
            WindowWidth = _windowWidth,
            WindowHeight = _windowHeight,
            SampleCatalogRows = _sampleCatalogRows,
            ServerBindAddress = ServerBindAddress.Trim(),
            ServerPort = port,
            ServerApiToken = ServerApiToken.Trim(),
            WebDavEnabled = WebDavEnabled,
            SearchDebounceMs = _searchDebounceMs,
        };

        _settings.Save(updated);
        _theme.Set(IsLightTheme ? ThemeVariant.Light : ThemeVariant.Dark);
        StatusText = $"Salvato ({DateTime.Now:HH:mm:ss}) — riavvia per applicare al server.";
    }
}
