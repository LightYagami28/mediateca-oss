using Microsoft.Extensions.Logging.Abstractions;
using Teledisk.Ui.Models;
using Teledisk.Ui.Services;
using Teledisk.Ui.ViewModels;

namespace Teledisk.Ui.Tests;

public class ViewModelTests
{
    private static async Task<MainWindowViewModel> CreateViewModel(int count = 50)
    {
        var theme = new ThemeService();
        var viewModel = new MainWindowViewModel(new SampleCatalogSource(count), NullLogger<MainWindowViewModel>.Instance, theme);
        await viewModel.LoadAsync();
        return viewModel;
    }

    [Fact]
    public async Task Il_catalogo_parte_carico()
    {
        var viewModel = await CreateViewModel();
        Assert.Equal(50, viewModel.Rows.Count);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task La_ricerca_filtra_per_nome_e_canale()
    {
        var viewModel = await CreateViewModel();
        var primo = viewModel.Rows[0];

        var query = primo.Name[..8];
        viewModel.Search = query;
        Assert.NotEmpty(viewModel.Rows);
        Assert.All(viewModel.Rows, row => Assert.Contains(query, row.Name, StringComparison.OrdinalIgnoreCase));

        viewModel.Search = "Cinema Atlantide";
        Assert.NotEmpty(viewModel.Rows);
        Assert.All(viewModel.Rows, row => Assert.Equal("Cinema Atlantide", row.Channel));

        viewModel.Search = string.Empty;
        Assert.Equal(50, viewModel.Rows.Count);
    }

    [Fact]
    public async Task La_selezione_aggiorna_il_dettaglio()
    {
        var viewModel = await CreateViewModel();
        Assert.False(viewModel.HasSelection);

        var row = viewModel.Rows[3];
        viewModel.SelectedRow = row;

        Assert.True(viewModel.HasSelection);
        Assert.Equal(row.Name, viewModel.DetailTitle);
        Assert.Equal(row.Channel, viewModel.DetailSubtitle);
        Assert.Equal(row.SizeText, viewModel.DetailSize);
        Assert.Equal(row.KindLabel, viewModel.DetailKind);
    }

    [Fact]
    public async Task La_navigazione_marca_una_sola_voce()
    {
        var viewModel = await CreateViewModel();
        var voce = viewModel.Sections[0].Items[0];

        viewModel.SelectNavCommand.Execute(voce);

        Assert.True(voce.IsSelected);
        Assert.Single(viewModel.Sections.SelectMany(s => s.Items), i => i.IsSelected);
    }

    [Fact]
    public async Task Il_tema_alterna_chiaro_e_scuro()
    {
        var viewModel = await CreateViewModel();
        Assert.False(viewModel.IsLightTheme);

        viewModel.ToggleThemeCommand.Execute(null);
        Assert.True(viewModel.IsLightTheme);
    }

    [Fact]
    public void Il_formato_usa_unita_leggibili()
    {
        Assert.Equal("1,5 GB", Format.Size((long)(1.5 * 1024 * 1024 * 1024)).Replace(".", ","));
        Assert.Equal("512 B", Format.Size(512));
        Assert.Equal("—", Format.Size(0));
        Assert.Equal("1:05:00", Format.Duration(3900));
        Assert.Equal("2:30", Format.Duration(150));
    }
}
