using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

public enum TransportMode
{
    Stcp,
    Unir,
    Metro,
    Train
}

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
    private readonly TransitDataService _transitService = new();
    private readonly UnirDataService _unirService = new();
    private readonly MetroDataService _metroService = new();
    private readonly IPTrainService _trainService = new();

    private List<StcpLine> _allLines = new();
    private List<UnirLine> _allUnirLines = new();
    private List<MetroLine> _allMetroLines = new();
    private List<MetroStation> _allMetroStations = new();
    private List<TrainStation> _allStations = new();

    public ObservableCollection<StcpLine> FilteredLines { get; set; } = new();
    public ObservableCollection<UnirLine> FilteredUnirLines { get; set; } = new();
    public ObservableCollection<MetroLine> FilteredMetroLines { get; set; } = new();
    public ObservableCollection<MetroStation> FilteredMetroStations { get; set; } = new();
    public ObservableCollection<TrainStation> FilteredStations { get; set; } = new();

    private TransportMode _currentMode = TransportMode.Stcp;
    public TransportMode CurrentMode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStcpMode));
            OnPropertyChanged(nameof(IsUnirMode));
            OnPropertyChanged(nameof(IsMetroMode));
            OnPropertyChanged(nameof(IsTrainMode));
            OnPropertyChanged(nameof(StcpTabBackground));
            OnPropertyChanged(nameof(StcpTabTextColor));
            OnPropertyChanged(nameof(UnirTabBackground));
            OnPropertyChanged(nameof(UnirTabTextColor));
            OnPropertyChanged(nameof(MetroTabBackground));
            OnPropertyChanged(nameof(MetroTabTextColor));
            OnPropertyChanged(nameof(TrainTabBackground));
            OnPropertyChanged(nameof(TrainTabTextColor));
            OnPropertyChanged(nameof(SearchPlaceholder));
            OnPropertyChanged(nameof(ShowMetroLines));
            OnPropertyChanged(nameof(ShowMetroStations));
            OnPropertyChanged(nameof(ShowMetroStations));
            ApplyFilter();
        }
    }

    private bool _isListViewVisible = true;
    public bool IsListViewVisible
    {
        get => _isListViewVisible;
        set
        {
            _isListViewVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMapViewVisible));
            OnPropertyChanged(nameof(ListViewTabColor));
            OnPropertyChanged(nameof(MapViewTabColor));
            OnPropertyChanged(nameof(ListViewTabOpacity));
            OnPropertyChanged(nameof(MapViewTabOpacity));
        }
    }
    public bool IsMapViewVisible => !IsListViewVisible;
    
    private bool _isLoadingLocation = false;
    public bool IsLoadingLocation
    {
        get => _isLoadingLocation;
        set { _isLoadingLocation = value; OnPropertyChanged(); }
    }

    public Color ListViewTabColor => IsListViewVisible ? Color.FromArgb("#4877ff") : Color.FromArgb("#64748B");
    public Color MapViewTabColor => !IsListViewVisible ? Color.FromArgb("#4877ff") : Color.FromArgb("#64748B");
    public double ListViewTabOpacity => IsListViewVisible ? 1.0 : 0.5;
    public double MapViewTabOpacity => !IsListViewVisible ? 1.0 : 0.5;

    public bool IsStcpMode => CurrentMode == TransportMode.Stcp;
    public bool IsUnirMode => CurrentMode == TransportMode.Unir;
    public bool IsMetroMode => CurrentMode == TransportMode.Metro;
    public bool IsTrainMode => CurrentMode == TransportMode.Train;

    // Metro sub-toggle: Lines vs Stations
    private bool _isMetroStationsView = false;
    public bool IsMetroStationsView
    {
        get => _isMetroStationsView;
        set
        {
            _isMetroStationsView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsMetroLinesView));
            OnPropertyChanged(nameof(ShowMetroLines));
            OnPropertyChanged(nameof(ShowMetroStations));
            OnPropertyChanged(nameof(MetroLinesPillBackground));
            OnPropertyChanged(nameof(MetroLinesPillTextColor));
            OnPropertyChanged(nameof(MetroStationsPillBackground));
            OnPropertyChanged(nameof(MetroStationsPillTextColor));
            ApplyFilter();
        }
    }

    public bool IsMetroLinesView => !IsMetroStationsView;
    public bool ShowMetroLines => IsMetroMode && !IsMetroStationsView;
    public bool ShowMetroStations => IsMetroMode && IsMetroStationsView;

    public Color StcpTabBackground => IsStcpMode ? Color.FromArgb("#28d8c3") : Color.FromArgb("#263238");
    public Color StcpTabTextColor => IsStcpMode ? Color.FromArgb("#00332C") : Color.FromArgb("#B0BEC5");

    public Color UnirTabBackground => IsUnirMode ? Color.FromArgb("#4877ff") : Color.FromArgb("#263238");
    public Color UnirTabTextColor => IsUnirMode ? Colors.White : Color.FromArgb("#B0BEC5");

    public Color MetroTabBackground => IsMetroMode ? Color.FromArgb("#212e65") : Color.FromArgb("#263238");
    public Color MetroTabTextColor => IsMetroMode ? Colors.White : Color.FromArgb("#B0BEC5");

    public Color TrainTabBackground => IsTrainMode ? Color.FromArgb("#1b652d") : Color.FromArgb("#263238");
    public Color TrainTabTextColor => IsTrainMode ? Colors.White : Color.FromArgb("#B0BEC5");

    private Color MetroUnselectedPillBg => Services.ThemeService.Instance.IsCurrentlyDark ? Color.FromArgb("#1E293B") : Color.FromArgb("#ECEFF1");
    private Color MetroUnselectedPillText => Services.ThemeService.Instance.IsCurrentlyDark ? Color.FromArgb("#94A3B8") : Color.FromArgb("#455A64");

    public Color MetroLinesPillBackground => IsMetroLinesView ? Color.FromArgb("#212e65") : MetroUnselectedPillBg;
    public Color MetroLinesPillTextColor => IsMetroLinesView ? Colors.White : MetroUnselectedPillText;

    public Color MetroStationsPillBackground => IsMetroStationsView ? Color.FromArgb("#212e65") : MetroUnselectedPillBg;
    public Color MetroStationsPillTextColor => IsMetroStationsView ? Colors.White : MetroUnselectedPillText;

    public string SearchPlaceholder => CurrentMode switch
    {
        TransportMode.Stcp => "Pesquisar STCP (ex: 600, 200, Maia)...",
        TransportMode.Unir => "Pesquisar UNIR (ex: 5002, 6001, Gondomar)...",
        TransportMode.Metro => IsMetroLinesView ? "Pesquisar linha (ex: D, Amarela, Dragão)..." : "Pesquisar estação (ex: Trindade, Bolhão)...",
        _ => "Pesquisar estação (ex: Campanhã, Bento, Braga)..."
    };

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }

    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public ICommand RefreshCommand { get; }

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        RefreshCommand = new Command(async () => await LoadDataAsync());
        Services.ThemeService.Instance.ThemeChanged += (s, e) => RefreshThemePillColors();
    }

    private void RefreshThemePillColors()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(MetroLinesPillBackground));
            OnPropertyChanged(nameof(MetroLinesPillTextColor));
            OnPropertyChanged(nameof(MetroStationsPillBackground));
            OnPropertyChanged(nameof(MetroStationsPillTextColor));
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_allLines.Count == 0 || _allUnirLines.Count == 0 || _allMetroLines.Count == 0 || _allStations.Count == 0)
        {
            IsRefreshing = true;
            RefreshCommand.Execute(null);
        }
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var linesTask = _transitService.GetAllLinesAsync();
            var unirTask = _unirService.GetAllLinesAsync();
            var metroLinesTask = _metroService.GetAllLinesAsync();
            var metroStationsTask = _metroService.GetAllStationsAsync();
            var stationsTask = _trainService.GetAllStationsAsync();

            _allLines = await linesTask;
            _allUnirLines = await unirTask;
            _allMetroLines = await metroLinesTask;
            _allMetroStations = await metroStationsTask;
            _allStations = await stationsTask;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load data: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                IsRefreshing = false;
            });
        }
    }

    private void OnSelectStcpMode(object? sender, EventArgs e) => CurrentMode = TransportMode.Stcp;
    private void OnSelectUnirMode(object? sender, EventArgs e) => CurrentMode = TransportMode.Unir;
    private void OnSelectMetroMode(object? sender, EventArgs e) => CurrentMode = TransportMode.Metro;
    private void OnSelectTrainMode(object? sender, EventArgs e) => CurrentMode = TransportMode.Train;

    private void OnSelectMetroLinesSubMode(object? sender, EventArgs e) => IsMetroStationsView = false;
    private void OnSelectMetroStationsSubMode(object? sender, EventArgs e) => IsMetroStationsView = true;

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? string.Empty;

        if (IsStcpMode)
        {
            FilteredLines.Clear();
            var results = string.IsNullOrWhiteSpace(query)
                ? _allLines
                : _allLines.Where(l =>
                    l.LineNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    l.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var line in results)
            {
                FilteredLines.Add(line);
            }
        }
        else if (IsUnirMode)
        {
            FilteredUnirLines.Clear();
            var results = string.IsNullOrWhiteSpace(query)
                ? _allUnirLines
                : _allUnirLines.Where(l =>
                    l.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    l.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    l.Municipality.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    l.Origin.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    l.Destination.Contains(query, StringComparison.OrdinalIgnoreCase));

            foreach (var line in results)
            {
                FilteredUnirLines.Add(line);
            }
        }
        else if (IsMetroMode)
        {
            if (IsMetroLinesView)
            {
                FilteredMetroLines.Clear();
                var results = string.IsNullOrWhiteSpace(query)
                    ? _allMetroLines
                    : _allMetroLines.Where(l =>
                        l.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.Origin.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        l.Destination.Contains(query, StringComparison.OrdinalIgnoreCase));

                foreach (var line in results)
                {
                    FilteredMetroLines.Add(line);
                }
            }
            else
            {
                FilteredMetroStations.Clear();
                var results = string.IsNullOrWhiteSpace(query)
                    ? _allMetroStations
                    : _allMetroStations.Where(s =>
                        s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        s.Zone.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        s.LinesDisplay.Contains(query, StringComparison.OrdinalIgnoreCase));

                foreach (var station in results)
                {
                    FilteredMetroStations.Add(station);
                }
            }
        }
        else
        {
            FilteredStations.Clear();
            var results = string.IsNullOrWhiteSpace(query)
                ? _allStations
                : _allStations.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.NodeId.ToString().Contains(query));

            foreach (var station in results)
            {
                FilteredStations.Add(station);
            }
        }
    }

    private async void OnLineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StcpLine line)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            var navigationParameter = new Dictionary<string, object> { { "Line", line } };
            await Shell.Current.GoToAsync(nameof(LineStopsPage), navigationParameter);
        }
    }

    private async void OnUnirLineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is UnirLine line)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            var navigationParameter = new Dictionary<string, object> { { "Line", line } };
            await Shell.Current.GoToAsync(nameof(UnirLineStopsPage), navigationParameter);
        }
    }

    private async void OnMetroLineSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MetroLine line)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            var navigationParameter = new Dictionary<string, object> { { "Line", line } };
            await Shell.Current.GoToAsync(nameof(MetroLineStopsPage), navigationParameter);
        }
    }

    private async void OnMetroStationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is MetroStation station)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            var navigationParameter = new Dictionary<string, object> { { "Station", station } };
            await Shell.Current.GoToAsync(nameof(MetroStationDeparturesPage), navigationParameter);
        }
    }

    private async void OnStationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is TrainStation station)
        {
            if (sender is CollectionView cv) cv.SelectedItem = null;
            var navigationParameter = new Dictionary<string, object> { { "Station", station } };
            await Shell.Current.GoToAsync(nameof(TrainStationDeparturesPage), navigationParameter);
        }
    }

    private async void OnOpenTrainMap(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(TrainMapPage));
    }

    private async void OnNavigateToHome(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HomePage");
    }

    private void OnOpenMenu(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private void OnShowListView(object? sender, EventArgs e) => IsListViewVisible = true;

    private async void OnShowMapView(object? sender, EventArgs e)
    {
        IsListViewVisible = false;
        
        // Initialize Map if not already done
        if (NearbyMapWebView.Source == null)
        {
            await LoadMapHtmlAsync();
        }
        else
        {
            await CenterOnUserLocationAsync();
        }
    }

    private async Task LoadMapHtmlAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("nearby_map.html");
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();
            NearbyMapWebView.Source = new HtmlWebViewSource { Html = html };
            
            // Wait for WebView to render
            await Task.Delay(1500); 
            await CenterOnUserLocationAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading nearby map HTML: {ex.Message}");
        }
    }

    private async void OnCenterLocationClicked(object? sender, EventArgs e)
    {
        await CenterOnUserLocationAsync();
    }

    private async Task CenterOnUserLocationAsync()
    {
        if (IsLoadingLocation) return;
        
        try
        {
            IsLoadingLocation = true;
            
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                var location = await Geolocation.Default.GetLocationAsync(request);

                if (location != null && NearbyMapWebView.Source != null)
                {
                    await NearbyMapWebView.EvaluateJavaScriptAsync($"setUserLocation({location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                }
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Permissão Negada", "Precisamos de aceder à localização para te centrar no mapa.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting location: {ex.Message}");
            if (Application.Current?.MainPage != null)
                await Application.Current.MainPage.DisplayAlert("Erro", "Não foi possível obter a localização atual.", "OK");
        }
        finally
        {
            IsLoadingLocation = false;
        }
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url != null && e.Url.StartsWith("paragens://stop"))
        {
            e.Cancel = true;
            try
            {
                var uri = new Uri(e.Url);
                
                var queryDict = new Dictionary<string, string>();
                var q = uri.Query.TrimStart('?');
                foreach (var pair in q.Split('&'))
                {
                    var parts = pair.Split('=');
                    if (parts.Length == 2)
                    {
                        queryDict[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1]);
                    }
                }
                
                var stopCode = queryDict.TryGetValue("id", out var id) ? id : "";
                var stopName = queryDict.TryGetValue("name", out var name) ? name : "";
                var type = queryDict.TryGetValue("type", out var t) ? t : "";

                if (string.IsNullOrEmpty(stopCode)) return;

                if (type == "BUS")
                {
                    var stop = new UnirStop { Code = stopCode, Name = stopName, Zone = "" };
                    await Shell.Current.GoToAsync(nameof(UnirStopDeparturesPage), new Dictionary<string, object>
                    {
                        { "Stop", stop },
                        { "StopCode", stopCode },
                        { "StopName", stopName },
                        { "StopZone", "" }
                    });
                }
                else if (type == "TRAIN")
                {
                    var stop = new TrainStation { Code = stopCode, Name = stopName };
                    await Shell.Current.GoToAsync(nameof(TrainStationDeparturesPage), new Dictionary<string, object> { { "Station", stop } });
                }
                else if (type == "METRO")
                {
                    var stop = new MetroStation { Id = stopCode, Name = stopName };
                    await Shell.Current.GoToAsync(nameof(MetroStationDeparturesPage), new Dictionary<string, object> { { "Station", stop } });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing map click: {ex.Message}");
            }
        }
    }
}
