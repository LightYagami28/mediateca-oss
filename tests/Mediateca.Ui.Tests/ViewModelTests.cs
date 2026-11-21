using Mediateca.Core.Domain;
using Mediateca.Data;
using Mediateca.Ui.Models;
using Mediateca.Ui.Services;
using Mediateca.Ui.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mediateca.Ui.Tests;

public class ViewModelTests
{
    private static async Task<CatalogViewModel> CreateCatalog(int count = 50, int debounceMs = 0, ILogger<CatalogViewModel>? logger = null)
    {
        var viewModel = new CatalogViewModel(new SampleCatalogSource(count), logger ?? NullLogger<CatalogViewModel>.Instance, debounceMs);
        await viewModel.LoadAsync();
        return viewModel;
    }

    [Fact]
    public async Task Il_catalogo_parte_carico()
    {
        var viewModel = await CreateCatalog();
        Assert.Equal(50, viewModel.Rows.Count);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task La_selezione_aggiorna_il_dettaglio()
    {
        var viewModel = await CreateCatalog();
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
    public async Task La_ricerca_debounced_filtra_per_nome()
    {
        var viewModel = await CreateCatalog(200);
        var target = viewModel.Rows[7];

        viewModel.Search = target.Name[..10];
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.NotEmpty(viewModel.Rows);
        Assert.All(viewModel.Rows, row => Assert.Contains(target.Name[..10], row.Name, StringComparison.OrdinalIgnoreCase));

        viewModel.Search = string.Empty;
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(200, viewModel.Rows.Count);
    }

    [Fact]
    public async Task Il_caricamento_scrive_il_log_quando_il_livello_e_attivo()
    {
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var viewModel = await CreateCatalog(logger: factory.CreateLogger<CatalogViewModel>());
        Assert.Equal(50, viewModel.Rows.Count);
    }

    [Fact]
    public async Task La_ricerca_annullata_lascia_invariato_il_catalogo()
    {
        using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
        var viewModel = await CreateCatalog(debounceMs: 200, logger: factory.CreateLogger<CatalogViewModel>());

        viewModel.Search = "a";
        viewModel.Search = string.Empty;
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.False(viewModel.IsSearching);
        Assert.Equal(50, viewModel.Rows.Count);
    }

    [Fact]
    public async Task I_canali_caricano_le_righe_con_log_attivo()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var database = new MediatecaDatabase(Path.Combine(directory, "mediateca.db"));
            database.Migrate();
            new SqliteMediaStore(database).UpsertChannel(new Channel { Id = 1, Title = "Canale", IsSource = true });

            using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Debug));
            var viewModel = new ChannelsViewModel(database, factory.CreateLogger<ChannelsViewModel>());

            await viewModel.LoadAsync(TestContext.Current.CancellationToken);

            Assert.Single(viewModel.Rows);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    break;
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
            }
        }
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
