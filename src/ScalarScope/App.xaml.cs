using ScalarScope.Services;
using ScalarScope.ViewModels;

namespace ScalarScope;

public partial class App : Application
{
    public static VortexSessionViewModel Session { get; } = new();
    public static ComparisonViewModel Comparison { get; } = new();
    public static KeyboardService Keyboard { get; private set; } = null!;
    public static bool NeedsRecovery { get; private set; }

    public App()
    {
        InitializeComponent();

        // Initialize crash reporting
        CrashReportingService.Initialize();

        // Check if we need to show recovery
        NeedsRecovery = CrashReportingService.DidRecoverFromCrash();

        Keyboard = new KeyboardService(Session);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();
        shell.BindingContext = Session;

        var window = new Window(shell)
        {
            Title = "ScalarScope",
            Width = 1400,
            Height = 900
        };

        // Subscribe to window lifecycle for session state
        window.Destroying += OnWindowDestroying;

        return window;
    }

    private void OnWindowDestroying(object? sender, EventArgs e)
    {
        // Mark clean shutdown
        CrashReportingService.MarkCleanShutdown();
    }

    /// <summary>
    /// Save current session state (call periodically or on significant changes).
    /// </summary>
    public static void SaveSessionState()
    {
        var state = new SessionState
        {
            LoadedFilePath = Session.LoadedFilePath,
            CurrentPage = Shell.Current?.CurrentState?.Location?.ToString() ?? "Trajectory",
            PlaybackTime = Session.Player?.Time ?? 0,
            IsPlaying = Session.Player?.IsPlaying ?? false,
            Theme = Application.Current?.RequestedTheme == AppTheme.Dark ? "dark" : "light"
        };

        CrashReportingService.SaveSessionState(state);
    }
}
