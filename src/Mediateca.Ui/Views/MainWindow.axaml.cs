using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Mediateca.Ui.Services;
using Mediateca.Ui.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mediateca.Ui.Views;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(Resolve<MainWindowViewModel>(), Resolve<ThemeService>(), Resolve<IOptions<UiOptions>>())
    {
    }

    public MainWindow(MainWindowViewModel viewModel, ThemeService theme, IOptions<UiOptions> options)
    {
        InitializeComponent();
        Width = options.Value.WindowWidth;
        Height = options.Value.WindowHeight;
        DataContext = viewModel;

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = theme.Current;
        }

        theme.Changed += OnThemeChanged;

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel model)
            {
                await model.LoadAsync();
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e != null)
        {
            base.OnKeyDown(e);

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.K)
            {
                this.FindControl<TextBox>("SearchBox")?.Focus();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && DataContext is MainWindowViewModel viewModel && viewModel.Catalog.Search.Length > 0)
            {
                viewModel.Catalog.Search = string.Empty;
                e.Handled = true;
            }
        }
        else
        {
            throw new ArgumentNullException();
        }
    }

    private static void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant variant)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = variant;
        }
    }

    private static T Resolve<T>()
        where T : notnull =>
        (App.Services ?? throw new InvalidOperationException("Il contenitore dei servizi non è inizializzato.")).GetRequiredService<T>();
}
