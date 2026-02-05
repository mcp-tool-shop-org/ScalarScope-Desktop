using ScalarScope.Models;
using ScalarScope.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScalarScope.ViewModels;

/// <summary>
/// ViewModel for the export dialog.
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly ExportService _exportService = new();

    [ObservableProperty]
    private GeometryRun? _run;

    [ObservableProperty]
    private GeometryRun? _leftRun;

    [ObservableProperty]
    private GeometryRun? _rightRun;

    [ObservableProperty]
    private double _currentTime;

    [ObservableProperty]
    private bool _isComparison;

    [ObservableProperty]
    private string _exportStatus = "";

    [ObservableProperty]
    private bool _isExporting;

    // Export settings
    [ObservableProperty]
    private int _width = 1920;

    [ObservableProperty]
    private int _height = 1080;

    [ObservableProperty]
    private int _fps = 30;

    [ObservableProperty]
    private double _duration = 5.0;

    [ObservableProperty]
    private bool _showProfessors = true;

    [ObservableProperty]
    private bool _showMetrics = true;

    [ObservableProperty]
    private bool _showEigenvalues = true;

    [RelayCommand]
    private async Task ExportCurrentFrameAsync()
    {
        if (Run == null && (LeftRun == null || RightRun == null)) return;

        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Save Screenshot As"
        });

        // FilePicker doesn't support save dialogs well in MAUI, so use a default path
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        Directory.CreateDirectory(scalarScopeExports);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = IsComparison
            ? $"scalarscope_comparison_{timestamp}.png"
            : $"scalarscope_trajectory_{timestamp}.png";

        var outputPath = Path.Combine(scalarScopeExports, filename);

        try
        {
            IsExporting = true;
            ExportStatus = "Exporting...";

            var options = CreateExportOptions();

            if (IsComparison && LeftRun != null && RightRun != null)
            {
                await _exportService.ExportComparisonStillAsync(LeftRun, RightRun, CurrentTime, outputPath, options);
            }
            else if (Run != null)
            {
                await _exportService.ExportStillAsync(Run, CurrentTime, outputPath, options);
            }

            ExportStatus = $"Saved: {outputPath}";
        }
        catch (Exception ex)
        {
            ExportStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task ExportFrameSequenceAsync()
    {
        if (Run == null) return;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = Path.Combine(scalarScopeExports, $"sequence_{timestamp}");

        try
        {
            IsExporting = true;
            ExportStatus = "Exporting frame sequence...";

            var options = CreateExportOptions();
            var paths = await _exportService.ExportFrameSequenceAsync(Run, outputDir, options);

            ExportStatus = $"Saved {paths.Length} frames to: {outputDir}";

            // Create a simple info file
            var infoPath = Path.Combine(outputDir, "info.txt");
            await File.WriteAllTextAsync(infoPath,
                $"ScalarScope Frame Sequence Export\n" +
                $"Run: {Run.Metadata.RunId}\n" +
                $"Condition: {Run.Metadata.Condition}\n" +
                $"Frames: {paths.Length}\n" +
                $"FPS: {Fps}\n" +
                $"Duration: {Duration}s\n" +
                $"Resolution: {Width}x{Height}\n" +
                $"\n" +
                $"To convert to video, use ffmpeg:\n" +
                $"ffmpeg -framerate {Fps} -i frame_%05d.png -c:v libx264 -pix_fmt yuv420p output.mp4\n" +
                $"\n" +
                $"To convert to GIF:\n" +
                $"ffmpeg -framerate {Fps} -i frame_%05d.png -vf \"fps={Fps},scale={Width}:-1:flags=lanczos\" output.gif\n");
        }
        catch (Exception ex)
        {
            ExportStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    [RelayCommand]
    private async Task QuickExportAsync()
    {
        // Quick export at current time with default settings
        if (Run == null) return;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        Directory.CreateDirectory(scalarScopeExports);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_quick_{timestamp}.png");

        try
        {
            IsExporting = true;

            var options = CreateExportOptions();
            await _exportService.ExportStillAsync(Run, CurrentTime, outputPath, options);

            ExportStatus = $"Quick save: {Path.GetFileName(outputPath)}";
        }
        catch (Exception ex)
        {
            ExportStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private ExportOptions CreateExportOptions()
    {
        return new ExportOptions
        {
            Width = Width,
            Height = Height,
            Fps = Fps,
            Duration = Duration,
            ShowProfessors = ShowProfessors,
            ShowMetrics = ShowMetrics,
            ShowEigenvalues = ShowEigenvalues
        };
    }
}
