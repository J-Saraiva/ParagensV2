using System.Collections.ObjectModel;
using System.Windows.Input;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Station), "Station")]
public partial class MetroStationDeparturesPage : ContentPage
{
    private readonly MetroDataService _metroService = new();
    private MetroStation _station = new();

    public ObservableCollection<MetroDeparture> Departures { get; set; } = new();

    public MetroStation Station
    {
        get => _station;
        set
        {
            _station = value;
            StationName = _station.Name;
            StationZone = _station.Zone;
            StationLines = _station.LinesDisplay;
            OnPropertyChanged(nameof(StationName));
            OnPropertyChanged(nameof(StationZone));
            OnPropertyChanged(nameof(StationLines));
            OnPropertyChanged();
        }
    }

    public string StationName { get; set; } = string.Empty;
    public string StationZone { get; set; } = string.Empty;
    public string StationLines { get; set; } = string.Empty;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool IsEmpty => Departures.Count == 0 && !IsLoading;

    public ICommand RefreshCommand { get; }

    public MetroStationDeparturesPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadDeparturesAsync());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadDeparturesAsync();
    }

    private async Task LoadDeparturesAsync()
    {
        if (Station == null || string.IsNullOrWhiteSpace(Station.Id)) return;

        IsLoading = true;
        try
        {
            var results = await _metroService.GetStationDeparturesAsync(Station.Id);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Departures.Clear();
                foreach (var d in results)
                {
                    Departures.Add(d);
                }
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load Metro departures: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Station == null || string.IsNullOrWhiteSpace(Station.Id)) return;
        var favId = $"station:metro:{Station.Id}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"{StationName} - Metro do Porto", "Cancelar", null, action);
        if (choice == action)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"Estação {StationName} removida dos favoritos.", "OK");
            }
            else
            {
                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Station,
                    Mode = FavoriteTransportMode.Metro,
                    Title = StationName,
                    Subtitle = $"Zona {StationZone} • Linhas {StationLines} • Metro",
                    BadgeText = "🚇",
                    ColorHex = "#212e65",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Station)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Estação {StationName} adicionada aos favoritos!", "OK");
            }
        }
    }
}
