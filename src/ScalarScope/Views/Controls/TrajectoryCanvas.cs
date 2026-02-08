using ScalarScope.Models;
using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Color modes for trajectory visualization.
/// </summary>
public enum TrajectoryColorMode
{
    /// <summary>Color by time (start to end gradient).</summary>
    Time,
    /// <summary>Color by velocity magnitude (slow=blue, fast=red).</summary>
    Velocity,
    /// <summary>Color by curvature (low=blue, high=orange).</summary>
    Curvature
}

/// <summary>
/// Core vortex visualization: trajectory flow field in reduced space.
/// Shows the path through state space with velocity vectors and curvature.
/// </summary>
public class TrajectoryCanvas : SKCanvasView
{
    public static readonly BindableProperty SessionProperty =
        BindableProperty.Create(nameof(Session), typeof(VortexSessionViewModel), typeof(TrajectoryCanvas),
            propertyChanged: OnSessionChanged);

    public static readonly BindableProperty ShowVelocityProperty =
        BindableProperty.Create(nameof(ShowVelocity), typeof(bool), typeof(TrajectoryCanvas), true);

    public static readonly BindableProperty ShowCurvatureProperty =
        BindableProperty.Create(nameof(ShowCurvature), typeof(bool), typeof(TrajectoryCanvas), true);

    public static readonly BindableProperty ShowProfessorsProperty =
        BindableProperty.Create(nameof(ShowProfessors), typeof(bool), typeof(TrajectoryCanvas), true);

    public static readonly BindableProperty ShowHeatMapProperty =
        BindableProperty.Create(nameof(ShowHeatMap), typeof(bool), typeof(TrajectoryCanvas), false);

    public static readonly BindableProperty ShowVectorFieldProperty =
        BindableProperty.Create(nameof(ShowVectorField), typeof(bool), typeof(TrajectoryCanvas), false);

    public static readonly BindableProperty ColorModeProperty =
        BindableProperty.Create(nameof(ColorMode), typeof(TrajectoryColorMode), typeof(TrajectoryCanvas), TrajectoryColorMode.Time);

    // Hover state for tooltips
    public static readonly BindableProperty HoveredPointProperty =
        BindableProperty.Create(nameof(HoveredPoint), typeof(TrajectoryTimestep), typeof(TrajectoryCanvas));

    public static readonly BindableProperty IsHoveringProperty =
        BindableProperty.Create(nameof(IsHovering), typeof(bool), typeof(TrajectoryCanvas), false);

    public VortexSessionViewModel? Session
    {
        get => (VortexSessionViewModel?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public bool ShowVelocity
    {
        get => (bool)GetValue(ShowVelocityProperty);
        set => SetValue(ShowVelocityProperty, value);
    }

    public bool ShowCurvature
    {
        get => (bool)GetValue(ShowCurvatureProperty);
        set => SetValue(ShowCurvatureProperty, value);
    }

    public bool ShowProfessors
    {
        get => (bool)GetValue(ShowProfessorsProperty);
        set => SetValue(ShowProfessorsProperty, value);
    }

    public bool ShowHeatMap
    {
        get => (bool)GetValue(ShowHeatMapProperty);
        set => SetValue(ShowHeatMapProperty, value);
    }

    public bool ShowVectorField
    {
        get => (bool)GetValue(ShowVectorFieldProperty);
        set => SetValue(ShowVectorFieldProperty, value);
    }

    public TrajectoryColorMode ColorMode
    {
        get => (TrajectoryColorMode)GetValue(ColorModeProperty);
        set => SetValue(ColorModeProperty, value);
    }

    public TrajectoryTimestep? HoveredPoint
    {
        get => (TrajectoryTimestep?)GetValue(HoveredPointProperty);
        set => SetValue(HoveredPointProperty, value);
    }

    public bool IsHovering
    {
        get => (bool)GetValue(IsHoveringProperty);
        set => SetValue(IsHoveringProperty, value);
    }

    // Zoom and pan state
    private float _zoomLevel = 1.0f;
    private SKPoint _panOffset = SKPoint.Empty;
    private SKPoint? _lastPanPoint;

    // Hover detection
    private SKPoint? _hoverPoint;
    private const float HoverRadius = 20f;

    // Colors
    private static new readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");
    private static readonly SKColor TrajectoryStartColor = SKColor.Parse("#00d9ff");
    private static readonly SKColor TrajectoryEndColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor VelocityColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor CurvatureHighColor = SKColor.Parse("#ff9f43");
    private static readonly SKColor ProfessorColor = SKColor.Parse("#a29bfe");
    private static readonly SKColor HoldoutColor = SKColor.Parse("#fd79a8");

    // Heat map colors (cool to hot)
    private static readonly SKColor[] HeatMapGradient = new[]
    {
        SKColor.Parse("#000033"), // Deep blue - lowest density
        SKColor.Parse("#0066cc"), // Blue
        SKColor.Parse("#00cc99"), // Cyan-green
        SKColor.Parse("#66ff33"), // Green
        SKColor.Parse("#ffff00"), // Yellow
        SKColor.Parse("#ff6600"), // Orange
        SKColor.Parse("#ff0000"), // Red - highest density
    };

    // Velocity color mode (slow to fast)
    private static readonly SKColor VelocitySlowColor = SKColor.Parse("#3498db"); // Blue - slow
    private static readonly SKColor VelocityFastColor = SKColor.Parse("#e74c3c"); // Red - fast

    // Curvature color mode (low to high)
    private static readonly SKColor CurvatureLowColor = SKColor.Parse("#2ecc71"); // Green - smooth
    private static readonly SKColor CurvatureHighModeColor = SKColor.Parse("#e67e22"); // Orange - sharp turn

    // Vector field arrows
    private static readonly SKColor VectorFieldColor = SKColor.Parse("#4a5568"); // Muted gray-blue

    private float _scale = 100f;
    private SKPoint _center;

    // Throttled rendering for large runs
    private DateTime _lastRenderTime = DateTime.MinValue;
    private bool _renderPending;
    private const double ThrottleIntervalMs = 33.33; // ~30fps during scrubbing

    public TrajectoryCanvas()
    {
        PaintSurface += OnPaintSurface;
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        switch (e.ActionType)
        {
            case SKTouchAction.Entered:
            case SKTouchAction.Moved:
                _hoverPoint = e.Location;
                UpdateHoveredPoint();
                InvalidateSurface();
                break;

            case SKTouchAction.Exited:
            case SKTouchAction.Cancelled:
                _hoverPoint = null;
                HoveredPoint = null;
                IsHovering = false;
                InvalidateSurface();
                break;

            case SKTouchAction.Pressed:
                _lastPanPoint = e.Location;
                break;

            case SKTouchAction.Released:
                _lastPanPoint = null;
                break;

            case SKTouchAction.WheelChanged:
                // Zoom with mouse wheel
                var zoomDelta = e.WheelDelta > 0 ? 1.1f : 0.9f;
                _zoomLevel = Math.Clamp(_zoomLevel * zoomDelta, 0.25f, 4f);
                InvalidateSurface();
                break;
        }

        e.Handled = true;
    }

    private void UpdateHoveredPoint()
    {
        if (_hoverPoint == null || Session?.Run?.Trajectory?.Timesteps == null)
        {
            HoveredPoint = null;
            IsHovering = false;
            return;
        }

        var points = Session.Run.Trajectory.Timesteps;
        TrajectoryTimestep? closest = null;
        var minDist = float.MaxValue;

        foreach (var pt in points)
        {
            if (pt.State2D.Count < 2) continue;
            var screenPt = ToScreen(pt.State2D);
            var dist = SKPoint.Distance(screenPt, _hoverPoint.Value);
            if (dist < minDist && dist < HoverRadius * _zoomLevel)
            {
                minDist = dist;
                closest = pt;
            }
        }

        HoveredPoint = closest;
        IsHovering = closest != null;
    }

    private static void OnSessionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TrajectoryCanvas canvas)
        {
            if (oldValue is VortexSessionViewModel oldSession)
            {
                oldSession.Player.TimeChanged -= canvas.OnTimeChanged;
            }
            if (newValue is VortexSessionViewModel newSession)
            {
                newSession.Player.TimeChanged += canvas.OnTimeChanged;
            }
            canvas.InvalidateSurface();
        }
    }

    private void OnTimeChanged()
    {
        // Throttle rendering for large runs
        if (Session?.Player.IsLargeRun == true)
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastRenderTime).TotalMilliseconds;

            if (elapsed >= ThrottleIntervalMs)
            {
                _lastRenderTime = now;
                MainThread.BeginInvokeOnMainThread(InvalidateSurface);
            }
            else if (!_renderPending)
            {
                // Schedule a render for when throttle period ends
                _renderPending = true;
                var delay = (int)(ThrottleIntervalMs - elapsed);
                _ = Task.Delay(delay).ContinueWith(_ =>
                {
                    _renderPending = false;
                    _lastRenderTime = DateTime.Now;
                    MainThread.BeginInvokeOnMainThread(InvalidateSurface);
                });
            }
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(InvalidateSurface);
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        _center = new SKPoint(info.Width / 2f + _panOffset.X, info.Height / 2f + _panOffset.Y);
        _scale = Math.Min(info.Width, info.Height) / 4f * _zoomLevel;

        DrawGrid(canvas, info);

        if (Session?.Run is null)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Invariant check: trajectory must be non-empty before rendering
        if (!InvariantGuard.AssertTrajectoryNonEmpty(Session.Run, "TrajectoryCanvas.OnPaintSurface"))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        if (ShowProfessors)
        {
            DrawProfessorVectors(canvas);
        }

        if (ShowHeatMap)
        {
            DrawHeatMap(canvas, info);
        }

        if (ShowVectorField)
        {
            DrawVectorFieldGrid(canvas, info);
        }

        DrawTrajectory(canvas);

        if (ShowVelocity)
        {
            DrawVelocityVectors(canvas);
        }

        if (ShowCurvature)
        {
            DrawCurvatureMarkers(canvas);
        }

        DrawCurrentPosition(canvas);
        DrawLegend(canvas, info);
        DrawHoverTooltip(canvas, info);
        DrawZoomIndicator(canvas, info);
    }

    private void DrawHoverTooltip(SKCanvas canvas, SKImageInfo info)
    {
        if (!IsHovering || HoveredPoint == null || _hoverPoint == null) return;

        var pt = HoveredPoint;
        var screenPt = ToScreen(pt.State2D);

        // Tooltip background
        var tooltipText = new[]
        {
            $"t = {pt.Time:F3}",
            $"x = {pt.State2D[0]:F4}",
            $"y = {pt.State2D[1]:F4}",
            $"vel = {pt.VelocityMagnitude:F4}",
            $"curv = {pt.Curvature:F4}",
            $"dim = {pt.EffectiveDim:F2}"
        };

        using var textFont = new SKFont(SKTypeface.Default, 12);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#ee1a1a2e"),
            Style = SKPaintStyle.Fill
        };

        using var borderPaint = new SKPaint
        {
            Color = SKColor.Parse("#00d9ff"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };

        // Calculate tooltip size
        var lineHeight = 16f;
        var padding = 8f;
        var maxWidth = tooltipText.Max(t => textFont.MeasureText(t, textPaint)) + padding * 2;
        var height = tooltipText.Length * lineHeight + padding * 2;

        // Position tooltip (offset from hover point, keep on screen)
        var tooltipX = Math.Min(screenPt.X + 15, info.Width - maxWidth - 10);
        var tooltipY = Math.Min(screenPt.Y - height / 2, info.Height - height - 10);
        tooltipY = Math.Max(tooltipY, 10);

        var rect = new SKRect(tooltipX, tooltipY, tooltipX + maxWidth, tooltipY + height);
        canvas.DrawRoundRect(rect, 5, 5, bgPaint);
        canvas.DrawRoundRect(rect, 5, 5, borderPaint);

        // Draw text lines
        var y = tooltipY + padding + 12;
        foreach (var line in tooltipText)
        {
            canvas.DrawText(line, tooltipX + padding, y, SKTextAlign.Left, textFont, textPaint);
            y += lineHeight;
        }

        // Highlight the hovered point
        using var highlightPaint = new SKPaint
        {
            Color = SKColor.Parse("#00d9ff"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawCircle(screenPt, 8, highlightPaint);
    }

    private void DrawZoomIndicator(SKCanvas canvas, SKImageInfo info)
    {
        if (Math.Abs(_zoomLevel - 1.0f) < 0.01f && _panOffset == SKPoint.Empty) return;

        using var font = new SKFont(SKTypeface.Default, 11);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(150),
            IsAntialias = true
        };

        var text = $"Zoom: {_zoomLevel:F1}x";
        var x = info.Width - font.MeasureText(text, paint) - 10;
        canvas.DrawText(text, x, info.Height - 10, SKTextAlign.Left, font, paint);
    }

    /// <summary>
    /// Reset zoom and pan to default.
    /// </summary>
    public void ResetView()
    {
        _zoomLevel = 1.0f;
        _panOffset = SKPoint.Empty;
        InvalidateSurface();
    }

    private void DrawGrid(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Draw axes
        canvas.DrawLine(0, _center.Y, info.Width, _center.Y, paint);
        canvas.DrawLine(_center.X, 0, _center.X, info.Height, paint);

        // Draw grid lines
        paint.PathEffect = SKPathEffect.CreateDash([5, 5], 0);
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            var offset = i * _scale / 2;
            canvas.DrawLine(0, _center.Y + offset, info.Width, _center.Y + offset, paint);
            canvas.DrawLine(_center.X + offset, 0, _center.X + offset, info.Height, paint);
        }
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 18);
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true
        };
        canvas.DrawText("Load a geometry run to visualize", _center.X, _center.Y, SKTextAlign.Center, font, paint);
    }

    private void DrawTrajectory(SKCanvas canvas)
    {
        var points = Session!.GetTrajectoryUpToTime(Session.Player.Time).ToList();
        if (points.Count < 2) return;

        // Calculate stats for color modes
        var maxVelocity = points.Max(p => p.VelocityMagnitude);
        if (maxVelocity < 0.0001) maxVelocity = 1.0;

        var maxCurvature = points.Max(p => p.Curvature);
        if (maxCurvature < 0.0001) maxCurvature = 1.0;

        // Draw glow layer first (behind main trajectory)
        DrawTrajectoryGlow(canvas, points, maxVelocity, maxCurvature);

        // Draw main trajectory with Catmull-Rom splines
        DrawTrajectorySpline(canvas, points, maxVelocity, maxCurvature);
    }

    /// <summary>
    /// Gets the trajectory color based on the current ColorMode.
    /// </summary>
    private SKColor GetTrajectoryColor(TrajectoryTimestep point, int index, int totalPoints, double maxVelocity, double maxCurvature)
    {
        return ColorMode switch
        {
            TrajectoryColorMode.Velocity => GetVelocityColor(point.VelocityMagnitude, maxVelocity),
            TrajectoryColorMode.Curvature => GetCurvatureModeColor(point.Curvature, maxCurvature),
            _ => GetTimeColor(index, totalPoints) // Default: Time mode
        };
    }

    private SKColor GetTimeColor(int index, int totalPoints)
    {
        var t = (float)index / totalPoints;
        return InterpolateColor(TrajectoryStartColor, TrajectoryEndColor, t);
    }

    private SKColor GetVelocityColor(double velocity, double maxVelocity)
    {
        var t = (float)Math.Clamp(velocity / maxVelocity, 0, 1);
        return InterpolateColor(VelocitySlowColor, VelocityFastColor, t);
    }

    private SKColor GetCurvatureModeColor(double curvature, double maxCurvature)
    {
        var t = (float)Math.Clamp(curvature / maxCurvature, 0, 1);
        return InterpolateColor(CurvatureLowColor, CurvatureHighModeColor, t);
    }

    private void DrawTrajectoryGlow(SKCanvas canvas, List<TrajectoryTimestep> points, double maxVelocity, double maxCurvature)
    {
        using var glowPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        // Draw multiple glow passes with decreasing opacity and increasing width
        for (int glowPass = 3; glowPass >= 1; glowPass--)
        {
            var glowWidth = 8f + glowPass * 4f;
            var glowAlpha = (byte)(30 / glowPass);

            for (int i = 1; i < points.Count; i++)
            {
                var t = (float)i / points.Count;
                var opacity = GetTrailOpacity(t, points.Count);
                
                var color = GetTrajectoryColor(points[i], i, points.Count, maxVelocity, maxCurvature);
                glowPaint.Color = color.WithAlpha((byte)(glowAlpha * opacity));
                glowPaint.StrokeWidth = glowWidth;
                glowPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, glowPass * 2);

                var p1 = ToScreen(points[i - 1].State2D);
                var p2 = ToScreen(points[i].State2D);
                canvas.DrawLine(p1, p2, glowPaint);
            }
        }
    }

    private void DrawTrajectorySpline(SKCanvas canvas, List<TrajectoryTimestep> points, double maxVelocity, double maxCurvature)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        // Need at least 4 points for Catmull-Rom
        if (points.Count >= 4)
        {
            // Draw Catmull-Rom spline segments
            for (int i = 1; i < points.Count - 2; i++)
            {
                DrawCatmullRomSegment(canvas, paint, points, i, maxVelocity, maxCurvature);
            }
            
            // Draw the first and last segments as simple lines (not enough control points)
            DrawSimpleSegment(canvas, paint, points, 0, maxVelocity, maxCurvature);
            DrawSimpleSegment(canvas, paint, points, points.Count - 2, maxVelocity, maxCurvature);
        }
        else
        {
            // Fallback to simple lines for very short trajectories
            for (int i = 1; i < points.Count; i++)
            {
                DrawSimpleSegment(canvas, paint, points, i - 1, maxVelocity, maxCurvature);
            }
        }
    }

    private void DrawCatmullRomSegment(SKCanvas canvas, SKPaint paint, List<TrajectoryTimestep> points, int i, double maxVelocity, double maxCurvature)
    {
        var p0 = ToScreen(points[i - 1].State2D);
        var p1 = ToScreen(points[i].State2D);
        var p2 = ToScreen(points[i + 1].State2D);
        var p3 = ToScreen(points[i + 2].State2D);

        var t = (float)(i + 1) / points.Count;
        var opacity = GetTrailOpacity(t, points.Count);
        var velocity = points[i + 1].VelocityMagnitude;
        var strokeWidth = GetAdaptiveStrokeWidth(velocity, maxVelocity);

        var color = GetTrajectoryColor(points[i + 1], i + 1, points.Count, maxVelocity, maxCurvature);
        paint.Color = color.WithAlpha((byte)(255 * opacity));
        paint.StrokeWidth = strokeWidth;

        // Catmull-Rom spline interpolation
        using var path = new SKPath();
        path.MoveTo(p1);

        const int subdivisions = 8;
        for (int s = 1; s <= subdivisions; s++)
        {
            float u = s / (float)subdivisions;
            var pt = CatmullRom(p0, p1, p2, p3, u);
            path.LineTo(pt);
        }

        canvas.DrawPath(path, paint);
    }

    private void DrawSimpleSegment(SKCanvas canvas, SKPaint paint, List<TrajectoryTimestep> points, int i, double maxVelocity, double maxCurvature)
    {
        if (i + 1 >= points.Count) return;

        var t = (float)(i + 1) / points.Count;
        var opacity = GetTrailOpacity(t, points.Count);
        var velocity = points[i + 1].VelocityMagnitude;
        var strokeWidth = GetAdaptiveStrokeWidth(velocity, maxVelocity);

        var color = GetTrajectoryColor(points[i + 1], i + 1, points.Count, maxVelocity, maxCurvature);
        paint.Color = color.WithAlpha((byte)(255 * opacity));
        paint.StrokeWidth = strokeWidth;

        var p1 = ToScreen(points[i].State2D);
        var p2 = ToScreen(points[i + 1].State2D);
        canvas.DrawLine(p1, p2, paint);
    }

    /// <summary>
    /// Catmull-Rom spline interpolation between p1 and p2.
    /// </summary>
    private static SKPoint CatmullRom(SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        // Catmull-Rom basis functions
        float b0 = -0.5f * t3 + t2 - 0.5f * t;
        float b1 = 1.5f * t3 - 2.5f * t2 + 1.0f;
        float b2 = -1.5f * t3 + 2.0f * t2 + 0.5f * t;
        float b3 = 0.5f * t3 - 0.5f * t2;

        return new SKPoint(
            b0 * p0.X + b1 * p1.X + b2 * p2.X + b3 * p3.X,
            b0 * p0.Y + b1 * p1.Y + b2 * p2.Y + b3 * p3.Y
        );
    }

    /// <summary>
    /// Calculate trail opacity - older points fade out.
    /// </summary>
    private static float GetTrailOpacity(float t, int totalPoints)
    {
        // More aggressive fade for longer trajectories
        var fadeStart = Math.Max(0.3f, 1.0f - totalPoints / 2000f);
        
        if (t < fadeStart)
        {
            // Fade increases from 0.4 at start to 1.0 at fadeStart
            return 0.4f + 0.6f * (t / fadeStart);
        }
        return 1.0f;
    }

    /// <summary>
    /// Calculate stroke width based on velocity - faster = thinner (like calligraphy).
    /// </summary>
    private static float GetAdaptiveStrokeWidth(double velocity, double maxVelocity)
    {
        const float minWidth = 1.5f;
        const float maxWidth = 5.0f;

        // Inverse relationship: high velocity = thin stroke
        var normalizedVelocity = Math.Clamp(velocity / maxVelocity, 0, 1);
        var width = maxWidth - (maxWidth - minWidth) * (float)normalizedVelocity;
        
        return width;
    }

    private void DrawHeatMap(SKCanvas canvas, SKImageInfo info)
    {
        var points = Session!.GetTrajectoryUpToTime(Session.Player.Time).ToList();
        if (points.Count < 2) return;

        // Create density grid
        const int gridSize = 64;
        var density = new int[gridSize, gridSize];
        var maxDensity = 0;

        // Map trajectory points to grid cells and count visits
        foreach (var pt in points)
        {
            if (pt.State2D.Count < 2) continue;
            var screenPt = ToScreen(pt.State2D);
            
            // Convert screen coordinates to grid indices
            var gridX = (int)Math.Clamp((screenPt.X / info.Width) * gridSize, 0, gridSize - 1);
            var gridY = (int)Math.Clamp((screenPt.Y / info.Height) * gridSize, 0, gridSize - 1);
            
            density[gridX, gridY]++;
            maxDensity = Math.Max(maxDensity, density[gridX, gridY]);
        }

        if (maxDensity == 0) return;

        // Draw heat map cells
        var cellWidth = (float)info.Width / gridSize;
        var cellHeight = (float)info.Height / gridSize;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = false
        };

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (density[x, y] == 0) continue;

                // Normalize density to 0-1 range (logarithmic for better visualization)
                var normalizedDensity = Math.Log(1 + density[x, y]) / Math.Log(1 + maxDensity);
                
                // Get color from gradient
                var color = GetHeatMapColor((float)normalizedDensity);
                paint.Color = color.WithAlpha(160); // Semi-transparent

                var rect = new SKRect(
                    x * cellWidth,
                    y * cellHeight,
                    (x + 1) * cellWidth,
                    (y + 1) * cellHeight
                );
                canvas.DrawRect(rect, paint);
            }
        }

        // Apply Gaussian blur for smoother appearance
        using var blurPaint = new SKPaint
        {
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, cellWidth / 2)
        };
    }

    private static SKColor GetHeatMapColor(float t)
    {
        // Map t (0-1) to gradient index
        var gradientCount = HeatMapGradient.Length - 1;
        var scaledT = t * gradientCount;
        var index = (int)Math.Floor(scaledT);
        var fraction = scaledT - index;

        if (index >= gradientCount)
            return HeatMapGradient[gradientCount];

        // Interpolate between adjacent colors
        var c1 = HeatMapGradient[index];
        var c2 = HeatMapGradient[index + 1];

        return new SKColor(
            (byte)(c1.Red + (c2.Red - c1.Red) * fraction),
            (byte)(c1.Green + (c2.Green - c1.Green) * fraction),
            (byte)(c1.Blue + (c2.Blue - c1.Blue) * fraction),
            255
        );
    }

    private void DrawVectorFieldGrid(SKCanvas canvas, SKImageInfo info)
    {
        var points = Session!.GetTrajectoryUpToTime(Session.Player.Time).ToList();
        if (points.Count < 2) return;

        // Build velocity lookup for nearby trajectory points
        var velocityLookup = new Dictionary<(int, int), (double vx, double vy)>();
        const int gridSize = 12;

        foreach (var pt in points)
        {
            if (pt.State2D.Count < 2) continue;
            
            var screenPt = ToScreen(pt.State2D);
            var gridX = (int)Math.Clamp((screenPt.X / info.Width) * gridSize, 0, gridSize - 1);
            var gridY = (int)Math.Clamp((screenPt.Y / info.Height) * gridSize, 0, gridSize - 1);
            
            // Use velocity from trajectory data if available
            var vx = pt.State2D.Count > 0 ? pt.State2D[0] : 0;
            var vy = pt.State2D.Count > 1 ? pt.State2D[1] : 0;
            
            // Only keep first pass velocity (could average instead)
            velocityLookup.TryAdd((gridX, gridY), (vx, vy));
        }

        using var arrowPaint = new SKPaint
        {
            Color = VectorFieldColor.WithAlpha(100),
            StrokeWidth = 1.5f,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        var cellWidth = (float)info.Width / gridSize;
        var cellHeight = (float)info.Height / gridSize;
        var arrowLength = Math.Min(cellWidth, cellHeight) * 0.4f;

        // Draw arrows at grid intersections
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                var centerX = (x + 0.5f) * cellWidth;
                var centerY = (y + 0.5f) * cellHeight;

                // Try to get velocity from nearby trajectory points
                if (velocityLookup.TryGetValue((x, y), out var vel))
                {
                    var mag = Math.Sqrt(vel.vx * vel.vx + vel.vy * vel.vy);
                    if (mag > 0.001)
                    {
                        // Normalize and scale
                        var dx = (float)(vel.vx / mag * arrowLength);
                        var dy = (float)(-vel.vy / mag * arrowLength); // Y inverted

                        DrawSmallArrow(canvas, arrowPaint,
                            new SKPoint(centerX, centerY),
                            new SKPoint(centerX + dx, centerY + dy));
                    }
                }
                else
                {
                    // No trajectory data - draw a small dot placeholder
                    canvas.DrawCircle(centerX, centerY, 2f, arrowPaint);
                }
            }
        }
    }

    private static void DrawSmallArrow(SKCanvas canvas, SKPaint paint, SKPoint from, SKPoint to)
    {
        canvas.DrawLine(from, to, paint);
        
        // Arrow head
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        
        if (length < 5) return;

        var headLength = Math.Min(length * 0.3f, 8f);
        var headAngle = 0.5f; // radians

        var angle = (float)Math.Atan2(dy, dx);
        
        var head1 = new SKPoint(
            to.X - headLength * (float)Math.Cos(angle - headAngle),
            to.Y - headLength * (float)Math.Sin(angle - headAngle)
        );
        var head2 = new SKPoint(
            to.X - headLength * (float)Math.Cos(angle + headAngle),
            to.Y - headLength * (float)Math.Sin(angle + headAngle)
        );

        canvas.DrawLine(to, head1, paint);
        canvas.DrawLine(to, head2, paint);
    }

    private void DrawVelocityVectors(SKCanvas canvas)
    {
        var points = Session!.GetTrajectoryUpToTime(Session.Player.Time).ToList();

        using var paint = new SKPaint
        {
            Color = VelocityColor,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Draw velocity vectors every N points
        int step = Math.Max(1, points.Count / 20);
        for (int i = 0; i < points.Count; i += step)
        {
            var pt = points[i];
            if (pt.Velocity.Count < 2) continue;

            var pos = ToScreen(pt.State2D);
            var vel = new SKPoint(
                (float)pt.Velocity[0] * _scale * 0.3f,
                -(float)pt.Velocity[1] * _scale * 0.3f
            );

            DrawArrow(canvas, pos, new SKPoint(pos.X + vel.X, pos.Y + vel.Y), paint);
        }
    }

    private void DrawCurvatureMarkers(SKCanvas canvas)
    {
        var points = Session!.GetTrajectoryUpToTime(Session.Player.Time).ToList();

        // Find high-curvature points (phase transitions)
        var maxCurvature = points.Max(p => p.Curvature);
        if (maxCurvature < 0.01) return;

        var curvatureThreshold = maxCurvature * 0.5;
        var highCurvaturePoints = points.Where(p => p.Curvature > curvatureThreshold).ToList();

        // Draw halos first (behind markers)
        DrawCurvatureHalos(canvas, highCurvaturePoints, maxCurvature);

        // Draw core markers on top
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        foreach (var pt in highCurvaturePoints)
        {
            var pos = ToScreen(pt.State2D);
            var intensity = (float)(pt.Curvature / maxCurvature);
            paint.Color = CurvatureHighColor.WithAlpha((byte)(intensity * 200));

            var radius = 5 + intensity * 15;
            canvas.DrawCircle(pos, radius, paint);
        }
    }

    private void DrawCurvatureHalos(SKCanvas canvas, List<TrajectoryTimestep> highCurvaturePoints, double maxCurvature)
    {
        using var haloPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        // Draw multiple concentric rings for each high-curvature point
        foreach (var pt in highCurvaturePoints)
        {
            var pos = ToScreen(pt.State2D);
            var intensity = (float)(pt.Curvature / maxCurvature);

            // Draw 3 expanding halo rings
            for (int ring = 1; ring <= 3; ring++)
            {
                var baseRadius = 10 + intensity * 20;
                var ringRadius = baseRadius + ring * 8;
                var ringAlpha = (byte)(80 * intensity / ring);
                var ringWidth = 3f / ring;

                haloPaint.Color = CurvatureHighColor.WithAlpha(ringAlpha);
                haloPaint.StrokeWidth = ringWidth;
                haloPaint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, ring * 2);

                canvas.DrawCircle(pos, ringRadius, haloPaint);
            }

            // Draw inner glow
            using var glowPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
            };
            glowPaint.Color = CurvatureHighColor.WithAlpha((byte)(40 * intensity));
            canvas.DrawCircle(pos, 15 * intensity + 5, glowPaint);
        }
    }

    private void DrawProfessorVectors(SKCanvas canvas)
    {
        var professors = Session?.Run?.Evaluators.Professors;
        if (professors is null || professors.Count == 0) return;

        using var paint = new SKPaint
        {
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        using var textFont = new SKFont(SKTypeface.Default, 12);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        foreach (var prof in professors)
        {
            if (prof.Vector.Count < 2) continue;

            paint.Color = prof.Holdout ? HoldoutColor : ProfessorColor;

            var end = ToScreen(prof.Vector);
            DrawArrow(canvas, _center, end, paint);

            // Label
            textPaint.Color = paint.Color;
            canvas.DrawText(prof.Name, end.X + 5, end.Y - 5, SKTextAlign.Left, textFont, textPaint);
        }
    }

    private void DrawCurrentPosition(SKCanvas canvas)
    {
        var current = Session?.CurrentTrajectoryState;
        if (current?.State2D is not { Count: >= 2 } state2D) return;

        var pos = ToScreen(state2D);

        using var glowPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10)
        };
        canvas.DrawCircle(pos, 15, glowPaint);

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(pos, 6, paint);
    }

    private void DrawLegend(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 11);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            IsAntialias = true
        };

        var x = 10f;
        var y = info.Height - 75f;
        var spacing = 15f;

        // Performance mode indicator
        if (Session?.Player.IsLargeRun == true)
        {
            paint.Color = SKColor.Parse("#ffd93d").WithAlpha(200);
            canvas.DrawText($"⚡ {Session.Player.FrameSkipDisplay}", x, y, SKTextAlign.Left, font, paint);
            y += spacing;
            paint.Color = SKColors.White.WithAlpha(180);
        }

        // Time indicator
        var time = Session?.Player.Time ?? 0;
        canvas.DrawText($"t = {time:P0}", x, y, SKTextAlign.Left, font, paint);
        y += spacing;

        // Effective dimension
        var effDim = Session?.CurrentTrajectoryState?.EffectiveDim ?? 0;
        canvas.DrawText($"Eff. Dim = {effDim:F2}", x, y, SKTextAlign.Left, font, paint);
        y += spacing;

        // Curvature
        var curvature = Session?.CurrentTrajectoryState?.Curvature ?? 0;
        canvas.DrawText($"Curvature = {curvature:F3}", x, y, SKTextAlign.Left, font, paint);
    }

    private void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint)
    {
        canvas.DrawLine(from, to, paint);

        // Arrowhead
        var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var headLen = 8f;
        var headAngle = 0.5f;

        var p1 = new SKPoint(
            to.X - headLen * MathF.Cos(angle - headAngle),
            to.Y - headLen * MathF.Sin(angle - headAngle)
        );
        var p2 = new SKPoint(
            to.X - headLen * MathF.Cos(angle + headAngle),
            to.Y - headLen * MathF.Sin(angle + headAngle)
        );

        canvas.DrawLine(to, p1, paint);
        canvas.DrawLine(to, p2, paint);
    }

    private SKPoint ToScreen(IList<double> state)
    {
        if (state.Count < 2) return _center;
        return new SKPoint(
            _center.X + (float)state[0] * _scale,
            _center.Y - (float)state[1] * _scale // Y inverted
        );
    }

    private static SKColor InterpolateColor(SKColor from, SKColor to, float t)
    {
        return new SKColor(
            (byte)(from.Red + (to.Red - from.Red) * t),
            (byte)(from.Green + (to.Green - from.Green) * t),
            (byte)(from.Blue + (to.Blue - from.Blue) * t),
            255
        );
    }
}
