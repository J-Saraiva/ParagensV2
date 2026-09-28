using ParagensV2.Services;

namespace ParagensV2.Views;

public partial class SettingsPage : ContentPage
{
    public bool IsSystemThemeSelected => ThemeService.Instance.CurrentTheme == AppThemeOption.System;
    public bool IsLightThemeSelected => ThemeService.Instance.CurrentTheme == AppThemeOption.Light;
    public bool IsDarkThemeSelected => ThemeService.Instance.CurrentTheme == AppThemeOption.Dark;

    public int SystemThemeStrokeThickness => IsSystemThemeSelected ? 2 : 0;
    public int LightThemeStrokeThickness => IsLightThemeSelected ? 2 : 0;
    public int DarkThemeStrokeThickness => IsDarkThemeSelected ? 2 : 0;

    private static readonly Brush SelectedBrush = new SolidColorBrush(Color.FromArgb("#28d8c3"));
    private static readonly Brush TransparentBrush = new SolidColorBrush(Colors.Transparent);

    public Brush SystemThemeBorderBrush => IsSystemThemeSelected ? SelectedBrush : TransparentBrush;
    public Brush LightThemeBorderBrush => IsLightThemeSelected ? SelectedBrush : TransparentBrush;
    public Brush DarkThemeBorderBrush => IsDarkThemeSelected ? SelectedBrush : TransparentBrush;

    public SettingsPage()
    {
        InitializeComponent();
        BindingContext = this;

        ThemeService.Instance.ThemeChanged += OnThemeChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshState();
    }

    private void OnThemeChanged(object? sender, AppThemeOption e)
    {
        MainThread.BeginInvokeOnMainThread(RefreshState);
    }

    private void RefreshState()
    {
        OnPropertyChanged(nameof(IsSystemThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(SystemThemeStrokeThickness));
        OnPropertyChanged(nameof(LightThemeStrokeThickness));
        OnPropertyChanged(nameof(DarkThemeStrokeThickness));
        OnPropertyChanged(nameof(SystemThemeBorderBrush));
        OnPropertyChanged(nameof(LightThemeBorderBrush));
        OnPropertyChanged(nameof(DarkThemeBorderBrush));
    }

    private void OnOpenMenu(object? sender, EventArgs e)
    {
        Shell.Current.FlyoutIsPresented = true;
    }

    private void OnSelectSystemTheme(object? sender, EventArgs e)
    {
        ThemeService.Instance.SetTheme(AppThemeOption.System);
        RefreshState();
    }

    private void OnSelectLightTheme(object? sender, EventArgs e)
    {
        ThemeService.Instance.SetTheme(AppThemeOption.Light);
        RefreshState();
    }

    private void OnSelectDarkTheme(object? sender, EventArgs e)
    {
        ThemeService.Instance.SetTheme(AppThemeOption.Dark);
        RefreshState();
    }
}
