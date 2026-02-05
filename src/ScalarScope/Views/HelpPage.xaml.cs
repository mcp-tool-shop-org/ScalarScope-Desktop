using ScalarScope.Services;

namespace ScalarScope.Views;

public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Show demo recap if user has completed the demo
        demoRecapFrame.IsVisible = UserPreferencesService.HasCompletedDemo;
    }

    private async void OnReplayDemoClicked(object? sender, EventArgs e)
    {
        try
        {
            // Reset demo state and replay
            UserPreferencesService.ResetFirstRunState();
            App.Session.RefreshFirstRunState();

            // Start the demo again
            var (pathA, pathB) = await DemoService.StartDemoAsync();

            if (pathA != null && pathB != null)
            {
                App.Comparison.LoadDemoRuns(pathA, pathB);
                await Shell.Current.GoToAsync("//compare");

                // Auto-start playback
                await Task.Delay(500);
                if (!App.Comparison.Player.IsPlaying)
                {
                    App.Comparison.Player.PlayPauseCommand.Execute(null);
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not replay demo: {ex.Message}", "OK");
        }
    }

    private void OnRunDiagnosticsClicked(object sender, EventArgs e)
    {
        var report = DiagnosticsService.RunDiagnostics();
        DiagnosticsResultLabel.Text = report.ToSummary();
        DiagnosticsFrame.IsVisible = true;
    }

    private async void OnCreateSupportBundleClicked(object sender, EventArgs e)
    {
        try
        {
            var bundlePath = await CrashReportingService.GenerateSupportBundleAsync();

            await DisplayAlert(
                "Support Bundle Created",
                $"A support bundle has been saved to:\n\n{bundlePath}\n\nPlease include this file when reporting issues.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                "Error",
                $"Failed to create support bundle: {ex.Message}",
                "OK");
        }
    }

    private async void OnCopySystemInfoClicked(object sender, EventArgs e)
    {
        var info = $"""
            ScalarScope System Information
            ==================================
            Version: 1.0.3.0
            OS: {Environment.OSVersion}
            .NET: {Environment.Version}
            64-bit: {Environment.Is64BitProcess}
            Processors: {Environment.ProcessorCount}
            Machine: {Environment.MachineName}
            """;

        await Clipboard.Default.SetTextAsync(info);

        await DisplayAlert(
            "Copied",
            "System information has been copied to the clipboard.",
            "OK");
    }

    private async void OnGitHubClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("https://github.com/mcp-tool-shop-org/scalarscope-desktop");
        }
        catch { }
    }

    private async void OnReportIssueClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("https://github.com/mcp-tool-shop-org/scalarscope-desktop/issues/new/choose");
        }
        catch { }
    }
}
