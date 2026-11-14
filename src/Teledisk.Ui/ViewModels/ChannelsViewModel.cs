using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Teledisk.Data;
using Teledisk.Ui.Models;

namespace Teledisk.Ui.ViewModels;

public partial class ChannelsViewModel : ObservableObject
{
    private readonly TelediskDatabase _database;

    public ChannelsViewModel(TelediskDatabase database, ILogger<ChannelsViewModel> logger)
    {
        _database = database;
        Logger = logger;
    }

    private ILogger<ChannelsViewModel> Logger { get; }

    public ObservableCollection<ChannelRow> Rows { get; private set; } = [];

    [ObservableProperty]
    private bool _isBusy;

    public string CountText => $"{Rows.Count:N0} canali";

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            _database.Migrate();
            var store = new SqliteMediaStore(_database);
            var rows = store.ListChannels().Select(ChannelRow.From).ToList();
            Logger.LogDebug("Canali caricati: {Count}", rows.Count);
            Rows = new ObservableCollection<ChannelRow>(rows);
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(CountText));
        }
        finally
        {
            IsBusy = false;
        }

        return Task.CompletedTask;
    }
}
