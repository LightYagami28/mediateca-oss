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
    private const string ViewCatalog = "catalog";
    private const string ViewSettings = "settings";
    private const string SectionLibrary = "Libreria";
    private const string SectionTransfers = "Trasferimenti";
    private const string SectionAccess = "Accesso";

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

    public static string FreeSpaceText
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

    public static string VersionText => "Mediateca 1.0 · .NET 11";

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
            ViewSettings => Settings,
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
        new NavSection(SectionLibrary,
        [
            new NavItem("Catalogo", SectionLibrary, ViewCatalog),
            new NavItem("Canali", SectionLibrary, "channels"),
            new NavItem("Generi", SectionLibrary, ViewCatalog),
            new NavItem("Etichette", SectionLibrary, ViewCatalog),
        ]),
        new NavSection(SectionTransfers,
        [
            new NavItem("Code", SectionTransfers, ViewCatalog),
            new NavItem("Scaricati", SectionTransfers, ViewCatalog),
            new NavItem("Backup", SectionTransfers, ViewCatalog),
        ]),
        new NavSection(SectionAccess,
        [
            new NavItem("Server HTTP", SectionAccess, ViewSettings),
            new NavItem("WebDAV", SectionAccess, ViewSettings),
            new NavItem("FUSE", SectionAccess, ViewCatalog),
            new NavItem("Condivisione", SectionAccess, ViewCatalog),
        ]),
        new NavSection("Bot Telegram",
        [
            new NavItem("Comandi", "Bot", ViewCatalog),
            new NavItem("Utenti", "Bot", ViewCatalog),
            new NavItem("Pagamenti", "Bot", ViewCatalog),
        ]),
        new NavSection("Sistema",
        [
            new NavItem("Impostazioni", "Sistema", ViewSettings),
            new NavItem("Log", "Sistema", "logs"),
        ]),
    ];
}
