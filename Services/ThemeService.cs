namespace ParagensV2.Services;

public enum AppThemeOption
{
    System = 0,
    Light = 1,
    Dark = 2
}

public class ThemeService
{
    private const string ThemePreferenceKey = "app_theme_preference";
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public AppThemeOption CurrentTheme { get; private set; } = AppThemeOption.System;

    public event EventHandler<AppThemeOption>? ThemeChanged;

    private bool _isApplyingTheme;

    public bool IsCurrentlyDark
    {
        get
        {
            if (CurrentTheme == AppThemeOption.Dark) return true;
            if (CurrentTheme == AppThemeOption.Light) return false;
            try
            {
                return Application.Current?.RequestedTheme == AppTheme.Dark;
            }
            catch
            {
                return false;
            }
        }
    }

    public void Initialize()
    {
        try
        {
            var savedOption = Preferences.Default.Get(ThemePreferenceKey, (int)AppThemeOption.System);
            CurrentTheme = Enum.IsDefined(typeof(AppThemeOption), savedOption) 
                ? (AppThemeOption)savedOption 
                : AppThemeOption.System;

            ApplyTheme(CurrentTheme);

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeChanged -= OnSystemThemeChanged;
                Application.Current.RequestedThemeChanged += OnSystemThemeChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ThemeService Initialize error: {ex.Message}");
        }
    }

    public void SetTheme(AppThemeOption option)
    {
        CurrentTheme = option;
        try
        {
            Preferences.Default.Set(ThemePreferenceKey, (int)option);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving theme preference: {ex.Message}");
        }

        ApplyTheme(option);
        ThemeChanged?.Invoke(this, option);
    }

    private void ApplyTheme(AppThemeOption option)
    {
        if (Application.Current == null || _isApplyingTheme) return;

        try
        {
            _isApplyingTheme = true;

            var targetTheme = option switch
            {
                AppThemeOption.Light => AppTheme.Light,
                AppThemeOption.Dark => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };

            if (Application.Current.UserAppTheme != targetTheme)
            {
                Application.Current.UserAppTheme = targetTheme;
            }

            UpdateColorResources();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
        }
        finally
        {
            _isApplyingTheme = false;
        }
    }

    private void UpdateColorResources()
    {
        if (Application.Current == null) return;

        try
        {
            bool isDark = IsCurrentlyDark;
            Application.Current.Resources["Background"] = Color.FromArgb(isDark ? "#0B1120" : "#F0F4F8");
            Application.Current.Resources["CardBackground"] = Color.FromArgb(isDark ? "#1E293B" : "#FFFFFF");
            Application.Current.Resources["TextPrimary"] = Color.FromArgb(isDark ? "#F8FAFC" : "#1E293B");
            Application.Current.Resources["TextSecondary"] = Color.FromArgb(isDark ? "#94A3B8" : "#64748B");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating color resources: {ex.Message}");
        }
    }

    private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        if (_isApplyingTheme) return;

        if (CurrentTheme == AppThemeOption.System)
        {
            UpdateColorResources();
            ThemeChanged?.Invoke(this, AppThemeOption.System);
        }
    }
}
