using System.Collections.ObjectModel;
using System.Globalization;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Stop), "Stop")]
[QueryProperty(nameof(Line), "Line")]
[QueryProperty(nameof(DirectionId), "DirectionId")]
public partial class MapPage : ContentPage
{
    private StcpStop _stop = null!;
    private StcpLine _line = null!;
    private int _directionId = 0;
    
    private readonly TransitDataService _transitService = new();
    
    public ObservableCollection<BusArrival> Etas { get; set; } = new();

    public int DirectionId
    {
        get => _directionId;
        set
        {
            _directionId = value;
            OnPropertyChanged();
        }
    }

    public StcpStop Stop
    {
        get => _stop;
        set
        {
            _stop = value;
            OnPropertyChanged();
        }
    }

    public StcpLine Line
    {
        get => _line;
        set
        {
            _line = value;
            PageTitle = $"Linha {_line.LineNumber} - {_line.Name}";
            OnPropertyChanged(nameof(PageTitle));
        }
    }

    private bool _isLoadingEtas;
    public bool IsLoadingEtas
    {
        get => _isLoadingEtas;
        set { _isLoadingEtas = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasEtas)); OnPropertyChanged(nameof(IsEmpty)); }
    }

    public bool HasEtas => Etas.Count > 0 && !IsLoadingEtas;
    public bool IsEmpty => Etas.Count == 0 && !IsLoadingEtas;

    public string PageTitle { get; set; } = string.Empty;

    public MapPage()
    {
        InitializeComponent();
        BindingContext = this;
        MapWebView.Navigated += OnMapNavigated;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        if (MapWebView.Source == null)
        {
            _ = LoadLocalHtmlAsync();
        }
        
        _ = LoadEtasAsync();
    }

    private async Task LoadEtasAsync()
    {
        IsLoadingEtas = true;
        try
        {
            // Scrape actual ETAs for this stop code
            var arrivals = await _transitService.GetLiveArrivalsAsync(Stop.Code);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Etas.Clear();
                foreach (var a in arrivals)
                {
                    Etas.Add(a);
                }
                OnPropertyChanged(nameof(HasEtas));
                OnPropertyChanged(nameof(IsEmpty));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load ETAs: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsLoadingEtas = false);
        }
    }

    private async Task LoadLocalHtmlAsync()
    {
        using var stream = await FileSystem.Current.OpenAppPackageFileAsync("map.html");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        
        MapWebView.Source = new HtmlWebViewSource
        {
            Html = html
        };
    }

    private async void OnMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (Line != null)
        {
            // Draw Route Overlay for this direction
            var route = await _transitService.GetRouteShapeAsync(Line.LineNumber, DirectionId);
            if (route.Count > 0)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(route);
                await MapWebView.EvaluateJavaScriptAsync($"setBusRoute('{json}', '{Line.Color}');");
            }

            // Draw Stops for this direction
            var stops = await _transitService.GetLineStopsRawAsync(Line.LineNumber, DirectionId);
            if (stops.Count > 0)
            {
                // We map them to lowercase for the javascript
                var stopsJs = stops.Select(s => new { 
                    lat = s.Lat, 
                    lng = s.Lng, 
                    name = s.Name, 
                    code = s.Code 
                }).ToList();
                
                var stopsJson = System.Text.Json.JsonSerializer.Serialize(stopsJs);
                var selectedCode = Stop?.Code ?? "";
                await MapWebView.EvaluateJavaScriptAsync($"setBusStops('{stopsJson}', '{selectedCode}', '{Line.Color}');");
            }
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        if (Stop == null) return;
        var favId = $"stop:stcp:{Stop.Code}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var action = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync($"{Stop.Name} ({Stop.Code}) - STCP", "Cancelar", null, action);
        if (choice == action)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"Paragem {Stop.Name} removida dos favoritos.", "OK");
            }
            else
            {
                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Stop,
                    Mode = FavoriteTransportMode.Stcp,
                    Title = Stop.Name,
                    Subtitle = $"Código: {Stop.Code} • STCP",
                    BadgeText = Stop.Code,
                    ColorHex = "#28d8c3",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Stop)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Paragem {Stop.Name} adicionada aos favoritos!", "OK");
            }
        }
    }
}
