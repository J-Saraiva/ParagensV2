using System.Collections.ObjectModel;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Line), "Line")]
public partial class MetroLineStopsPage : ContentPage
{
    private readonly MetroDataService _metroService = new();
    private MetroLine _line = new();
    private int _directionId = 0;

    public ObservableCollection<MetroStation> Stops { get; set; } = new();

    public MetroLine Line
    {
        get => _line;
        set
        {
            _line = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineColor));
            OnPropertyChanged(nameof(DirectionStatusText));
            _ = LoadStopsAsync();
        }
    }

    public Color LineColor
    {
        get
        {
            try
            {
                return Color.FromArgb(Line?.ColorHex ?? "#199FDA");
            }
            catch
            {
                return Color.FromArgb("#199FDA");
            }
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
            var headsign = DirectionId == 0 
                ? Line.Direction0?.Headsign 
                : Line.Direction1?.Headsign;

            if (string.IsNullOrEmpty(headsign))
            {
                headsign = DirectionId == 0 ? Line.Destination : Line.Origin;
            }

            return DirectionId == 0 
                ? $"Sentido: {headsign}" 
                : $"Sentido: {headsign}";
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

    public MetroLineStopsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async Task LoadStopsAsync()
    {
        if (Line == null || string.IsNullOrWhiteSpace(Line.Id)) return;

        IsLoading = true;
        try
        {
            var results = await _metroService.GetStopsForLineAsync(Line.Id, DirectionId);
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
            System.Diagnostics.Debug.WriteLine($"Error loading stops for Metro line {Line.Id}: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async void OnToggleDirection(object? sender, EventArgs e)
    {
        DirectionId = DirectionId == 0 ? 1 : 0;
        await LoadStopsAsync();
    }

    private async void OnStationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MetroStation station)
        {
            if (sender is CollectionView cv)
            {
                cv.SelectedItem = null;
            }

            var navParam = new Dictionary<string, object>
            {
                { "Station", station }
            };
            await Shell.Current.GoToAsync(nameof(MetroStationDeparturesPage), navParam);
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Line == null) return;
        var favId = $"line:metro:{Line.Id}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"{Line.Name} - Metro do Porto", "Cancelar", null, action);
        if (choice == action)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"{Line.Name} removida dos favoritos.", "OK");
            }
            else
            {
                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Line,
                    Mode = FavoriteTransportMode.Metro,
                    Title = Line.Name,
                    Subtitle = Line.RouteDescription,
                    BadgeText = Line.BadgeText,
                    ColorHex = Line.ColorHex,
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Line)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"{Line.Name} adicionada aos favoritos!", "OK");
            }
        }
    }
}
