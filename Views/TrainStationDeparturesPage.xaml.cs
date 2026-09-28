using System.Collections.ObjectModel;
using System.Windows.Input;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Station), "Station")]
public partial class TrainStationDeparturesPage : ContentPage
{
    private TrainStation _station = new();
    private readonly IPTrainService _trainService = new();

    public ObservableCollection<TrainDeparture> Departures { get; set; } = new();

    public TrainStation Station
    {
        get => _station;
        set
        {
            _station = value;
            StationName = _station.Name;
            StationCode = !string.IsNullOrEmpty(_station.Code) ? _station.Code : _station.NodeId.ToString();
            OnPropertyChanged(nameof(StationName));
            OnPropertyChanged(nameof(StationCode));
            OnPropertyChanged();
        }
    }

    public string StationName { get; set; } = string.Empty;
    public string StationCode { get; set; } = string.Empty;

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

    public TrainStationDeparturesPage()
    {
        InitializeComponent();
        RefreshCommand = new Command(async () => await LoadDeparturesAsync());
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadDeparturesAsync();
    }

    private async Task LoadDeparturesAsync()
    {
        if (Station == null) return;

        var lookupKey = !string.IsNullOrEmpty(Station.Code) ? Station.Code : Station.NodeId.ToString();
        if (string.IsNullOrEmpty(lookupKey) || lookupKey == "0") return;

        IsLoading = true;
        try
        {
            var results = await _trainService.GetStationDeparturesAsync(lookupKey);
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
            System.Diagnostics.Debug.WriteLine($"Failed to load train departures: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async void OnOpenInMap(object? sender, EventArgs e)
    {
        var navParam = new Dictionary<string, object>
        {
            { "FocusStationCode", Station.Code },
            { "FocusStationName", Station.Name }
        };
        await Shell.Current.GoToAsync(nameof(TrainMapPage), navParam);
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Station == null || string.IsNullOrWhiteSpace(Station.Code)) return;
        var favId = $"station:train:{Station.Code}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"{StationName} - CP Comboios", "Cancelar", null, action);
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
                    Mode = FavoriteTransportMode.Train,
                    Title = StationName,
                    Subtitle = $"Código {StationCode} • CP Comboios de Portugal",
                    BadgeText = "🚆",
                    ColorHex = "#1b652d",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Station)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Estação {StationName} adicionada aos favoritos!", "OK");
            }
        }
    }
    private async void OnDepartureSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TrainDeparture departure)
        {
            var navParam = new Dictionary<string, object>
            {
                { "TrainId", departure.TrainId },
                { "TrainNumber", departure.TrainNumber }
            };
            
            // Clear selection
            if (sender is CollectionView cv) cv.SelectedItem = null;
            
            await Shell.Current.GoToAsync(nameof(TrainMapPage), navParam);
        }
    }
}
