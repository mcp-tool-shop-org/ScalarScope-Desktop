using AspireDesktop.Models;
using AspireDesktop.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace AspireDesktop.Views.Controls;

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

    // Colors
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");
    private static readonly SKColor TrajectoryStartColor = SKColor.Parse("#00d9ff");
    private static readonly SKColor TrajectoryEndColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor VelocityColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor CurvatureHighColor = SKColor.Parse("#ff9f43");
    private static readonly SKColor ProfessorColor = SKColor.Parse("#a29bfe");
    private static readonly SKColor HoldoutColor = SKColor.Parse("#fd79a8");

    private float _padding = 40f;
    private float _scale = 100f;
    private SKPoint _center;

    public TrajectoryCanvas()
    {
        PaintSurface += OnPaintSurface;
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
        MainThread.BeginInvokeOnMainThread(InvalidateSurface);
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        _center = new SKPoint(info.Width / 2f, info.Height / 2f);
        _scale = Math.Min(info.Width, info.Height) / 4f;

        DrawGrid(canvas, info);

        if (Session?.Run is null)
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
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 18,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("Load a geometry run to visualize", _center.X, _center.Y, paint);
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

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 12,
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
            canvas.DrawText(prof.Name, end.X + 5, end.Y - 5, textPaint);
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
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            TextSize = 11,
            IsAntialias = true
        };

        var x = 10f;
        var y = info.Height - 60f;
        var spacing = 15f;

        // Time indicator
        var time = Session?.Player.Time ?? 0;
        canvas.DrawText($"t = {time:P0}", x, y, paint);
        y += spacing;

        // Effective dimension
        var effDim = Session?.CurrentTrajectoryState?.EffectiveDim ?? 0;
        canvas.DrawText($"Eff. Dim = {effDim:F2}", x, y, paint);
        y += spacing;

        // Curvature
        var curvature = Session?.CurrentTrajectoryState?.Curvature ?? 0;
        canvas.DrawText($"Curvature = {curvature:F3}", x, y, paint);
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
