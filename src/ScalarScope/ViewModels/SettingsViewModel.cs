using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarScope.Services;

namespace ScalarScope.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// Uses partial properties for AOT compatibility.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        LoadSettings();
    }

    // --- Theme Settings ---

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    public string[] ThemeOptions { get; } = ["System", "Light", "Dark"];

    partial void OnThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };
        UserPreferencesService.SetTheme(theme);
        Application.Current!.UserAppTheme = theme;
    }

    // --- Animation Settings ---

    [ObservableProperty]
    public partial bool ReduceAnimations { get; set; }

    partial void OnReduceAnimationsChanged(bool value)
    {
        UserPreferencesService.SetReduceAnimations(value);
    }

    // --- Playback Settings ---

    [ObservableProperty]
    public partial int DefaultSpeedIndex { get; set; }

    public string[] SpeedOptions { get; } = ["0.25x", "0.5x", "1x", "2x", "4x"];

    public float[] SpeedValues { get; } = [0.25f, 0.5f, 1f, 2f, 4f];

    partial void OnDefaultSpeedIndexChanged(int value)
    {
        if (value >= 0 && value < SpeedValues.Length)
        {
            UserPreferencesService.SetDefaultPlaybackSpeed(SpeedValues[value]);
        }
    }

    [ObservableProperty]
    public partial bool AutoPlayOnLoad { get; set; }

    partial void OnAutoPlayOnLoadChanged(bool value)
    {
        UserPreferencesService.SetAutoPlayOnLoad(value);
    }

    // --- Session Settings ---

    [ObservableProperty]
    public partial bool AutoLoadLastSession { get; set; }

    partial void OnAutoLoadLastSessionChanged(bool value)
    {
        UserPreferencesService.SetAutoLoadLastSession(value);
    }

    [ObservableProperty]
    public partial int RecentFilesLimitIndex { get; set; }

    public string[] RecentFilesOptions { get; } = ["5 files", "10 files", "20 files"];

    public int[] RecentFilesValues { get; } = [5, 10, 20];

    partial void OnRecentFilesLimitIndexChanged(int value)
    {
        if (value >= 0 && value < RecentFilesValues.Length)
        {
            UserPreferencesService.SetRecentFilesLimit(RecentFilesValues[value]);
        }
    }

    // --- Export Settings ---

    [ObservableProperty]
    public partial string DefaultExportPath { get; set; } = "";

    [ObservableProperty]
    public partial int DefaultExportWidth { get; set; } = 1920;

    [ObservableProperty]
    public partial int DefaultExportHeight { get; set; } = 1080;

    partial void OnDefaultExportWidthChanged(int value)
    {
        UserPreferencesService.SetDefaultExportResolution(value, DefaultExportHeight);
    }

    partial void OnDefaultExportHeightChanged(int value)
    {
        UserPreferencesService.SetDefaultExportResolution(DefaultExportWidth, value);
    }

    // --- Accessibility Settings ---

    [ObservableProperty]
    public partial bool HighContrastMode { get; set; }

    partial void OnHighContrastModeChanged(bool value)
    {
        UserPreferencesService.SetHighContrastMode(value);
    }

    [ObservableProperty]
    public partial int AnnotationDensityIndex { get; set; }

    public string[] AnnotationOptions { get; } = ["Minimal", "Standard", "Full"];

    partial void OnAnnotationDensityIndexChanged(int value)
    {
        var density = value switch
        {
            0 => AnnotationDensity.Minimal,
            2 => AnnotationDensity.Full,
            _ => AnnotationDensity.Standard
        };
        UserPreferencesService.SetAnnotationDensity(density);
    }

    // --- Commands ---

    [RelayCommand]
    private async Task BrowseExportPathAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(new CancellationToken());
            if (result.IsSuccessful && !string.IsNullOrEmpty(result.Folder?.Path))
            {
                DefaultExportPath = result.Folder.Path;
                UserPreferencesService.SetDefaultExportPath(DefaultExportPath);
            }
        }
        catch
        {
            // Folder picker not supported or canceled
        }
    }

    [RelayCommand]
    private void ResetAllSettings()
    {
        UserPreferencesService.ResetAllSettings();
        LoadSettings();

        // Reset theme to system
        Application.Current!.UserAppTheme = AppTheme.Unspecified;
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        UserPreferencesService.ClearRecentFiles();
    }

    [RelayCommand]
    private void ResetDemoState()
    {
        UserPreferencesService.ResetFirstRunState();
    }

    // --- Load/Save ---

    private void LoadSettings()
    {
        // Theme
        ThemeIndex = UserPreferencesService.GetTheme() switch
        {
            AppTheme.Light => 1,
            AppTheme.Dark => 2,
            _ => 0
        };

        // Animations
        ReduceAnimations = UserPreferencesService.GetReduceAnimations();

        // Playback
        var speed = UserPreferencesService.GetDefaultPlaybackSpeed();
        DefaultSpeedIndex = Array.IndexOf(SpeedValues, speed);
        if (DefaultSpeedIndex < 0) DefaultSpeedIndex = 2; // Default to 1x

        AutoPlayOnLoad = UserPreferencesService.GetAutoPlayOnLoad();

        // Session
        AutoLoadLastSession = UserPreferencesService.GetAutoLoadLastSession();

        var limit = UserPreferencesService.GetRecentFilesLimit();
        RecentFilesLimitIndex = Array.IndexOf(RecentFilesValues, limit);
        if (RecentFilesLimitIndex < 0) RecentFilesLimitIndex = 1; // Default to 10

        // Export
        DefaultExportPath = UserPreferencesService.GetDefaultExportPath();
        var (width, height) = UserPreferencesService.GetDefaultExportResolution();
        DefaultExportWidth = width;
        DefaultExportHeight = height;

        // Accessibility
        HighContrastMode = UserPreferencesService.GetHighContrastMode();
        AnnotationDensityIndex = UserPreferencesService.GetAnnotationDensity() switch
        {
            AnnotationDensity.Minimal => 0,
            AnnotationDensity.Full => 2,
            _ => 1
        };
    }
}
