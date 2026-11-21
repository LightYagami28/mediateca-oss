using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mediateca.Data;
using Mediateca.Ui.Models;
using Microsoft.Extensions.Logging;

namespace Mediateca.Ui.ViewModels;

public partial class ChannelsViewModel : ObservableObject
{
    private readonly MediatecaDatabase _database;

    public ChannelsViewModel(MediatecaDatabase database, ILogger<ChannelsViewModel> logger)
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
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("Canali caricati: {Count}", rows.Count);
            }
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
