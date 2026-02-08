using ScalarScope.Models;
using ScalarScope.ViewModels;
using SkiaSharp;
using VortexKit.Core;

namespace ScalarScope.Services;

/// <summary>
/// Service for exporting visualizations as images and video clips.
/// </summary>
public class ExportService
{
    private const int DefaultWidth = 1920;
    private const int DefaultHeight = 1080;
    private const int MaxWidth = 7680;  // 8K limit
    private const int MaxHeight = 4320;
    private const int MaxFrames = 3600; // 60fps * 60s max

    /// <summary>
    /// Validates that a run can be exported.
    /// </summary>
    public ExportValidationResult ValidateForExport(GeometryRun? run)
    {
        if (run == null)
            return ExportValidationResult.Error("No training run loaded. Load a run before exporting.");

        if (run.Trajectory?.Timesteps == null || run.Trajectory.Timesteps.Count == 0)
            return ExportValidationResult.Error("Run has no trajectory data to export.");

        if (run.Trajectory.Timesteps[0].State2D == null || run.Trajectory.Timesteps[0].State2D.Count < 2)
            return ExportValidationResult.Error("Trajectory data is missing 2D coordinates.");

        return ExportValidationResult.Success();
    }

    /// <summary>
    /// Validates export options and returns sanitized options.
    /// </summary>
    public (ExportOptions options, string? warning) ValidateExportOptions(ExportOptions? options)
    {
        options ??= new ExportOptions();
        string? warning = null;

        var width = Math.Clamp(options.Width ?? DefaultWidth, 320, MaxWidth);
        var height = Math.Clamp(options.Height ?? DefaultHeight, 240, MaxHeight);
        var fps = Math.Clamp(options.Fps ?? 30, 1, 120);
        var duration = Math.Clamp(options.Duration ?? 5.0, 0.1, 120.0);

        var totalFrames = (int)(fps * duration);
        if (totalFrames > MaxFrames)
        {
            duration = MaxFrames / (double)fps;
            warning = $"Duration capped to {duration:F1}s to stay within frame limit";
        }

        return (options with
        {
            Width = width,
            Height = height,
            Fps = fps,
            Duration = duration
        }, warning);
    }

    /// <summary>
    /// Export current trajectory view as a PNG image.
    /// </summary>
    public async Task<ExportResult> ExportStillAsync(
        GeometryRun run,
        double time,
        string outputPath,
        ExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateForExport(run);
        if (!validation.IsValid)
            return ExportResult.Failure(validation.ErrorMessage!);

        var (validOptions, warning) = ValidateExportOptions(options);

        try
        {
            var width = validOptions.Width ?? DefaultWidth;
            var height = validOptions.Height ?? DefaultHeight;

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            if (surface == null)
                return ExportResult.Failure("Failed to create rendering surface. Try a smaller resolution.");

            var canvas = surface.Canvas;

            cancellationToken.ThrowIfCancellationRequested();

            // Render the visualization
            RenderTrajectoryFrame(canvas, run, time, width, height, validOptions);

            cancellationToken.ThrowIfCancellationRequested();

            // Ensure output directory exists
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Save to file
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            await using var stream = File.Create(outputPath);
            data.SaveTo(stream);

            return ExportResult.Succeeded(outputPath, warning);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial file
            TryDeleteFile(outputPath);
            return ExportResult.Cancelled();
        }
        catch (IOException ex)
        {
            return ExportResult.Failure($"File write failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }

    // Original method signature for backwards compatibility
    public async Task<string> ExportStillAsync(
        GeometryRun run,
        double time,
        string outputPath,
        ExportOptions? options)
    {
        var result = await ExportStillAsync(run, time, outputPath, options, CancellationToken.None);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.ErrorMessage);
        return result.OutputPath!;
    }

    /// <summary>
    /// Export trajectory animation as a sequence of frames.
    /// Can be combined into GIF or video externally.
    /// </summary>
    public async Task<ExportSequenceResult> ExportFrameSequenceAsync(
        GeometryRun run,
        string outputDir,
        ExportOptions? options = null,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateForExport(run);
        if (!validation.IsValid)
            return new ExportSequenceResult { IsSuccess = false, ErrorMessage = validation.ErrorMessage };

        var (validOptions, warning) = ValidateExportOptions(options);

        try
        {
            Directory.CreateDirectory(outputDir);

            var width = validOptions.Width ?? DefaultWidth;
            var height = validOptions.Height ?? DefaultHeight;
            var fps = validOptions.Fps ?? 30;
            var duration = validOptions.Duration ?? 5.0;
            var totalFrames = (int)(fps * duration);

            var paths = new List<string>();

            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            if (surface == null)
                return new ExportSequenceResult { IsSuccess = false, ErrorMessage = "Failed to create rendering surface" };

            var canvas = surface.Canvas;

            for (int frame = 0; frame < totalFrames; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var t = totalFrames > 1 ? (double)frame / (totalFrames - 1) : 0;

                canvas.Clear();
                RenderTrajectoryFrame(canvas, run, t, width, height, validOptions);

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);

                var framePath = Path.Combine(outputDir, $"frame_{frame:D5}.png");
                await using var stream = File.Create(framePath);
                data.SaveTo(stream);

                paths.Add(framePath);

                progress?.Report(new ExportProgress
                {
                    CurrentFrame = frame + 1,
                    TotalFrames = totalFrames,
                    PercentComplete = (frame + 1) * 100 / totalFrames,
                    CurrentFile = Path.GetFileName(framePath)
                });
            }

            return new ExportSequenceResult
            {
                IsSuccess = true,
                Paths = paths.ToArray(),
                Warning = warning
            };
        }
        catch (OperationCanceledException)
        {
            // Clean up partial output
            TryDeleteDirectory(outputDir);
            return new ExportSequenceResult { IsSuccess = false, IsCancelled = true };
        }
        catch (IOException ex)
        {
            return new ExportSequenceResult { IsSuccess = false, ErrorMessage = $"File write failed: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new ExportSequenceResult { IsSuccess = false, ErrorMessage = $"Export failed: {ex.Message}" };
        }
    }

    // Original method signature for backwards compatibility
    public async Task<string[]> ExportFrameSequenceAsync(
        GeometryRun run,
        string outputDir,
        ExportOptions? options)
    {
        var result = await ExportFrameSequenceAsync(run, outputDir, options, null, CancellationToken.None);
        if (!result.IsSuccess)
            throw new InvalidOperationException(result.ErrorMessage ?? "Export failed");
        return result.Paths!;
    }

    /// <summary>
    /// Export comparison view as a side-by-side image.
    /// </summary>
    public async Task<string> ExportComparisonStillAsync(
        GeometryRun leftRun,
        GeometryRun rightRun,
        double time,
        string outputPath,
        ExportOptions? options = null)
    {
        options ??= new ExportOptions();

        var width = options.Width ?? DefaultWidth;
        var height = options.Height ?? DefaultHeight;
        var halfWidth = width / 2 - 2;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        // Background
        canvas.Clear(SKColor.Parse("#0f0f1a"));

        // Left panel
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, halfWidth, height));
        RenderTrajectoryFrame(canvas, leftRun, time, halfWidth, height, options with
        {
            Label = "Path A (Orthogonal)",
            AccentColor = SKColor.Parse("#4ecdc4")
        });
        canvas.Restore();

        // Divider
        using var dividerPaint = new SKPaint { Color = SKColor.Parse("#2a2a4e"), StrokeWidth = 4 };
        canvas.DrawLine(width / 2f, 0, width / 2f, height, dividerPaint);

        // Right panel
        canvas.Save();
        canvas.Translate(width / 2f + 2, 0);
        canvas.ClipRect(new SKRect(0, 0, halfWidth, height));
        RenderTrajectoryFrame(canvas, rightRun, time, halfWidth, height, options with
        {
            Label = "Path B (Correlated)",
            AccentColor = SKColor.Parse("#ff6b6b")
        });
        canvas.Restore();

        // Title bar
        DrawTitleBar(canvas, leftRun, rightRun, time, width);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        return outputPath;
    }

    /// <summary>
    /// Export trajectory as SVG with full vector fidelity.
    /// </summary>
    public async Task<ExportResult> ExportSvgAsync(
        GeometryRun run,
        double time,
        string outputPath,
        SvgExportOptions? svgOptions = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateForExport(run);
        if (!validation.IsValid)
            return ExportResult.Failure(validation.ErrorMessage!);

        svgOptions ??= new SvgExportOptions();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Convert GeometryRun to SvgExportData
            var svgData = ConvertToSvgData(run, time, svgOptions);

            // Export using SvgExportService
            var svgService = new SvgExportService();
            var path = await svgService.ExportSvgAsync(svgData, outputPath, svgOptions);

            return ExportResult.Succeeded(path);
        }
        catch (OperationCanceledException)
        {
            return ExportResult.Cancelled();
        }
        catch (Exception ex)
        {
            return ExportResult.Failure($"SVG export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert GeometryRun to SvgExportData.
    /// </summary>
    private SvgExportData ConvertToSvgData(GeometryRun run, double time, SvgExportOptions options)
    {
        var timesteps = run.Trajectory?.Timesteps ?? [];
        var maxIdx = Math.Min((int)(time * Math.Max(1, timesteps.Count - 1)), timesteps.Count - 1);

        // Calculate view bounds from trajectory
        var (minX, maxX, minY, maxY) = CalculateBounds(timesteps);
        var padding = Math.Max(maxX - minX, maxY - minY) * 0.1;
        var viewBox = new SvgViewBox(
            minX - padding,
            minY - padding,
            (maxX - minX) + padding * 2,
            (maxY - minY) + padding * 2
        );

        // Convert trajectory points
        var trajectoryPoints = new List<SvgPoint>();
        var velocities = new List<double>();
        var curvatures = new List<double>();

        for (int i = 0; i <= maxIdx && i < timesteps.Count; i++)
        {
            var ts = timesteps[i];
            if (ts.State2D.Count >= 2)
            {
                trajectoryPoints.Add(new SvgPoint(ts.State2D[0], ts.State2D[1]));
                velocities.Add(ts.VelocityMagnitude);
                curvatures.Add(ts.Curvature);
            }
        }

        var trajectory = new SvgTrajectoryData
        {
            Points = trajectoryPoints,
            Label = run.Metadata?.Condition ?? "Trajectory",
            Velocities = velocities,
            Curvatures = curvatures
        };

        // Create grid data
        var grid = CreateGridData(viewBox);

        // Create markers for failures
        var markers = new List<SvgMarker>();
        if (run.Failures != null)
        {
            foreach (var failure in run.Failures.Where(f => f.T <= time))
            {
                var idx = (int)(failure.T * Math.Max(1, timesteps.Count - 1));
                if (idx >= 0 && idx < timesteps.Count && timesteps[idx].State2D.Count >= 2)
                {
                    markers.Add(new SvgMarker(
                        timesteps[idx].State2D[0],
                        timesteps[idx].State2D[1],
                        0.05,
                        SvgMarkerType.Failure,
                        failure.Description
                    ));
                }
            }
        }

        // Create annotations
        var annotations = new List<SvgAnnotation>
        {
            new(viewBox.X + 0.05, viewBox.Y + 0.05,
                $"{run.Metadata?.Condition ?? "Run"} | t={time:P0}")
        };

        return new SvgExportData
        {
            ViewBox = viewBox,
            Trajectories = [trajectory],
            Grid = grid,
            Markers = markers,
            Annotations = annotations
        };
    }

    private static (double minX, double maxX, double minY, double maxY) CalculateBounds(
        IList<TrajectoryTimestep> timesteps)
    {
        if (timesteps.Count == 0)
            return (-1, 1, -1, 1);

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var ts in timesteps)
        {
            if (ts.State2D.Count >= 2)
            {
                minX = Math.Min(minX, ts.State2D[0]);
                maxX = Math.Max(maxX, ts.State2D[0]);
                minY = Math.Min(minY, ts.State2D[1]);
                maxY = Math.Max(maxY, ts.State2D[1]);
            }
        }

        // Ensure minimum size
        if (maxX - minX < 0.01) { minX -= 0.5; maxX += 0.5; }
        if (maxY - minY < 0.01) { minY -= 0.5; maxY += 0.5; }

        return (minX, maxX, minY, maxY);
    }

    private static SvgGridData CreateGridData(SvgViewBox viewBox)
    {
        var majorLines = new List<SvgLine>();
        var minorLines = new List<SvgLine>();
        var labels = new List<SvgLabel>();

        // Calculate grid spacing (round to nice numbers)
        var rangeX = viewBox.Width;
        var rangeY = viewBox.Height;
        var majorSpacing = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(rangeX, rangeY))));
        if (majorSpacing > Math.Max(rangeX, rangeY) / 2) majorSpacing /= 2;

        var minorSpacing = majorSpacing / 5;

        // Vertical lines
        var startX = Math.Floor(viewBox.X / majorSpacing) * majorSpacing;
        for (var x = startX; x <= viewBox.X + viewBox.Width; x += majorSpacing)
        {
            majorLines.Add(new SvgLine(x, viewBox.Y, x, viewBox.Y + viewBox.Height));
            labels.Add(new SvgLabel(x, viewBox.Y + viewBox.Height + majorSpacing * 0.1, $"{x:G3}"));

            for (var mx = x + minorSpacing; mx < x + majorSpacing && mx <= viewBox.X + viewBox.Width; mx += minorSpacing)
            {
                minorLines.Add(new SvgLine(mx, viewBox.Y, mx, viewBox.Y + viewBox.Height));
            }
        }

        // Horizontal lines
        var startY = Math.Floor(viewBox.Y / majorSpacing) * majorSpacing;
        for (var y = startY; y <= viewBox.Y + viewBox.Height; y += majorSpacing)
        {
            majorLines.Add(new SvgLine(viewBox.X, y, viewBox.X + viewBox.Width, y));
            labels.Add(new SvgLabel(viewBox.X - majorSpacing * 0.1, y, $"{y:G3}"));

            for (var my = y + minorSpacing; my < y + majorSpacing && my <= viewBox.Y + viewBox.Height; my += minorSpacing)
            {
                minorLines.Add(new SvgLine(viewBox.X, my, viewBox.X + viewBox.Width, my));
            }
        }

        return new SvgGridData
        {
            MajorLines = majorLines,
            MinorLines = minorLines,
            Labels = labels
        };
    }

    private void RenderTrajectoryFrame(
        SKCanvas canvas,
        GeometryRun run,
        double time,
        int width,
        int height,
        ExportOptions options)
    {
        var backgroundColor = SKColor.Parse("#0f0f1a");
        var gridColor = SKColor.Parse("#2a2a4e");
        var accentColor = options.AccentColor ?? SKColor.Parse("#00d9ff");

        canvas.Clear(backgroundColor);

        var center = new SKPoint(width / 2f, height / 2f);
        var scale = Math.Min(width, height) / 4f;

        // Draw grid
        DrawGrid(canvas, width, height, center, scale, gridColor);

        // Draw label if provided
        if (!string.IsNullOrEmpty(options.Label))
        {
            using var labelFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 24);
            using var labelPaint = new SKPaint
            {
                Color = accentColor,
                IsAntialias = true
            };
            canvas.DrawText(options.Label, 20, 40, SKTextAlign.Left, labelFont, labelPaint);
        }

        // Draw professor vectors
        if (options.ShowProfessors)
        {
            DrawProfessorVectors(canvas, run, center, scale);
        }

        // Draw trajectory
        DrawTrajectory(canvas, run, time, center, scale, accentColor);

        // Draw current position
        DrawCurrentPosition(canvas, run, time, center, scale, accentColor);

        // Draw metrics
        if (options.ShowMetrics)
        {
            DrawMetrics(canvas, run, time, width, height);
        }

        // Draw eigenvalue spectrum
        if (options.ShowEigenvalues)
        {
            DrawEigenSpectrum(canvas, run, time, width, height);
        }
    }

    private void DrawGrid(SKCanvas canvas, int width, int height, SKPoint center, float scale, SKColor gridColor)
    {
        using var paint = new SKPaint
        {
            Color = gridColor,
            StrokeWidth = 1,
            IsAntialias = true
        };

        canvas.DrawLine(0, center.Y, width, center.Y, paint);
        canvas.DrawLine(center.X, 0, center.X, height, paint);

        paint.PathEffect = SKPathEffect.CreateDash([5, 5], 0);
        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            var offset = i * scale / 2;
            canvas.DrawLine(0, center.Y + offset, width, center.Y + offset, paint);
            canvas.DrawLine(center.X + offset, 0, center.X + offset, height, paint);
        }
    }

    private void DrawProfessorVectors(SKCanvas canvas, GeometryRun run, SKPoint center, float scale)
    {
        var professors = run.Evaluators?.Professors;
        if (professors == null || professors.Count == 0) return;

        using var paint = new SKPaint
        {
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            Color = SKColor.Parse("#a29bfe").WithAlpha(180)
        };

        using var holdoutPaint = new SKPaint
        {
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            Color = SKColor.Parse("#fd79a8").WithAlpha(180)
        };

        foreach (var prof in professors)
        {
            if (prof.Vector.Count < 2) continue;
            var end = ToScreen(prof.Vector, center, scale);
            DrawArrow(canvas, center, end, prof.Holdout ? holdoutPaint : paint);
        }
    }

    private void DrawTrajectory(SKCanvas canvas, GeometryRun run, double time, SKPoint center, float scale, SKColor accentColor)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count < 2) return;

        var maxIdx = (int)(time * (steps.Count - 1));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        for (int i = 1; i <= maxIdx && i < steps.Count; i++)
        {
            var t = (float)i / steps.Count;
            paint.Color = accentColor.WithAlpha((byte)(100 + t * 155));

            var p1 = ToScreen(steps[i - 1].State2D, center, scale);
            var p2 = ToScreen(steps[i].State2D, center, scale);
            canvas.DrawLine(p1, p2, paint);
        }
    }

    private void DrawCurrentPosition(SKCanvas canvas, GeometryRun run, double time, SKPoint center, float scale, SKColor accentColor)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count == 0) return;

        var idx = (int)(time * (steps.Count - 1));
        idx = Math.Clamp(idx, 0, steps.Count - 1);
        var current = steps[idx];

        if (current.State2D.Count < 2) return;

        var pos = ToScreen(current.State2D, center, scale);

        using var glowPaint = new SKPaint
        {
            Color = accentColor.WithAlpha(120),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12)
        };
        canvas.DrawCircle(pos, 18, glowPaint);

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(pos, 8, paint);
    }

    private void DrawMetrics(SKCanvas canvas, GeometryRun run, double time, int width, int height)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count == 0) return;

        var idx = (int)(time * (steps.Count - 1));
        idx = Math.Clamp(idx, 0, steps.Count - 1);
        var current = steps[idx];

        using var font = new SKFont(SKTypeface.Default, 16);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(200),
            IsAntialias = true
        };

        var x = 20f;
        var y = height - 60f;

        canvas.DrawText($"Time: {time:P0}", x, y, SKTextAlign.Left, font, paint);
        y += 22;
        canvas.DrawText($"Eff.Dim: {current.EffectiveDim:F2}", x, y, SKTextAlign.Left, font, paint);
        y += 22;
        canvas.DrawText($"Curvature: {current.Curvature:F3}", x, y, SKTextAlign.Left, font, paint);
    }

    private void DrawEigenSpectrum(SKCanvas canvas, GeometryRun run, double time, int width, int height)
    {
        var eigenvalues = run.Geometry?.Eigenvalues;
        if (eigenvalues == null || eigenvalues.Count == 0) return;

        var eigenIdx = (int)(time * (eigenvalues.Count - 1));
        eigenIdx = Math.Clamp(eigenIdx, 0, eigenvalues.Count - 1);
        var eigen = eigenvalues[eigenIdx];

        if (eigen.Values.Count == 0) return;

        var total = eigen.Values.Sum();
        if (total <= 0) return;

        // Draw small bar chart in corner
        var chartX = width - 200f;
        var chartY = height - 120f;
        var barWidth = 30f;
        var maxBarHeight = 80f;

        using var barPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var textFont = new SKFont(SKTypeface.Default, 12);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            IsAntialias = true
        };

        var colors = new[]
        {
            SKColor.Parse("#4ecdc4"),
            SKColor.Parse("#ff9f43"),
            SKColor.Parse("#a29bfe"),
            SKColor.Parse("#fd79a8"),
            SKColor.Parse("#ffd93d")
        };

        for (int i = 0; i < Math.Min(eigen.Values.Count, 5); i++)
        {
            var fraction = eigen.Values[i] / total;
            var barHeight = (float)(fraction * maxBarHeight);
            var x = chartX + i * (barWidth + 5);

            barPaint.Color = colors[i % colors.Length];
            canvas.DrawRect(x, chartY + maxBarHeight - barHeight, barWidth, barHeight, barPaint);
            canvas.DrawText($"λ{i + 1}", x + barWidth / 2, chartY + maxBarHeight + 15, SKTextAlign.Center, textFont, textPaint);
        }
    }

    private void DrawTitleBar(SKCanvas canvas, GeometryRun leftRun, GeometryRun rightRun, double time, int width)
    {
        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#1a1a2e"),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, width, 60, bgPaint);

        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 20);
        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        canvas.DrawText("ScalarScope Training Dynamics Comparison", width / 2f, 35, SKTextAlign.Center, titleFont, titlePaint);

        using var subtitleFont = new SKFont(SKTypeface.Default, 12);
        using var subtitlePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(150),
            IsAntialias = true
        };

        canvas.DrawText($"Time: {time:P0} | Left: {leftRun.Metadata.Condition} | Right: {rightRun.Metadata.Condition}",
            width / 2f, 52, SKTextAlign.Center, subtitleFont, subtitlePaint);
    }

    private void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint)
    {
        canvas.DrawLine(from, to, paint);

        var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var headLen = 10f;
        var headAngle = 0.5f;

        var p1 = new SKPoint(
            to.X - headLen * MathF.Cos(angle - headAngle),
            to.Y - headLen * MathF.Sin(angle - headAngle));
        var p2 = new SKPoint(
            to.X - headLen * MathF.Cos(angle + headAngle),
            to.Y - headLen * MathF.Sin(angle + headAngle));

        canvas.DrawLine(to, p1, paint);
        canvas.DrawLine(to, p2, paint);
    }

    private static SKPoint ToScreen(IList<double> state, SKPoint center, float scale)
    {
        if (state.Count < 2) return center;
        return new SKPoint(
            center.X + (float)state[0] * scale,
            center.Y - (float)state[1] * scale);
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore cleanup errors */ }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { /* ignore cleanup errors */ }
    }
}

public record ExportOptions
{
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Fps { get; init; }
    public double? Duration { get; init; }
    public string? Label { get; init; }
    public SKColor? AccentColor { get; init; }
    public bool ShowProfessors { get; init; } = true;
    public bool ShowMetrics { get; init; } = true;
    public bool ShowEigenvalues { get; init; } = true;
    public bool ShowAnnotations { get; init; } = false;
}

/// <summary>
/// Result of export validation.
/// </summary>
public record ExportValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ExportValidationResult Success() => new() { IsValid = true };
    public static ExportValidationResult Error(string message) => new() { IsValid = false, ErrorMessage = message };
}

/// <summary>
/// Result of a single-file export operation.
/// </summary>
public record ExportResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Warning { get; init; }

    public static ExportResult Succeeded(string path, string? warning = null) =>
        new() { IsSuccess = true, OutputPath = path, Warning = warning };

    public static ExportResult Failure(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };

    public static ExportResult Cancelled() =>
        new() { IsSuccess = false, IsCancelled = true };
}

/// <summary>
/// Result of a frame sequence export operation.
/// </summary>
public record ExportSequenceResult
{
    public bool IsSuccess { get; init; }
    public bool IsCancelled { get; init; }
    public string[]? Paths { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// Progress information during export.
/// </summary>
public record ExportProgress
{
    public int CurrentFrame { get; init; }
    public int TotalFrames { get; init; }
    public int PercentComplete { get; init; }
    public string? CurrentFile { get; init; }
}
