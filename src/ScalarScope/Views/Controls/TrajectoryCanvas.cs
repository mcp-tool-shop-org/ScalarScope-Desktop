using ScalarScope.Models;
using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

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
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");
    private static readonly SKColor TrajectoryStartColor = SKColor.Parse("#00d9ff");
    private static readonly SKColor TrajectoryEndColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor VelocityColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor CurvatureHighColor = SKColor.Parse("#ff9f43");
    private static readonly SKColor ProfessorColor = SKColor.Parse("#a29bfe");
    private static readonly SKColor HoldoutColor = SKColor.Parse("#fd79a8");

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

        using var path = new SKPath();
        var first = ToScreen(points[0].State2D);
        path.MoveTo(first);

        for (int i = 1; i < points.Count; i++)
        {
            var pt = ToScreen(points[i].State2D);
            path.LineTo(pt);
        }

        // Gradient stroke based on time
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        // Draw with color gradient (time-based)
        for (int i = 1; i < points.Count; i++)
        {
            var t = (float)i / points.Count;
            var color = InterpolateColor(TrajectoryStartColor, TrajectoryEndColor, t);
            paint.Color = color;

            var p1 = ToScreen(points[i - 1].State2D);
            var p2 = ToScreen(points[i].State2D);
            canvas.DrawLine(p1, p2, paint);
        }
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

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };

        foreach (var pt in points.Where(p => p.Curvature > maxCurvature * 0.5))
        {
            var pos = ToScreen(pt.State2D);
            var intensity = (float)(pt.Curvature / maxCurvature);
            paint.Color = CurvatureHighColor.WithAlpha((byte)(intensity * 200));

            var radius = 5 + intensity * 15;
            canvas.DrawCircle(pos, radius, paint);
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
        if (current?.State2D.Count < 2) return;

        var pos = ToScreen(current.State2D);

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
