using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mediateca.Ui.Models;
using Mediateca.Ui.Services;
using Microsoft.Extensions.Logging;

namespace Mediateca.Ui.ViewModels;

public partial class CatalogViewModel : ObservableObject
{
    private readonly ICatalogSource _source;
    private readonly int _searchDebounceMs;
    private CancellationTokenSource? _searchCts;
    private IReadOnlyList<CatalogRow> _all = [];

    public CatalogViewModel(ICatalogSource source, ILogger<CatalogViewModel> logger, int searchDebounceMs = 150)
    {
        _source = source;
        Logger = logger;
        _searchDebounceMs = searchDebounceMs;
    }

    private ILogger<CatalogViewModel> Logger { get; }

    public ObservableCollection<CatalogRow> Rows { get; private set; } = [];

    [ObservableProperty]
    private string _search = string.Empty;

    private CatalogRow? _selectedRow;

    public CatalogRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!SetProperty(ref _selectedRow, value))
            {
                return;
            }

            OnPropertyChanged(nameof(DetailTitle));
            OnPropertyChanged(nameof(DetailSubtitle));
            OnPropertyChanged(nameof(DetailSize));
            OnPropertyChanged(nameof(DetailKind));
            OnPropertyChanged(nameof(DetailChannel));
            OnPropertyChanged(nameof(DetailYear));
            OnPropertyChanged(nameof(DetailDuration));
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSearching;

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

    partial void OnSearchChanged(string value) => ScheduleSearch();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _all = await _source.LoadAsync(cancellationToken).ConfigureAwait(true);
            Logger.LogDebug("Catalogo caricato con {Count} elementi", _all.Count);
            SetRows(_all);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ScheduleSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _ = DebounceAndSearchAsync(cts.Token);
    }

    private async Task DebounceAndSearchAsync(CancellationToken token)
    {
        await Task.Delay(_searchDebounceMs, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (token.IsCancellationRequested)
        {
            return;
        }

        IsSearching = true;
        try
        {
            var results = await _source.SearchAsync(Search.Trim(), 2_000, token).ConfigureAwait(true);
            SetRows(results);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Logger.LogDebug("Ricerca annullata prima della fine.");
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void SetRows(IEnumerable<CatalogRow> rows)
    {
        Rows = new ObservableCollection<CatalogRow>(rows);
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(ItemsText));
        OnPropertyChanged(nameof(TotalItemsText));
        OnPropertyChanged(nameof(TotalSizeText));
        OnPropertyChanged(nameof(ChannelsText));
    }
}
