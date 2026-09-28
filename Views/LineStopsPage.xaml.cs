using System.Collections.ObjectModel;
using System.ComponentModel;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Line), "Line")]
public partial class LineStopsPage : ContentPage, INotifyPropertyChanged
{
    private readonly TransitDataService _transitService = new();
    private StcpLine _line = null!;
    private int _directionId = 0; // 0 = Ida, 1 = Volta
    private bool _isLoading;

    public ObservableCollection<StcpStop> Stops { get; set; } = new();

    public StcpLine Line
    {
        get => _line;
        set
        {
            _line = value;
            OnPropertyChanged();
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
            var parts = Line.Name.Split('-', StringSplitOptions.TrimEntries);
            var origin = parts.Length > 0 ? parts[0] : "Origem";
            var dest = parts.Length > 1 ? parts[1] : "Destino";

            return DirectionId == 0 
                ? $"Sentido: IDA ({origin} ➔ {dest})" 
                : $"Sentido: VOLTA ({dest} ➔ {origin})";
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public LineStopsPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void OnToggleDirection(object? sender, EventArgs e)
    {
        DirectionId = (DirectionId == 0) ? 1 : 0;
        await LoadStopsAsync();
    }

    private async Task LoadStopsAsync()
    {
        if (Line == null) return;

        IsLoading = true;
        try
        {
            var stops = await _transitService.GetLineStopsRawAsync(Line.LineNumber, DirectionId);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Stops.Clear();
                foreach (var s in stops)
                {
                    Stops.Add(s);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading stops for line {Line.LineNumber}: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
        }
    }

    private async void OnStopSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StcpStop stop)
        {
            if (sender is CollectionView cv)
            {
                cv.SelectedItem = null;
            }

            var navigationParameter = new Dictionary<string, object>
            {
                { "Stop", stop },
                { "Line", Line },
                { "DirectionId", DirectionId }
            };

            await Shell.Current.GoToAsync(nameof(MapPage), navigationParameter);
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Line == null) return;
        var favId = $"line:stcp:{Line.LineNumber}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"Linha {Line.LineNumber} - STCP", "Cancelar", null, action);
        if (choice == action)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"Linha {Line.LineNumber} removida dos favoritos.", "OK");
            }
            else
            {
                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Line,
                    Mode = FavoriteTransportMode.Stcp,
                    Title = $"Linha {Line.LineNumber}",
                    Subtitle = Line.Name,
                    BadgeText = Line.LineNumber,
                    ColorHex = "#28d8c3",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Line)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Linha {Line.LineNumber} adicionada aos favoritos!", "OK");
            }
        }
    }
}
