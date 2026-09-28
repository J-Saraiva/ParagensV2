using System.Text.Json;
using System.Web;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(FocusStationCode), "FocusStationCode")]
[QueryProperty(nameof(FocusStationName), "FocusStationName")]
[QueryProperty(nameof(TrainId), "TrainId")]
[QueryProperty(nameof(TrainNumber), "TrainNumber")]
public partial class TrainMapPage : ContentPage
{
    private readonly IPTrainService _trainService = new();
    private string _focusStationCode = string.Empty;
    private string _focusStationName = string.Empty;
    private string _trainId = string.Empty;
    private string _trainNumber = string.Empty;
    private bool _mapLoaded = false;

    public string FocusStationCode
    {
        get => _focusStationCode;
        set
        {
            _focusStationCode = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string FocusStationName
    {
        get => _focusStationName;
        set
        {
            _focusStationName = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    public string TrainId
    {
        get => _trainId;
        set
        {
            _trainId = value ?? string.Empty;
            UpdateHeader();
            OnPropertyChanged();
        }
    }

    public string TrainNumber
    {
        get => _trainNumber;
        set
        {
            _trainNumber = value ?? string.Empty;
            UpdateHeader();
            OnPropertyChanged();
        }
    }

    public string PageTitle { get; set; } = "🗺️ Mapa Ferroviário";
    public string PageSubtitle { get; set; } = "Estações de Portugal";
    public string PageDescription { get; set; } = "Toque num marcador para ver partidas em tempo real";

    public TrainMapPage()
    {
        InitializeComponent();
        BindingContext = this;
        TrainWebView.Navigated += OnMapNavigated;
        UpdateHeader();
    }

    private void UpdateHeader()
    {
        if (!string.IsNullOrEmpty(TrainId))
        {
            PageTitle = "🚆 Acompanhamento em Tempo Real";
            PageSubtitle = !string.IsNullOrEmpty(TrainNumber) ? $"Comboio {TrainNumber}" : "Rota do Comboio";
            PageDescription = "Última localização partilhada e trajeto previsto";
        }
        else
        {
            PageTitle = "🗺️ Mapa Ferroviário";
            PageSubtitle = "Estações de Portugal";
            PageDescription = "Toque num marcador para ver partidas em tempo real";
        }
        
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(PageSubtitle));
        OnPropertyChanged(nameof(PageDescription));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (TrainWebView.Source == null)
        {
            _ = LoadMapHtmlAsync();
        }
    }

    private async Task LoadMapHtmlAsync()
    {
        try
        {
            using var stream = await FileSystem.Current.OpenAppPackageFileAsync("train_map.html");
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TrainWebView.Source = new HtmlWebViewSource
                {
                    Html = html
                };
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load train_map.html: {ex.Message}");
        }
    }

    private async void OnMapNavigated(object? sender, WebNavigatedEventArgs e)
    {
        _mapLoaded = true;
        await PopulateStationsAsync();
    }

    private async Task PopulateStationsAsync()
    {
        if (!_mapLoaded) return;

        try
        {
            if (!string.IsNullOrEmpty(TrainId))
            {
                var rawJson = await _trainService.GetTrainDetailsRawAsync(TrainId);
                if (!string.IsNullOrEmpty(rawJson))
                {
                    await TrainWebView.EvaluateJavaScriptAsync($"showTrainRoute({rawJson});");
                    return;
                }
            }

            var stations = await _trainService.GetAllStationsAsync();
            if (stations.Count > 0)
            {
                var json = JsonSerializer.Serialize(stations.Select(s => new
                {
                    Code = s.Code,
                    Name = s.Name,
                    Lat = s.Lat,
                    Lon = s.Lon
                }));

                var focus = FocusStationCode ?? "";
                await TrainWebView.EvaluateJavaScriptAsync($"setTrainStations({json}, '{focus}');");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to render map data: {ex.Message}");
        }
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("paragens://station", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;

            try
            {
                var uri = new Uri(e.Url);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var code = query["code"] ?? "";
                var name = query["name"] ?? "Estação";

                var station = new TrainStation
                {
                    Code = code,
                    Name = name
                };

                var navParam = new Dictionary<string, object>
                {
                    { "Station", station }
                };

                await Shell.Current.GoToAsync(nameof(TrainStationDeparturesPage), navParam);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            }
        }
    }
}
