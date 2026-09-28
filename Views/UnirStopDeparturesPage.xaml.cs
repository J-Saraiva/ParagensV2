using System.Collections.ObjectModel;
using System.Windows.Input;
using ParagensV2.Models;
using ParagensV2.Services;

namespace ParagensV2.Views;

[QueryProperty(nameof(Stop), "Stop")]
[QueryProperty(nameof(StopCode), "StopCode")]
[QueryProperty(nameof(StopName), "StopName")]
[QueryProperty(nameof(StopZone), "StopZone")]
public partial class UnirStopDeparturesPage : ContentPage
{
    private readonly UnirDataService _unirService = new();
    private UnirStop _stop = new();
    private IDispatcherTimer? _refreshTimer;

    public ObservableCollection<UnirDeparture> Departures { get; set; } = new();

    public UnirStop Stop
    {
        get => _stop;
        set
        {
            _stop = value ?? new UnirStop();
            if (!string.IsNullOrEmpty(_stop.Name))
                StopName = UnirDataService.CleanMojibake(_stop.Name);
            if (!string.IsNullOrEmpty(_stop.Code))
                StopCode = _stop.Code;
            if (!string.IsNullOrEmpty(_stop.Zone))
                StopZone = _stop.Zone;

            OnPropertyChanged(nameof(StopName));
            OnPropertyChanged(nameof(StopCode));
            OnPropertyChanged(nameof(StopZone));
            OnPropertyChanged();
            _ = LoadDeparturesAsync();
        }
    }

    private string _stopName = string.Empty;
    public string StopName
    {
        get => _stopName;
        set
        {
            _stopName = UnirDataService.CleanMojibake(value);
            if (_stop != null && string.IsNullOrEmpty(_stop.Name))
                _stop.Name = _stopName;
            OnPropertyChanged();
        }
    }

    private string _stopCode = string.Empty;
    public string StopCode
    {
        get => _stopCode;
        set
        {
            _stopCode = value ?? string.Empty;
            if (_stop != null && string.IsNullOrEmpty(_stop.Code))
                _stop.Code = _stopCode;
            OnPropertyChanged();
            _ = LoadDeparturesAsync();
        }
    }

    private string _stopZone = string.Empty;
    public string StopZone
    {
        get => _stopZone;
        set
        {
            _stopZone = value ?? string.Empty;
            if (_stop != null && string.IsNullOrEmpty(_stop.Zone))
                _stop.Zone = _stopZone;
            OnPropertyChanged();
        }
    }

    private string _lastUpdatedText = "A carregar previsões...";
    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        set
        {
            _lastUpdatedText = value;
            OnPropertyChanged();
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

    private bool _showScheduledWarning;
    public bool ShowScheduledWarning
    {
        get => _showScheduledWarning;
        set
        {
            _showScheduledWarning = value;
            OnPropertyChanged();
        }
    }

    public bool IsEmpty => Departures.Count == 0 && !IsLoading;

    public ICommand RefreshCommand { get; }

    public UnirStopDeparturesPage()
    {
        InitializeComponent();
        BindingContext = this;
        RefreshCommand = new Command(async () => await LoadDeparturesAsync());
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadDeparturesAsync();
        StartAutoRefresh();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        _refreshTimer = Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(30);
        _refreshTimer.Tick += async (s, e) =>
        {
            if (!IsLoading)
            {
                await LoadDeparturesAsync(silent: true);
            }
        };
        _refreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
        }
    }

    private async Task LoadDeparturesAsync(bool silent = false)
    {
        var effectiveCode = !string.IsNullOrWhiteSpace(StopCode) ? StopCode : Stop?.Code;
        if (string.IsNullOrWhiteSpace(effectiveCode)) return;

        if (!silent)
        {
            IsLoading = true;
        }

        try
        {
            var results = await _unirService.GetStopDeparturesAsync(effectiveCode);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Departures.Clear();
                foreach (var d in results)
                {
                    Departures.Add(d);
                }
                var hasRealtime = results.Any(d => d.IsRealtime);
                ShowScheduledWarning = !hasRealtime && results.Count > 0;
                LastUpdatedText = hasRealtime
                    ? $"⚡ GPS Moovit Ativo • Atualizado às {DateTime.Now:HH:mm:ss}"
                    : $"🕒 Horário Oficial • Atualizado às {DateTime.Now:HH:mm:ss}";
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(LastUpdatedText));
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load UNIR departures for {effectiveCode}: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LastUpdatedText = "⚠️ Falha ao atualizar horários";
                OnPropertyChanged(nameof(LastUpdatedText));
            });
        }
        finally
        {
            if (!silent)
            {
                MainThread.BeginInvokeOnMainThread(() => IsLoading = false);
            }
        }
    }

    private async void OnInfoBannerTapped(object? sender, EventArgs e)
    {
        await PromptOpenExternalTrackerAsync();
    }

    private async Task PromptOpenExternalTrackerAsync()
    {
        var choice = await DisplayActionSheetAsync(
            "Consultar Trânsito e GPS em Direto",
            "Cancelar",
            null,
            "📱 Abrir no Moovit",
            "🗺️ Abrir no Google Maps");

        if (choice == "📱 Abrir no Moovit")
        {
            await OpenMoovitAsync();
        }
        else if (choice == "🗺️ Abrir no Google Maps")
        {
            await OpenGoogleMapsAsync();
        }
    }

    private async Task OpenMoovitAsync()
    {
        try
        {
            string url;
            if (Stop != null && Stop.Lat != 0 && Stop.Lng != 0)
            {
                var latStr = Stop.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lngStr = Stop.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
                url = $"https://moovitapp.com/?to={latStr}_{lngStr}&metroId=4161";
            }
            else
            {
                url = "https://moovitapp.com/index/pt/transporte_p%C3%BAblico-Porto_e_Braga-4161";
            }
            await Launcher.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", "Não foi possível abrir o Moovit: " + ex.Message, "OK");
        }
    }

    private async Task OpenGoogleMapsAsync()
    {
        try
        {
            string url;
            if (Stop != null && Stop.Lat != 0 && Stop.Lng != 0)
            {
                var latStr = Stop.Lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lngStr = Stop.Lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
                url = $"https://www.google.com/maps/search/?api=1&query={latStr},{lngStr}";
            }
            else
            {
                url = $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(StopName + " paragem")}";
            }
            await Launcher.OpenAsync(new Uri(url));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro", "Não foi possível abrir o Google Maps: " + ex.Message, "OK");
        }
    }

    private async void OnKebabMenuClicked(object? sender, EventArgs e)
    {
        var effectiveCode = !string.IsNullOrWhiteSpace(StopCode) ? StopCode : Stop?.Code;
        if (string.IsNullOrWhiteSpace(effectiveCode)) return;

        var favId = $"stop:unir:{effectiveCode}";
        var isFav = await FavoritesService.Instance.IsFavoriteAsync(favId);
        var favAction = isFav ? "🗑️ Remover dos Favoritos" : "⭐ Adicionar aos Favoritos";

        var choice = await DisplayActionSheetAsync(
            $"{StopName} ({effectiveCode}) - UNIR",
            "Cancelar",
            null,
            favAction,
            "📱 Abrir no Moovit (GPS em direto)",
            "🗺️ Abrir no Google Maps (GPS em direto)",
            "📋 Copiar Código da Paragem");

        if (choice == favAction)
        {
            if (isFav)
            {
                await FavoritesService.Instance.RemoveFavoriteAsync(favId);
                await DisplayAlertAsync("Favoritos", $"Paragem {StopName} removida dos favoritos.", "OK");
            }
            else
            {
                if (Stop == null || string.IsNullOrEmpty(Stop.Code))
                {
                    Stop = new UnirStop
                    {
                        Code = effectiveCode,
                        Name = StopName,
                        Zone = StopZone
                    };
                }

                var item = new FavoriteItem
                {
                    Id = favId,
                    Type = FavoriteType.Stop,
                    Mode = FavoriteTransportMode.Unir,
                    Title = StopName,
                    Subtitle = $"Código: {effectiveCode} • Zona {StopZone} • UNIR",
                    BadgeText = effectiveCode,
                    ColorHex = "#4877ff",
                    PayloadJson = System.Text.Json.JsonSerializer.Serialize(Stop)
                };
                await FavoritesService.Instance.AddFavoriteAsync(item);
                await DisplayAlertAsync("Favoritos", $"Paragem {StopName} adicionada aos favoritos!", "OK");
            }
        }
        else if (choice == "📱 Abrir no Moovit (GPS em direto)")
        {
            await OpenMoovitAsync();
        }
        else if (choice == "🗺️ Abrir no Google Maps (GPS em direto)")
        {
            await OpenGoogleMapsAsync();
        }
        else if (choice == "📋 Copiar Código da Paragem")
        {
            await Clipboard.Default.SetTextAsync(effectiveCode);
            await DisplayAlertAsync("Copiado", $"Código {effectiveCode} copiado para a área de transferência.", "OK");
        }
    }
}
