using System.Windows.Input;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediateca.Ui.Models;
using Mediateca.Ui.Services;

namespace Mediateca.Ui.ViewModels;

public partial class NavItem : ObservableObject
{
    public NavItem(string label, string section, string view)
    {
        Label = label;
        Section = section;
        View = view;
    }

    public string Label { get; }

    public string Section { get; }

    public string View { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ICommand? _selectCommand;
}

public sealed record NavSection(string Title, IReadOnlyList<NavItem> Items);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ThemeService _theme;

    public MainWindowViewModel(
        CatalogViewModel catalog,
        ChannelsViewModel channels,
        SettingsViewModel settings,
        LogsViewModel logs,
        ThemeService theme,
        ServerService server)
    {
        Catalog = catalog;
        Channels = channels;
        Settings = settings;
        Logs = logs;
        _theme = theme;
        Server = server;
        CurrentView = catalog;

        Sections = BuildSections();
        foreach (var item in Sections.SelectMany(section => section.Items))
        {
            item.SelectCommand = SelectNavCommand;
        }

        server.StatusChanged += (_, _) => OnPropertyChanged(nameof(ServerStatusText));
    }

    public CatalogViewModel Catalog { get; }

    public ChannelsViewModel Channels { get; }

    public SettingsViewModel Settings { get; }

    public LogsViewModel Logs { get; }

    private ServerService Server { get; }

    public IReadOnlyList<NavSection> Sections { get; }

    public ObservableObject CurrentView { get; private set; }

    [ObservableProperty]
    private bool _isLightTheme;

    public string ServerStatusText => Server.IsRunning ? $"Server attivo · {Server.Url}" : "Server non attivo";

    public string FreeSpaceText
    {
        get
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? @"C:\");
                return $"{Format.Size(drive.AvailableFreeSpace)} liberi";
            }
            catch (IOException)
            {
                return "—";
            }
        }
    }

    public string VersionText => "Mediateca 1.0 · .NET 11";

    [RelayCommand]
    private void ToggleTheme()
    {
        _theme.Toggle();
        IsLightTheme = _theme.Current == ThemeVariant.Light;
    }

    [RelayCommand]
    private void SelectNav(NavItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var candidate in Sections.SelectMany(section => section.Items))
        {
            candidate.IsSelected = ReferenceEquals(candidate, item);
        }

        CurrentView = item.View switch
        {
            "channels" => Channels,
            "settings" => Settings,
            "logs" => Logs,
            _ => Catalog,
        };
        OnPropertyChanged(nameof(CurrentView));

        if (item.View == "channels" && Channels.Rows.Count == 0)
        {
            _ = Channels.LoadAsync();
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (Catalog.Rows.Count == 0)
        {
            await Catalog.LoadAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    private static IReadOnlyList<NavSection> BuildSections() =>
    [
        new NavSection("Libreria",
        [
            new NavItem("Catalogo", "Libreria", "catalog"),
            new NavItem("Canali", "Libreria", "channels"),
            new NavItem("Generi", "Libreria", "catalog"),
            new NavItem("Etichette", "Libreria", "catalog"),
        ]),
        new NavSection("Trasferimenti",
        [
            new NavItem("Code", "Trasferimenti", "catalog"),
            new NavItem("Scaricati", "Trasferimenti", "catalog"),
            new NavItem("Backup", "Trasferimenti", "catalog"),
        ]),
        new NavSection("Accesso",
        [
            new NavItem("Server HTTP", "Accesso", "settings"),
            new NavItem("WebDAV", "Accesso", "settings"),
            new NavItem("FUSE", "Accesso", "catalog"),
            new NavItem("Condivisione", "Accesso", "catalog"),
        ]),
        new NavSection("Bot Telegram",
        [
            new NavItem("Comandi", "Bot", "catalog"),
            new NavItem("Utenti", "Bot", "catalog"),
            new NavItem("Pagamenti", "Bot", "catalog"),
        ]),
        new NavSection("Sistema",
        [
            new NavItem("Impostazioni", "Sistema", "settings"),
            new NavItem("Log", "Sistema", "logs"),
        ]),
    ];
}
