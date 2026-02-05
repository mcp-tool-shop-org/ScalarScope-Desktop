using AspireDesktop.Services;

namespace AspireDesktop.Views;

public partial class HelpPage : ContentPage
{
    public HelpPage()
    {
        InitializeComponent();
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
            ASPIRE Desktop System Information
            ==================================
            Version: 1.0.0-rc.1
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
            await Launcher.OpenAsync("https://github.com/mcp-tool-shop-org/aspire-ai-desktop");
        }
        catch { }
    }

    private async void OnReportIssueClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("https://github.com/mcp-tool-shop-org/aspire-ai-desktop/issues/new/choose");
        }
        catch { }
    }
}
