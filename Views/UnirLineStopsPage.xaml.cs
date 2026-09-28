using System.Collections.ObjectModel;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Line), "Line")]
public partial class UnirLineStopsPage : ContentPage
{
    private readonly UnirDataService _unirService = new();
    private UnirLine _line = new();
    private int _directionId = 1;

    public ObservableCollection<UnirStop> Stops { get; set; } = new();

    public UnirLine Line
    {
        get => _line;
        set
        {
            _line = value;
            if (_line != null)
            {
                _line.Name = UnirDataService.CleanMojibake(_line.Name);
                _line.Origin = UnirDataService.CleanMojibake(_line.Origin);
                _line.Destination = UnirDataService.CleanMojibake(_line.Destination);
                _line.Municipality = UnirDataService.CleanMojibake(_line.Municipality);
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(DirectionStatusText));
            _ = LoadStopsAsync();
        }
    }

    public int DirectionId
    {
        get => _directionId;
        set
        {
            _directionId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DirectionStatusText));
        }
    }

    public string DirectionStatusText
    {
        get
        {
            if (Line == null) return "Sentido: Ida";
            var origin = !string.IsNullOrEmpty(Line.Origin) ? Line.Origin : "Origem";
            var dest = !string.IsNullOrEmpty(Line.Destination) ? Line.Destination : "Destino";

            return DirectionId == 1 
                ? $"Sentido: IDA ({origin} ➔ {dest})" 
                : $"Sentido: VOLTA ({dest} ➔ {origin})";
        }
    }

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

    public bool IsEmpty => Stops.Count == 0 && !IsLoading;

    public UnirLineStopsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async Task LoadStopsAsync()
    {
        if (Line == null || string.IsNullOrWhiteSpace(Line.Code)) return;

        IsLoading = true;
        try
        {
            var results = await _unirService.GetStopsForLineAsync(Line.Code, DirectionId, Line.Name);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Stops.Clear();
                foreach (var s in results)
                {
                    Stops.Add(s);
                }
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading stops for UNIR line {Line.Code}: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async void OnToggleDirection(object? sender, EventArgs e)
    {
        DirectionId = DirectionId == 1 ? 2 : 1;
        await LoadStopsAsync();
    }

    private async void OnStopSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is UnirStop stop)
        {
            if (sender is CollectionView cv)
            {
                cv.SelectedItem = null;
            }

            var navParam = new Dictionary<string, object>
            {
                { "Stop", stop },
                { "StopCode", stop.Code },
                { "StopName", stop.Name },
                { "StopZone", stop.Zone }
            };
            await Shell.Current.GoToAsync(nameof(UnirStopDeparturesPage), navParam);
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Line == null) return;
        var favId = $"line:unir:{Line.Code}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"Linha {Line.Code} - UNIR", "Cancelar", null, action);
        if (choice == action)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"Linha {Line.Code} removida dos favoritos.", "OK");
            }
            else
            {
                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Line,
                    Mode = FavoriteTransportMode.Unir,
                    Title = $"Linha {Line.Code}",
                    Subtitle = Line.Name,
                    BadgeText = Line.Code,
                    ColorHex = "#4877ff",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Line)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Linha {Line.Code} adicionada aos favoritos!", "OK");
            }
        }
    }
}
