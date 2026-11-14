using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Teledisk.Ui.Models;
using Teledisk.Ui.Services;

namespace Teledisk.Ui.ViewModels;

public partial class NavItem : ObservableObject
{
    public NavItem(string label, string section, string badge = "")
    {
        Label = label;
        Section = section;
        Badge = badge;
    }

    public string Label { get; }

    public string Section { get; }

    public string Badge { get; internal set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private ICommand? _selectCommand;
}

public sealed record NavSection(string Title, IReadOnlyList<NavItem> Items);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICatalogSource _source;
    private IReadOnlyList<CatalogRow> _all = [];

    public MainWindowViewModel(ICatalogSource source, ILogger<MainWindowViewModel> logger, ThemeService theme)
    {
        _source = source;
        Logger = logger;
        Theme = theme;
        Sections = BuildSections();
        foreach (var item in Sections.SelectMany(section => section.Items))
        {
            item.SelectCommand = SelectNavCommand;
        }
    }

    public ObservableCollection<CatalogRow> Rows { get; } = [];

    public IReadOnlyList<NavSection> Sections { get; }

    private ILogger<MainWindowViewModel> Logger { get; }

    private ThemeService Theme { get; }

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailTitle), nameof(DetailSubtitle), nameof(DetailSize), nameof(DetailKind), nameof(DetailChannel), nameof(DetailYear), nameof(DetailDuration), nameof(HasSelection))]
    private CatalogRow? _selectedRow;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isLightTheme;

    public bool HasSelection => SelectedRow is not null;

    public string DetailTitle => SelectedRow?.Name ?? "Nessuna selezione";

    public string DetailSubtitle => SelectedRow?.Channel ?? "Seleziona un elemento del catalogo";

    public string DetailSize => SelectedRow?.SizeText ?? "—";

    public string DetailKind => SelectedRow?.KindLabel ?? "—";

    public string DetailChannel => SelectedRow?.Channel ?? "—";

    public string DetailYear => SelectedRow?.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—";

    public string DetailDuration => SelectedRow?.DurationText is { Length: > 0 } duration ? duration : "—";

    public string ItemsText => $"{Rows.Count:N0} elementi";

    public string TotalItemsText => $"{_all.Count:N0} in catalogo";

    public string TotalSizeText => Format.Size(_all.Sum(row => row.Size));

    public string ChannelsText => $"{_all.Select(row => row.Channel).Distinct().Count()} canali";

    public string QueueText => "0 in coda";

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

    public string VersionText => "Teledisk Community 1.0 · .NET 11";

    partial void OnSearchChanged(string value) => ApplyFilter();

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    [RelayCommand]
    private void ToggleTheme()
    {
        Theme.Toggle();
        IsLightTheme = Theme.Current == Avalonia.Styling.ThemeVariant.Light;
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
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _all = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
            Logger.LogDebug("Catalogo caricato con {Count} elementi", _all.Count);
            ApplyFilter();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ApplyFilter()
    {
        var query = Search?.Trim() ?? string.Empty;

        var filtered = query.Length == 0
            ? _all
            : _all.Where(row =>
                row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                row.Channel.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        Rows.Clear();
        foreach (var row in filtered)
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(ItemsText));
        OnPropertyChanged(nameof(TotalItemsText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(ChannelsText));
    }

    private static IReadOnlyList<NavSection> BuildSections() =>
    [
        new NavSection("Libreria",
        [
            new NavItem("Catalogo", "Libreria"),
            new NavItem("Canali", "Libreria"),
            new NavItem("Generi", "Libreria"),
            new NavItem("Etichette", "Libreria"),
        ]),
        new NavSection("Trasferimenti",
        [
            new NavItem("Code", "Trasferimenti"),
            new NavItem("Scaricati", "Trasferimenti"),
            new NavItem("Backup", "Trasferimenti"),
        ]),
        new NavSection("Accesso",
        [
            new NavItem("Server HTTP", "Accesso"),
            new NavItem("WebDAV", "Accesso"),
            new NavItem("FUSE", "Accesso"),
            new NavItem("Condivisione", "Accesso"),
        ]),
        new NavSection("Bot Telegram",
        [
            new NavItem("Comandi", "Bot"),
            new NavItem("Utenti", "Bot"),
            new NavItem("Pagamenti", "Bot"),
        ]),
        new NavSection("Sistema",
        [
            new NavItem("Impostazioni", "Sistema"),
            new NavItem("Log", "Sistema"),
        ]),
    ];
}
