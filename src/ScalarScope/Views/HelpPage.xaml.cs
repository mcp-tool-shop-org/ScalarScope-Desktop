using ScalarScope.Services;

namespace ScalarScope.Views;

public partial class HelpPage : ContentPage
{
    // Searchable sections mapped by name
    private readonly Dictionary<string, View> _searchableSections = new();

    public HelpPage()
    {
        InitializeComponent();
        InitializeSearchableSections();
    }

    private void InitializeSearchableSections()
    {
        // Map section names for search filtering
        _searchableSections["delta glossary failure ΔF rate"] = deltaFSection;
        _searchableSections["delta convergence ΔTc time speed"] = deltaTcSection;
        _searchableSections["delta dominance ΔTd eigenvalue λ"] = deltaTdSection;
        _searchableSections["delta alignment ΔĀ direction axis"] = deltaASection;
        _searchableSections["delta oscillation ΔO stability instability"] = deltaOSection;
        _searchableSections["trajectory shapes path smooth spiral"] = trajectorySection;
        _searchableSections["eigenvalue dominance spectrum λ₁"] = eigenvalueSection;
        _searchableSections["scalar rings vortex phase"] = scalarRingsSection;
        _searchableSections["delta hierarchy priority order"] = deltaHierarchySection;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Show demo recap if user has completed the demo
        demoRecapFrame.IsVisible = UserPreferencesService.HasCompletedDemo;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(query))
        {
            // Show all sections
            foreach (var section in _searchableSections.Values)
            {
                section.IsVisible = true;
            }
            deltaGlossarySection.IsVisible = true;
            visualPatternsSection.IsVisible = true;
            return;
        }

        // Filter sections based on search query
        foreach (var kvp in _searchableSections)
        {
            kvp.Value.IsVisible = kvp.Key.Contains(query);
        }

        // Show parent sections if any children are visible
        deltaGlossarySection.IsVisible =
            deltaFSection.IsVisible || deltaTcSection.IsVisible ||
            deltaTdSection.IsVisible || deltaASection.IsVisible ||
            deltaOSection.IsVisible;

        visualPatternsSection.IsVisible =
            trajectorySection.IsVisible || eigenvalueSection.IsVisible ||
            scalarRingsSection.IsVisible;
    }

    private async void OnSeeInContextClicked(object? sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is string deltaType)
        {
            try
            {
                // Navigate to comparison page and highlight the delta
                await Shell.Current.GoToAsync("//compare");

                // After navigation, request the comparison page to highlight this delta type
                await Task.Delay(300); // Allow navigation to complete

                // Publish a "see in context" event for the delta
                var message = deltaType switch
                {
                    "failure" => "Navigate to see ΔF (Failure Rate Delta) in context",
                    "convergence" => "Navigate to see ΔTc (Convergence Time Delta) in context",
                    "dominance" => "Navigate to see ΔTd (Dominance Time Delta) in context",
                    "alignment" => "Navigate to see ΔĀ (Alignment Delta) in context",
                    "oscillation" => "Navigate to see ΔO (Oscillation Index Delta) in context",
                    _ => "Navigate to comparison view"
                };

                // Set a flag for the comparison page to read
                MessagingCenter.Send(this, "HighlightDelta", deltaType);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Navigation Error", $"Could not navigate to comparison: {ex.Message}", "OK");
            }
        }
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
