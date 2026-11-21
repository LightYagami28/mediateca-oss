using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Mediateca.Ui.Services;

namespace Mediateca.Ui.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    public LogsViewModel(LogBuffer buffer)
    {
        Entries = buffer.Entries;
    }

    public ObservableCollection<LogEntry> Entries { get; }

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    public string CountText => $"{Entries.Count:N0} righe";
}
