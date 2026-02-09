using ScalarScope.Models;
using ScalarScope.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Canvas for overlaying multiple trajectories in a single view.
/// Supports deviation highlighting, statistical bands, and distance metrics display.
/// Phase 3.2 - Comparative Analysis
/// </summary>
public class OverlayComparisonCanvas : SKCanvasView
{
    #region Bindable Properties

    public static readonly BindableProperty RunsProperty =
        BindableProperty.Create(nameof(Runs), typeof(IList<GeometryRun>), typeof(OverlayComparisonCanvas),
            propertyChanged: OnRunsChanged);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(OverlayComparisonCanvas), 0.0,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ShowDeviationProperty =
        BindableProperty.Create(nameof(ShowDeviation), typeof(bool), typeof(OverlayComparisonCanvas), true,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ShowStatisticalBandsProperty =
        BindableProperty.Create(nameof(ShowStatisticalBands), typeof(bool), typeof(OverlayComparisonCanvas), false,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ShowDistanceMetricsProperty =
        BindableProperty.Create(nameof(ShowDistanceMetrics), typeof(bool), typeof(OverlayComparisonCanvas), true,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ConfidenceLevelProperty =
        BindableProperty.Create(nameof(ConfidenceLevel), typeof(double), typeof(OverlayComparisonCanvas), 0.95,
            propertyChanged: OnInvalidate);

    public IList<GeometryRun>? Runs
    {
        get => (IList<GeometryRun>?)GetValue(RunsProperty);
        set => SetValue(RunsProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public bool ShowDeviation
    {
        get => (bool)GetValue(ShowDeviationProperty);
        set => SetValue(ShowDeviationProperty, value);
    }

    public bool ShowStatisticalBands
    {
        get => (bool)GetValue(ShowStatisticalBandsProperty);
        set => SetValue(ShowStatisticalBandsProperty, value);
    }

    public bool ShowDistanceMetrics
    {
        get => (bool)GetValue(ShowDistanceMetricsProperty);
        set => SetValue(ShowDistanceMetricsProperty, value);
    }

    public double ConfidenceLevel
    {
        get => (double)GetValue(ConfidenceLevelProperty);
        set => SetValue(ConfidenceLevelProperty, value);
    }

    #endregion

    #region Palette

    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");
    private static readonly SKColor DeviationColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor BandColor = SKColor.Parse("#74b9ff");

    private static readonly SKColor[] TrajectoryPalette =
    [
        SKColor.Parse("#00d2d3"), // Cyan
        SKColor.Parse("#ff9f43"), // Orange
        SKColor.Parse("#a29bfe"), // Purple
        SKColor.Parse("#2ecc71"), // Green
        SKColor.Parse("#fd79a8"), // Pink
        SKColor.Parse("#ffeaa7"), // Yellow
        SKColor.Parse("#e17055"), // Coral
        SKColor.Parse("#81ecec")  // Teal
    ];

    #endregion

    private float _scale = 100f;
    private SKPoint _center;
    private readonly ComparativeAnalysisService _analysisService = new();
    
    // Cached analysis results
    private OverlayAlignment? _cachedAlignment;
    private DeviationAnalysis? _cachedDeviation;
    private StatisticalBands? _cachedBands;
    private DtwResult? _cachedDtw;
    private FrechetResult? _cachedFrechet;

    // Phase 1: Demo state fields
    private IList<GeometryRun>? _currentRenderRuns;
    private bool _isRenderingDemo;

    public OverlayComparisonCanvas()
    {
        PaintSurface += OnPaintSurface;
        
        // Phase 1: Subscribe to demo animation for continuous repainting
        DemoStateService.Instance.OnAnimationFrame += OnDemoAnimationFrame;
    }

    private void OnDemoAnimationFrame()
    {
        // Only repaint if we're showing demo data
        if (Runs is null || Runs.Count == 0)
        {
            MainThread.BeginInvokeOnMainThread(InvalidateSurface);
        }
    }

    private static void OnRunsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OverlayComparisonCanvas canvas)
        {
            canvas.InvalidateCache();
            canvas.InvalidateSurface();
        }
    }

    private static void OnInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is OverlayComparisonCanvas canvas)
            canvas.InvalidateSurface();
    }

    private void InvalidateCache()
    {
        _cachedAlignment = null;
        _cachedDeviation = null;
        _cachedBands = null;
        _cachedDtw = null;
        _cachedFrechet = null;
    }

    private void EnsureAnalysis()
    {
        var runs = _currentRenderRuns;
        if (runs == null || runs.Count == 0) return;

        // Compute alignment for all runs
        _cachedAlignment ??= _analysisService.AlignForOverlay(runs);

        // Compute deviation between first two runs
        if (runs.Count >= 2 && _cachedDeviation == null)
        {
            _cachedDeviation = _analysisService.ComputeDeviation(runs[0], runs[1]);
            _cachedDtw = _analysisService.ComputeDtw(runs[0], runs[1]);
            _cachedFrechet = _analysisService.ComputeFrechet(runs[0], runs[1]);
        }

        // Compute statistical bands if multiple runs
        if (runs.Count >= 2 && _cachedBands == null)
        {
            _cachedBands = _analysisService.ComputeStatisticalBands(runs, ConfidenceLevel);
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        _center = new SKPoint(info.Width / 2f, info.Height / 2f);
        _scale = Math.Min(info.Width, info.Height) / 3f;

        DrawGrid(canvas, info);

        // Phase 1: Use demo runs when no real runs available
        _currentRenderRuns = Runs;
        _isRenderingDemo = false;
        
        if (_currentRenderRuns is null || _currentRenderRuns.Count == 0)
        {
            var demoA = DemoStateService.Instance.DemoPathA;
            var demoB = DemoStateService.Instance.DemoPathB;
            if (demoA != null && demoB != null)
            {
                _currentRenderRuns = [demoA, demoB];
                _isRenderingDemo = true;
            }
        }

        if (_currentRenderRuns is null || _currentRenderRuns.Count == 0)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        EnsureAnalysis();

        // Draw statistical bands first (background)
        if (ShowStatisticalBands && _cachedBands?.IsValid == true)
        {
            DrawStatisticalBands(canvas, info);
        }

        // Draw deviation regions (behind trajectories)
        if (ShowDeviation && _currentRenderRuns.Count >= 2 && _cachedDeviation?.IsValid == true)
        {
            DrawDeviationRegions(canvas, info);
        }

        // Draw all trajectories
        if (_cachedAlignment?.IsValid == true)
        {
            DrawOverlayTrajectories(canvas, info);
        }

        // Draw current positions
        DrawCurrentPositions(canvas, info);

        // Draw legend
        DrawLegend(canvas, info);

        // Draw metrics
        if (ShowDistanceMetrics)
        {
            DrawDistanceMetrics(canvas, info);
        }
        
        // Phase 1: Draw demo badge if showing demo data
        if (_isRenderingDemo)
        {
            DrawDemoBadge(canvas, info);
        }
    }

    #region Drawing Methods

    private void DrawGrid(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            StrokeWidth = 1,
            IsAntialias = true
        };

        canvas.DrawLine(0, _center.Y, info.Width, _center.Y, paint);
        canvas.DrawLine(_center.X, 0, _center.X, info.Height, paint);

        paint.PathEffect = SKPathEffect.CreateDash([5, 5], 0);
        for (int i = -2; i <= 2; i++)
        {
            if (i == 0) continue;
            var offset = i * _scale / 2;
            canvas.DrawLine(0, _center.Y + offset, info.Width, _center.Y + offset, paint);
            canvas.DrawLine(_center.X + offset, 0, _center.X + offset, info.Height, paint);
        }
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true
        };
        canvas.DrawText("Load runs to compare", _center.X, _center.Y, SKTextAlign.Center, font, paint);
    }

    private void DrawStatisticalBands(SKCanvas canvas, SKImageInfo info)
    {
        var bands = _cachedBands;
        if (bands?.Points.Count < 2) return;

        var points = bands!.Points;

        // Draw confidence ellipses along the mean trajectory
        using var bandPaint = new SKPaint
        {
            Color = BandColor.WithAlpha(40),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var meanPaint = new SKPaint
        {
            Color = BandColor.WithAlpha(150),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([4, 4], 0)
        };

        // Draw mean trajectory
        using var meanPath = new SKPath();
        for (int i = 0; i < points.Count; i++)
        {
            var screenPos = NormalizedToScreen(points[i].MeanX, points[i].MeanY, info);
            if (i == 0)
                meanPath.MoveTo(screenPos);
            else
                meanPath.LineTo(screenPos);
        }
        canvas.DrawPath(meanPath, meanPaint);

        // Draw confidence band as series of ellipses at intervals
        var step = Math.Max(1, points.Count / 30);
        for (int i = 0; i < points.Count; i += step)
        {
            var point = points[i];
            var screenPos = NormalizedToScreen(point.MeanX, point.MeanY, info);
            var radiusX = (float)(point.ConfidenceRadiusX * _scale * 2);
            var radiusY = (float)(point.ConfidenceRadiusY * _scale * 2);

            // Clamp radius to reasonable size
            radiusX = Math.Clamp(radiusX, 2, 50);
            radiusY = Math.Clamp(radiusY, 2, 50);

            canvas.DrawOval(screenPos.X, screenPos.Y, radiusX, radiusY, bandPaint);
        }
    }

    private void DrawDeviationRegions(SKCanvas canvas, SKImageInfo info)
    {
        var deviation = _cachedDeviation;
        if (deviation?.DivergenceRegions.Count == 0) return;

        using var paint = new SKPaint
        {
            Color = DeviationColor.WithAlpha(30),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var borderPaint = new SKPaint
        {
            Color = DeviationColor.WithAlpha(100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([4, 4], 0)
        };

        foreach (var region in deviation!.DivergenceRegions)
        {
            // Draw connecting lines between divergent points
            var deviationPoints = deviation.Points
                .Where(p => p.Time >= region.StartTime && p.Time <= region.EndTime)
                .ToList();

            foreach (var point in deviationPoints)
            {
                var p1 = WorldToScreen((float)point.Position1.X, (float)point.Position1.Y, info);
                var p2 = WorldToScreen((float)point.Position2.X, (float)point.Position2.Y, info);

                // Draw connecting line with intensity based on deviation
                var intensity = (byte)(50 + Math.Min(100, point.Deviation * 100));
                paint.Color = DeviationColor.WithAlpha(intensity);
                canvas.DrawLine(p1, p2, paint);
            }
        }
    }

    private void DrawOverlayTrajectories(SKCanvas canvas, SKImageInfo info)
    {
        var alignment = _cachedAlignment;
        if (alignment == null) return;

        foreach (var trajectory in alignment.Trajectories)
        {
            var color = TrajectoryPalette[trajectory.ColorIndex % TrajectoryPalette.Length];
            DrawSingleTrajectory(canvas, info, trajectory, color);
        }
    }

    private void DrawSingleTrajectory(SKCanvas canvas, SKImageInfo info, AlignedTrajectory trajectory, SKColor color)
    {
        var points = trajectory.Points;
        if (points.Count < 2) return;

        var maxIdx = (int)(CurrentTime * (points.Count - 1));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        for (int i = 1; i <= maxIdx && i < points.Count; i++)
        {
            var t = (float)i / points.Count;
            // Fade from semi-transparent to solid
            paint.Color = color.WithAlpha((byte)(120 + t * 135));

            var p1 = NormalizedToScreen(points[i - 1].x, points[i - 1].y, info);
            var p2 = NormalizedToScreen(points[i].x, points[i].y, info);
            canvas.DrawLine(p1, p2, paint);
        }
    }

    private void DrawCurrentPositions(SKCanvas canvas, SKImageInfo info)
    {
        var alignment = _cachedAlignment;
        if (alignment == null) return;

        foreach (var trajectory in alignment.Trajectories)
        {
            if (trajectory.Points.Count == 0) continue;

            var idx = (int)(CurrentTime * (trajectory.Points.Count - 1));
            idx = Math.Clamp(idx, 0, trajectory.Points.Count - 1);
            var point = trajectory.Points[idx];

            var pos = NormalizedToScreen(point.x, point.y, info);
            var color = TrajectoryPalette[trajectory.ColorIndex % TrajectoryPalette.Length];

            // Glow
            using var glowPaint = new SKPaint
            {
                Color = color.WithAlpha(80),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 6)
            };
            canvas.DrawCircle(pos, 10, glowPaint);

            // Core
            using var corePaint = new SKPaint
            {
                Color = SKColors.White,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(pos, 4, corePaint);

            // Color ring
            using var ringPaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true
            };
            canvas.DrawCircle(pos, 4, ringPaint);
        }
    }

    private void DrawLegend(SKCanvas canvas, SKImageInfo info)
    {
        var alignment = _cachedAlignment;
        if (alignment == null || alignment.Trajectories.Count == 0) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 11);
        using var smallFont = new SKFont(SKTypeface.Default, 9);
        using var paint = new SKPaint { IsAntialias = true };

        var y = 15f;
        var x = 15f;

        foreach (var trajectory in alignment.Trajectories)
        {
            var color = TrajectoryPalette[trajectory.ColorIndex % TrajectoryPalette.Length];

            // Color swatch
            paint.Color = color;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(x + 5, y, 5, paint);

            // Run ID
            paint.Color = color;
            canvas.DrawText(trajectory.RunId, x + 15, y + 4, SKTextAlign.Left, font, paint);

            // Condition
            paint.Color = SKColors.Gray;
            canvas.DrawText(trajectory.Condition, x + 15, y + 16, SKTextAlign.Left, smallFont, paint);

            y += 28;
        }
    }

    private void DrawDistanceMetrics(SKCanvas canvas, SKImageInfo info)
    {
        if (_cachedDtw == null || _cachedFrechet == null) return;

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 12);
        using var smallFont = new SKFont(SKTypeface.Default, 10);
        using var paint = new SKPaint { IsAntialias = true };

        var x = info.Width - 15f;
        var y = 15f;

        // Header
        paint.Color = SKColors.White;
        canvas.DrawText("Distance Metrics", x, y, SKTextAlign.Right, font, paint);
        y += 18;

        // DTW
        paint.Color = SKColor.Parse("#74b9ff");
        if (_cachedDtw.IsValid)
        {
            canvas.DrawText($"DTW: {_cachedDtw.NormalizedDistance:F4}", x, y, SKTextAlign.Right, smallFont, paint);
            y += 14;
        }

        // Fréchet
        paint.Color = SKColor.Parse("#a29bfe");
        if (_cachedFrechet.IsValid)
        {
            canvas.DrawText($"Fréchet: {_cachedFrechet.Distance:F4}", x, y, SKTextAlign.Right, smallFont, paint);
            y += 14;
        }

        // Deviation stats
        if (_cachedDeviation?.IsValid == true)
        {
            paint.Color = DeviationColor;
            canvas.DrawText($"Max Δ: {_cachedDeviation.MaxDeviation:F4}", x, y, SKTextAlign.Right, smallFont, paint);
            y += 14;
            canvas.DrawText($"Mean Δ: {_cachedDeviation.MeanDeviation:F4}", x, y, SKTextAlign.Right, smallFont, paint);
            y += 14;

            // Divergence count
            var divergences = _cachedDeviation.DivergenceRegions.Count;
            if (divergences > 0)
            {
                paint.Color = SKColor.Parse("#ff9f43");
                canvas.DrawText($"Divergences: {divergences}", x, y, SKTextAlign.Right, smallFont, paint);
            }
        }
    }

    #endregion

    #region Coordinate Transforms

    private SKPoint NormalizedToScreen(double nx, double ny, SKImageInfo info)
    {
        // Convert normalized [0,1] to screen coords
        var margin = 50f;
        var drawWidth = info.Width - 2 * margin;
        var drawHeight = info.Height - 2 * margin;

        return new SKPoint(
            margin + (float)(nx * drawWidth),
            margin + (float)((1 - ny) * drawHeight) // Flip Y
        );
    }

    private SKPoint WorldToScreen(float x, float y, SKImageInfo info)
    {
        return new SKPoint(
            _center.X + x * _scale,
            _center.Y - y * _scale // Flip Y
        );
    }

    /// <summary>
    /// Phase 1: Draw "DEMO" badge in corner when showing demo data.
    /// </summary>
    private void DrawDemoBadge(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 10);
        using var bgPaint = new SKPaint
        {
            Color = DemoStateService.Instance.GetDemoBadgeColor().WithAlpha(180),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        var text = "DEMO";
        var textBounds = new SKRect();
        using var tempPaint = new SKPaint();
        tempPaint.MeasureText(text, ref textBounds);

        var padding = 4f;
        var rect = new SKRect(
            info.Width - textBounds.Width - padding * 2 - 8,
            8,
            info.Width - 8,
            8 + textBounds.Height + padding * 2
        );

        canvas.DrawRoundRect(rect, 3, 3, bgPaint);
        canvas.DrawText(text, rect.MidX, rect.MidY + textBounds.Height / 2, SKTextAlign.Center, font, textPaint);
    }

    #endregion
}
