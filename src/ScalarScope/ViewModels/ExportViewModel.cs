using ScalarScope.Models;
using ScalarScope.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VortexKit.Core;

// Resolve ambiguity between ScalarScope.Services and VortexKit.Core
using ExportService = ScalarScope.Services.ExportService;
using ExportOptions = ScalarScope.Services.ExportOptions;

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

    // SVG export settings
    [ObservableProperty]
    private bool _svgUseCatmullRom = true;

    [ObservableProperty]
    private bool _svgEnableGlow = true;

    [ObservableProperty]
    private bool _svgIncludeGrid = true;

    [ObservableProperty]
    private int _svgColorModeIndex;

    public string[] SvgColorModes { get; } = ["Solid", "Time Gradient", "Velocity Gradient", "Curvature"];

    [ObservableProperty]
    private int _svgPaletteIndex;

    public string[] SvgPalettes { get; } = ["Dark (Default)", "Light", "High Contrast", "Publication"];

    // Phase 2.2: High-resolution raster settings
    [ObservableProperty]
    private int _resolutionPresetIndex;

    public string[] ResolutionPresets { get; } = ["Custom", "HD 720p", "Full HD 1080p", "2K QHD", "4K UHD", "8K UHD"];

    [ObservableProperty]
    private int _formatIndex;

    public string[] ExportFormats { get; } = ["PNG", "JPEG", "WebP", "PDF"];

    [ObservableProperty]
    private bool _transparentBackground;

    [ObservableProperty]
    private int _jpegQuality = 95;

    partial void OnResolutionPresetIndexChanged(int value)
    {
        // Update Width/Height when preset changes
        var (w, h) = value switch
        {
            1 => (1280, 720),
            2 => (1920, 1080),
            3 => (2560, 1440),
            4 => (3840, 2160),
            5 => (7680, 4320),
            _ => (Width, Height) // Custom - keep current values
        };

        if (value > 0) // Only update for presets, not Custom
        {
            Width = w;
            Height = h;
        }
    }

    [RelayCommand]
    private async Task ExportHighResAsync()
    {
        if (Run == null) return;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        Directory.CreateDirectory(scalarScopeExports);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var extension = FormatIndex switch
        {
            1 => ".jpg",
            2 => ".webp",
            3 => ".pdf",
            _ => ".png"
        };
        var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_{timestamp}{extension}");

        try
        {
            IsExporting = true;
            ExportStatus = FormatIndex == 3 ? "Exporting PDF..." : $"Exporting {Width}x{Height}...";

            var options = CreateExportOptions() with
            {
                TransparentBackground = TransparentBackground,
                Format = FormatIndex switch
                {
                    1 => ExportFormat.Jpeg,
                    2 => ExportFormat.Webp,
                    3 => ExportFormat.Pdf,
                    _ => ExportFormat.Png
                },
                JpegQuality = JpegQuality
            };

            ExportResult result;
            if (FormatIndex == 3) // PDF
            {
                result = await _exportService.ExportPdfAsync(Run, CurrentTime, outputPath, options);
            }
            else
            {
                result = await _exportService.ExportStillAsync(Run, CurrentTime, outputPath, options, CancellationToken.None);
            }

            if (result.IsSuccess)
            {
                ExportStatus = $"Saved: {Path.GetFileName(result.OutputPath)}";
            }
            else
            {
                ExportStatus = $"Error: {result.ErrorMessage}";
            }
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
    private async Task ExportSvgAsync()
    {
        if (Run == null) return;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        Directory.CreateDirectory(scalarScopeExports);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_{timestamp}.svg");

        try
        {
            IsExporting = true;
            ExportStatus = "Exporting SVG...";

            var svgOptions = CreateSvgExportOptions();
            var result = await _exportService.ExportSvgAsync(Run, CurrentTime, outputPath, svgOptions);

            if (result.IsSuccess)
            {
                ExportStatus = $"SVG saved: {Path.GetFileName(result.OutputPath)}";
            }
            else
            {
                ExportStatus = $"Error: {result.ErrorMessage}";
            }
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

    private SvgExportOptions CreateSvgExportOptions()
    {
        var palette = SvgPaletteIndex switch
        {
            1 => SvgColorPalette.Light,
            2 => SvgColorPalette.HighContrast,
            3 => SvgColorPalette.Publication,
            _ => SvgColorPalette.Default
        };

        var colorMode = SvgColorModeIndex switch
        {
            1 => SvgColorMode.Time,
            2 => SvgColorMode.Velocity,
            3 => SvgColorMode.Curvature,
            _ => SvgColorMode.Solid
        };

        return new SvgExportOptions
        {
            Width = Width,
            Height = Height,
            Title = Run?.Metadata?.Condition ?? "ScalarScope Export",
            Description = $"Training trajectory visualization - t={CurrentTime:P0}",
            UseCatmullRomSplines = SvgUseCatmullRom,
            EnableGlow = SvgEnableGlow,
            IncludeGrid = SvgIncludeGrid,
            ColorMode = colorMode,
            Palette = palette,
            IncludeInkscapeMetadata = true,
            IncludeStartEndMarkers = true
        };
    }
}
