using ScalarScope.Services;
using ScalarScope.ViewModels;

namespace ScalarScope.Views;

public partial class OverviewPage : ContentPage
{
    private readonly ExportService _exportService = new();
    private readonly ExportViewModel _exportViewModel = new();

    public OverviewPage()
    {
        InitializeComponent();
        BindingContext = App.Session;
    }

    // === First-Run Demo Handlers ===

    private async void OnStartDemoClicked(object? sender, EventArgs e)
    {
        try
        {
            // Start demo - loads both Path A and Path B
            var (pathA, pathB) = await DemoService.StartDemoAsync();

            if (pathA == null || pathB == null)
            {
                await DisplayAlert("Demo Error", "Failed to load demo data. Please try again.", "OK");
                return;
            }

            // Refresh first-run state
            App.Session.RefreshFirstRunState();

            // Load runs into ComparisonViewModel
            App.Comparison.LoadDemoRuns(pathA, pathB);

            // Navigate to Compare tab
            await Shell.Current.GoToAsync("//compare");

            // Auto-start playback after a brief delay for UI to settle
            await Task.Delay(500);
            if (!App.Comparison.Player.IsPlaying)
            {
                App.Comparison.Player.PlayPauseCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Demo Error", $"Could not start demo: {ex.Message}", "OK");
        }
    }

    private async void OnLoadOwnRunClicked(object? sender, EventArgs e)
    {
        // Mark demo as skipped (user chose their own path)
        UserPreferencesService.MarkDemoSkipped();
        App.Session.RefreshFirstRunState();

        // Trigger the standard file picker
        App.Session.LoadRunCommand.Execute(null);
    }

    private void OnSkipDemoClicked(object? sender, EventArgs e)
    {
        // Mark demo as skipped
        UserPreferencesService.MarkDemoSkipped();
        App.Session.RefreshFirstRunState();

        // Stay on Overview - the empty state panel will now show
    }

    // === Sample Run Handlers ===

    private async void OnLoadCorrelatedClicked(object? sender, EventArgs e)
    {
        await LoadSampleRunAsync("correlated_professors.json");
    }

    private async void OnLoadOrthogonalClicked(object? sender, EventArgs e)
    {
        await LoadSampleRunAsync("orthogonal_professors.json");
    }

    private async Task LoadSampleRunAsync(string sampleFileName)
    {
        try
        {
            string? json = null;

            // Try multiple paths for MAUI asset loading (same approach as DemoService)
            var pathsToTry = new[]
            {
                $"Samples/{sampleFileName}",
                $"Samples\\{sampleFileName}",
                sampleFileName,
            };

            foreach (var path in pathsToTry)
            {
                try
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(path);
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                    break;
                }
                catch
                {
                    // Try next path
                }
            }

            // Fallback: try loading from source directory (for development)
            if (json == null)
            {
                var sourceDir = AppContext.BaseDirectory;
                var devPaths = new[]
                {
                    Path.Combine(sourceDir, "Resources", "Raw", "Samples", sampleFileName),
                    Path.Combine(sourceDir, "..", "..", "..", "..", "Resources", "Raw", "Samples", sampleFileName),
                    Path.Combine(sourceDir, "..", "..", "..", "..", "..", "Resources", "Raw", "Samples", sampleFileName),
                };

                foreach (var devPath in devPaths)
                {
                    if (File.Exists(devPath))
                    {
                        json = await File.ReadAllTextAsync(devPath);
                        break;
                    }
                }
            }

            if (json == null)
            {
                await DisplayAlert("Error", $"Could not find sample file: {sampleFileName}", "OK");
                return;
            }

            // Write to temp file and load (VortexSessionViewModel expects a file path)
            var tempPath = Path.Combine(FileSystem.CacheDirectory, sampleFileName);
            await File.WriteAllTextAsync(tempPath, json);

            await App.Session.LoadFromFileAsync(tempPath);

            // Navigate to trajectory view after loading
            await Shell.Current.GoToAsync("//trajectory");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load sample: {ex.Message}", "OK");
        }
    }

    private async void OnQuickExportClicked(object? sender, EventArgs e)
    {
        if (App.Session.Run == null)
        {
            exportStatusLabel.Text = "Load a training run first";
            return;
        }

        try
        {
            exportStatusLabel.Text = "Exporting...";
            exportStatusLabel.TextColor = Color.FromArgb("#888");

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
            Directory.CreateDirectory(scalarScopeExports);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_screenshot_{timestamp}.png");

            await _exportService.ExportStillAsync(
                App.Session.Run,
                App.Session.Player.Time,
                outputPath);

            exportStatusLabel.Text = $"Saved: {Path.GetFileName(outputPath)}";
            exportStatusLabel.TextColor = Color.FromArgb("#4ecdc4");
        }
        catch (Exception ex)
        {
            exportStatusLabel.Text = $"Error: {ex.Message}";
            exportStatusLabel.TextColor = Color.FromArgb("#ff6b6b");
        }
    }

    private async void OnExportSettingsClicked(object? sender, EventArgs e)
    {
        // For now, show a simple dialog. In future, this could be a full settings page.
        var action = await DisplayActionSheet(
            "Export Options",
            "Cancel",
            null,
            "Screenshot (1920x1080)",
            "Screenshot (3840x2160 4K)",
            "Frame Sequence (5s @ 30fps)",
            "Frame Sequence (10s @ 60fps)");

        if (action == null || action == "Cancel" || App.Session.Run == null) return;

        try
        {
            exportStatusLabel.Text = "Exporting...";

            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
            Directory.CreateDirectory(scalarScopeExports);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            if (action.StartsWith("Screenshot"))
            {
                var is4K = action.Contains("4K");
                var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_{(is4K ? "4k" : "hd")}_{timestamp}.png");

                await _exportService.ExportStillAsync(
                    App.Session.Run,
                    App.Session.Player.Time,
                    outputPath,
                    new ExportOptions
                    {
                        Width = is4K ? 3840 : 1920,
                        Height = is4K ? 2160 : 1080
                    });

                exportStatusLabel.Text = $"Saved: {Path.GetFileName(outputPath)}";
            }
            else if (action.StartsWith("Frame Sequence"))
            {
                var is60fps = action.Contains("60fps");
                var is10s = action.Contains("10s");
                var outputDir = Path.Combine(scalarScopeExports, $"sequence_{timestamp}");

                var paths = await _exportService.ExportFrameSequenceAsync(
                    App.Session.Run,
                    outputDir,
                    new ExportOptions
                    {
                        Fps = is60fps ? 60 : 30,
                        Duration = is10s ? 10.0 : 5.0
                    });

                exportStatusLabel.Text = $"Saved {paths.Length} frames to: sequence_{timestamp}";
            }

            exportStatusLabel.TextColor = Color.FromArgb("#4ecdc4");
        }
        catch (Exception ex)
        {
            exportStatusLabel.Text = $"Error: {ex.Message}";
            exportStatusLabel.TextColor = Color.FromArgb("#ff6b6b");
        }
    }
}
