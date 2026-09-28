using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

public enum HomeFavoritesFilter
{
    All,
    Lines,
    Stops,
    Stations
}

public partial class HomePage : ContentPage
{
    private readonly FavoritesService _favoritesService = FavoritesService.Instance;
    private HomeFavoritesFilter _currentFilter = HomeFavoritesFilter.All;
    private bool _isRefreshing;
    private List<FavoriteItem> _allFavorites = new();

    public ObservableCollection<FavoriteItem> FilteredFavorites { get; set; } = new();

    public ICommand RefreshCommand { get; }

    public HomeFavoritesFilter CurrentFilter
    {
        get => _currentFilter;
        set
        {
            _currentFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AllPillBackground));
            OnPropertyChanged(nameof(AllPillTextColor));
            OnPropertyChanged(nameof(LinesPillBackground));
            OnPropertyChanged(nameof(LinesPillTextColor));
            OnPropertyChanged(nameof(StopsPillBackground));
            OnPropertyChanged(nameof(StopsPillTextColor));
            OnPropertyChanged(nameof(StationsPillBackground));
            OnPropertyChanged(nameof(StationsPillTextColor));
            ApplyFilter();
        }
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set
        {
            _isRefreshing = value;
            OnPropertyChanged();
        }
    }

    public bool HasFavorites => FilteredFavorites.Count > 0;
    public bool IsEmpty => FilteredFavorites.Count == 0;

    // Filter pill labels with counts
    public string AllPillText => $"⭐ Todas ({_allFavorites.Count})";
    public string LinesPillText => $"🚌 Linhas ({_allFavorites.Count(f => f.Type == FavoriteType.Line)})";
    public string StopsPillText => $"🚏 Paragens ({_allFavorites.Count(f => f.Type == FavoriteType.Stop)})";
    public string StationsPillText => $"🚉 Estações ({_allFavorites.Count(f => f.Type == FavoriteType.Station)})";

    // Filter pill styling
    private Color UnselectedPillBg => ThemeService.Instance.IsCurrentlyDark ? Color.FromArgb("#1E293B") : Color.FromArgb("#ECEFF1");
    private Color UnselectedPillText => ThemeService.Instance.IsCurrentlyDark ? Color.FromArgb("#94A3B8") : Color.FromArgb("#455A64");

    public Color AllPillBackground => CurrentFilter == HomeFavoritesFilter.All ? Color.FromArgb("#1A237E") : UnselectedPillBg;
    public Color AllPillTextColor => CurrentFilter == HomeFavoritesFilter.All ? Colors.White : UnselectedPillText;

    public Color LinesPillBackground => CurrentFilter == HomeFavoritesFilter.Lines ? Color.FromArgb("#1A237E") : UnselectedPillBg;
    public Color LinesPillTextColor => CurrentFilter == HomeFavoritesFilter.Lines ? Colors.White : UnselectedPillText;

    public Color StopsPillBackground => CurrentFilter == HomeFavoritesFilter.Stops ? Color.FromArgb("#1A237E") : UnselectedPillBg;
    public Color StopsPillTextColor => CurrentFilter == HomeFavoritesFilter.Stops ? Colors.White : UnselectedPillText;

    public Color StationsPillBackground => CurrentFilter == HomeFavoritesFilter.Stations ? Color.FromArgb("#1A237E") : UnselectedPillBg;
    public Color StationsPillTextColor => CurrentFilter == HomeFavoritesFilter.Stations ? Colors.White : UnselectedPillText;

    public HomePage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadFavoritesAsync());
        ThemeService.Instance.ThemeChanged += (s, e) => RefreshPillStyles();
    }

    private void RefreshPillStyles()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(AllPillBackground));
            OnPropertyChanged(nameof(AllPillTextColor));
            OnPropertyChanged(nameof(LinesPillBackground));
            OnPropertyChanged(nameof(LinesPillTextColor));
            OnPropertyChanged(nameof(StopsPillBackground));
            OnPropertyChanged(nameof(StopsPillTextColor));
            OnPropertyChanged(nameof(StationsPillBackground));
            OnPropertyChanged(nameof(StationsPillTextColor));
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _favoritesService.FavoritesChanged += OnFavoritesChanged;
        _ = LoadFavoritesAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _favoritesService.FavoritesChanged -= OnFavoritesChanged;
    }

    private void OnFavoritesChanged(object? sender, EventArgs e)
    {
        _ = LoadFavoritesAsync();
    }

    private async Task LoadFavoritesAsync()
    {
        IsRefreshing = true;
        try
        {
            _allFavorites = await _favoritesService.GetFavoritesAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                OnPropertyChanged(nameof(AllPillText));
                OnPropertyChanged(nameof(LinesPillText));
                OnPropertyChanged(nameof(StopsPillText));
                OnPropertyChanged(nameof(StationsPillText));
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading favorites: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => IsRefreshing = false);
        }
    }

    private void ApplyFilter()
    {
        var items = CurrentFilter switch
        {
            HomeFavoritesFilter.Lines => _allFavorites.Where(f => f.Type == FavoriteType.Line),
            HomeFavoritesFilter.Stops => _allFavorites.Where(f => f.Type == FavoriteType.Stop),
            HomeFavoritesFilter.Stations => _allFavorites.Where(f => f.Type == FavoriteType.Station),
            _ => _allFavorites.AsEnumerable()
        };

        FilteredFavorites.Clear();
        foreach (var item in items)
        {
            FilteredFavorites.Add(item);
        }

        OnPropertyChanged(nameof(HasFavorites));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnSelectAllFilter(object? sender, EventArgs e) => CurrentFilter = HomeFavoritesFilter.All;
    private void OnSelectLinesFilter(object? sender, EventArgs e) => CurrentFilter = HomeFavoritesFilter.Lines;
    private void OnSelectStopsFilter(object? sender, EventArgs e) => CurrentFilter = HomeFavoritesFilter.Stops;
    private void OnSelectStationsFilter(object? sender, EventArgs e) => CurrentFilter = HomeFavoritesFilter.Stations;



    private async void OnNavigateToExplore(object? sender, EventArgs e)
    {
        // Navigate to the Explore / MainPage
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnFavoriteItemMenuClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is FavoriteItem item)
        {
            var choice = await DisplayActionSheetAsync(
                title: item.Title,
                cancel: "Cancelar",
                destruction: "Remover dos Favoritos",
                buttons: new[] { "Ver Detalhes" });

            if (choice == "Remover dos Favoritos")
            {
                await _favoritesService.RemoveFavoriteAsync(item.Id);
            }
            else if (choice == "Ver Detalhes")
            {
                await NavigateToFavoriteItemAsync(item);
            }
        }
    }

    private async void OnFavoriteSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FavoriteItem item)
        {
            if (sender is CollectionView cv)
            {
                cv.SelectedItem = null;
            }

            await NavigateToFavoriteItemAsync(item);
        }
    }

    private async Task NavigateToFavoriteItemAsync(FavoriteItem item)
    {
        try
        {
            switch (item.Type)
            {
                case FavoriteType.Line:
                    if (item.Mode == FavoriteTransportMode.Stcp)
                    {
                        var line = JsonSerializer.Deserialize<StcpLine>(item.PayloadJson);
                        if (line != null)
                        {
                            await Shell.Current.GoToAsync(nameof(LineStopsPage), new Dictionary<string, object> { { "Line", line } });
                        }
                    }
                    else if (item.Mode == FavoriteTransportMode.Unir)
                    {
                        var line = JsonSerializer.Deserialize<UnirLine>(item.PayloadJson);
                        if (line != null)
                        {
                            await Shell.Current.GoToAsync(nameof(UnirLineStopsPage), new Dictionary<string, object> { { "Line", line } });
                        }
                    }
                    else if (item.Mode == FavoriteTransportMode.Metro)
                    {
                        var line = JsonSerializer.Deserialize<MetroLine>(item.PayloadJson);
                        if (line != null)
                        {
                            await Shell.Current.GoToAsync(nameof(MetroLineStopsPage), new Dictionary<string, object> { { "Line", line } });
                        }
                    }
                    break;

                case FavoriteType.Stop:
                    if (item.Mode == FavoriteTransportMode.Stcp)
                    {
                        var stop = JsonSerializer.Deserialize<StcpStop>(item.PayloadJson);
                        if (stop != null)
                        {
                            await Shell.Current.GoToAsync(nameof(MapPage), new Dictionary<string, object> { { "Stop", stop } });
                        }
                    }
                    else if (item.Mode == FavoriteTransportMode.Unir)
                    {
                        var stop = JsonSerializer.Deserialize<UnirStop>(item.PayloadJson);
                        if (stop != null)
                        {
                            await Shell.Current.GoToAsync(nameof(UnirStopDeparturesPage), new Dictionary<string, object>
                            {
                                { "Stop", stop },
                                { "StopCode", stop.Code },
                                { "StopName", stop.Name },
                                { "StopZone", stop.Zone }
                            });
                        }
                    }
                    break;

                case FavoriteType.Station:
                    if (item.Mode == FavoriteTransportMode.Metro)
                    {
                        var station = JsonSerializer.Deserialize<MetroStation>(item.PayloadJson);
                        if (station != null)
                        {
                            await Shell.Current.GoToAsync(nameof(MetroStationDeparturesPage), new Dictionary<string, object> { { "Station", station } });
                        }
                    }
                    else if (item.Mode == FavoriteTransportMode.Train)
                    {
                        var station = JsonSerializer.Deserialize<TrainStation>(item.PayloadJson);
                        if (station != null)
                        {
                            await Shell.Current.GoToAsync(nameof(TrainStationDeparturesPage), new Dictionary<string, object> { { "Station", station } });
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", $"Não foi possível abrir o favorito: {ex.Message}", "OK");
        }
    }
}
