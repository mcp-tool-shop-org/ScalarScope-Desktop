using ScalarScope.Models;
using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Trajectory canvas for comparison view.
/// Renders a single run with label overlay.
/// </summary>
public class ComparisonTrajectoryCanvas : SKCanvasView
{
    public static readonly BindableProperty RunProperty =
        BindableProperty.Create(nameof(Run), typeof(GeometryRun), typeof(ComparisonTrajectoryCanvas),
            propertyChanged: OnRunChanged);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(ComparisonTrajectoryCanvas), 0.0,
            propertyChanged: OnTimeChanged);

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(nameof(Label), typeof(string), typeof(ComparisonTrajectoryCanvas), "");

    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(ComparisonTrajectoryCanvas), Colors.Cyan);

    public static readonly BindableProperty ShowAnnotationsProperty =
        BindableProperty.Create(nameof(ShowAnnotations), typeof(bool), typeof(ComparisonTrajectoryCanvas), false);

    public static readonly BindableProperty IsDominantProperty =
        BindableProperty.Create(nameof(IsDominant), typeof(bool?), typeof(ComparisonTrajectoryCanvas), null,
            propertyChanged: OnDominantChanged);

    public GeometryRun? Run
    {
        get => (GeometryRun?)GetValue(RunProperty);
        set => SetValue(RunProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public bool ShowAnnotations
    {
        get => (bool)GetValue(ShowAnnotationsProperty);
        set => SetValue(ShowAnnotationsProperty, value);
    }

    /// <summary>
    /// null = equal/unknown, true = this run is dominant, false = this run is weaker
    /// </summary>
    public bool? IsDominant
    {
        get => (bool?)GetValue(IsDominantProperty);
        set => SetValue(IsDominantProperty, value);
    }

    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");

    private float _scale = 100f;
    private SKPoint _center;

    public ComparisonTrajectoryCanvas()
    {
        PaintSurface += OnPaintSurface;
    }

    private static void OnRunChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ComparisonTrajectoryCanvas canvas)
            canvas.InvalidateSurface();
    }

    private static void OnTimeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ComparisonTrajectoryCanvas canvas)
            canvas.InvalidateSurface();
    }

    private static void OnDominantChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ComparisonTrajectoryCanvas canvas)
            canvas.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        _center = new SKPoint(info.Width / 2f, info.Height / 2f);
        _scale = Math.Min(info.Width, info.Height) / 4f;

        // Apply dimming overlay if this run is weaker
        if (IsDominant == false)
        {
            using var dimPaint = new SKPaint
            {
                Color = SKColors.Black.WithAlpha(60),
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRect(0, 0, info.Width, info.Height, dimPaint);
        }

        DrawGrid(canvas, info);
        DrawLabel(canvas, info);

        if (Run == null)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Invariant check: trajectory must be non-empty before rendering
        if (!InvariantGuard.AssertTrajectoryNonEmpty(Run, $"ComparisonTrajectoryCanvas.{Label}"))
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Invariant check: time must be valid
        var clampedTime = InvariantGuard.ClampTime(CurrentTime, $"ComparisonTrajectoryCanvas.{Label}");
        if (Math.Abs(clampedTime - CurrentTime) > 0.001)
        {
            // Time was out of bounds - use clamped value
            CurrentTime = clampedTime;
        }

        DrawProfessorVectors(canvas);
        DrawTrajectory(canvas);
        DrawCurrentPosition(canvas);

        if (ShowAnnotations)
        {
            DrawAnnotations(canvas, info);
        }

        DrawMetrics(canvas, info);
        DrawDominanceIndicator(canvas, info);
    }

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

    private void DrawLabel(SKCanvas canvas, SKImageInfo info)
    {
        if (string.IsNullOrEmpty(Label)) return;

        var skAccent = new SKColor(
            (byte)(AccentColor.Red * 255),
            (byte)(AccentColor.Green * 255),
            (byte)(AccentColor.Blue * 255)
        );

        using var boldFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 16);
        using var subtitleFont = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint
        {
            Color = skAccent,
            IsAntialias = true
        };

        canvas.DrawText(Label, 15, 25, SKTextAlign.Left, boldFont, paint);

        // Condition subtitle
        if (Run != null)
        {
            paint.Color = SKColors.Gray;
            canvas.DrawText(Run.Metadata.Condition, 15, 42, SKTextAlign.Left, subtitleFont, paint);
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
        canvas.DrawText("Load a training run", _center.X, _center.Y, SKTextAlign.Center, font, paint);
    }

    private void DrawTrajectory(SKCanvas canvas)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count < 2) return;

        var maxIdx = (int)(CurrentTime * (steps.Count - 1));

        var skAccent = new SKColor(
            (byte)(AccentColor.Red * 255),
            (byte)(AccentColor.Green * 255),
            (byte)(AccentColor.Blue * 255)
        );

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        for (int i = 1; i <= maxIdx && i < steps.Count; i++)
        {
            var t = (float)i / steps.Count;
            // Fade from dim to bright
            paint.Color = skAccent.WithAlpha((byte)(100 + t * 155));

            var p1 = ToScreen(steps[i - 1].State2D);
            var p2 = ToScreen(steps[i].State2D);
            canvas.DrawLine(p1, p2, paint);
        }
    }

    private void DrawProfessorVectors(SKCanvas canvas)
    {
        var professors = Run?.Evaluators.Professors;
        if (professors == null || professors.Count == 0) return;

        using var paint = new SKPaint
        {
            StrokeWidth = 2,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            Color = SKColor.Parse("#a29bfe").WithAlpha(150)
        };

        using var holdoutPaint = new SKPaint
        {
            StrokeWidth = 2,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round,
            Color = SKColor.Parse("#fd79a8").WithAlpha(150)
        };

        foreach (var prof in professors)
        {
            if (prof.Vector.Count < 2) continue;

            var end = ToScreen(prof.Vector);
            DrawArrow(canvas, _center, end, prof.Holdout ? holdoutPaint : paint);
        }
    }

    private void DrawCurrentPosition(SKCanvas canvas)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count == 0) return;

        var idx = (int)(CurrentTime * (steps.Count - 1));
        idx = Math.Clamp(idx, 0, steps.Count - 1);
        var current = steps[idx];

        if (current.State2D.Count < 2) return;

        var pos = ToScreen(current.State2D);

        var skAccent = new SKColor(
            (byte)(AccentColor.Red * 255),
            (byte)(AccentColor.Green * 255),
            (byte)(AccentColor.Blue * 255)
        );

        using var glowPaint = new SKPaint
        {
            Color = skAccent.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
        };
        canvas.DrawCircle(pos, 12, glowPaint);

        using var paint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(pos, 5, paint);
    }

    private void DrawAnnotations(SKCanvas canvas, SKImageInfo info)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count == 0) return;

        var idx = (int)(CurrentTime * (steps.Count - 1));
        idx = Math.Clamp(idx, 0, steps.Count - 1);
        var current = steps[idx];

        using var annotFont = new SKFont(SKTypeface.Default, 10);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(200),
            IsAntialias = true
        };

        // Curvature annotation - use centralized threshold
        if (current.Curvature > ConsistencyCheckService.HighCurvatureThreshold)
        {
            var pos = ToScreen(current.State2D);
            paint.Color = SKColor.Parse("#ff9f43");
            canvas.DrawText($"↻ Curvature: {current.Curvature:F2}", pos.X + 15, pos.Y - 10, SKTextAlign.Left, annotFont, paint);
            canvas.DrawText("Phase transition detected", pos.X + 15, pos.Y + 5, SKTextAlign.Left, annotFont, paint);
        }

        // Effective dimensionality annotation
        var eigenvalues = Run.Geometry.Eigenvalues;
        if (eigenvalues.Count > 0)
        {
            var eigenIdx = (int)(CurrentTime * (eigenvalues.Count - 1));
            eigenIdx = Math.Clamp(eigenIdx, 0, eigenvalues.Count - 1);
            var eigen = eigenvalues[eigenIdx];

            // Use centralized calculation for consistency
            var firstFactor = ConsistencyCheckService.ComputeFirstFactorVariance(eigen.Values, "ComparisonTrajectoryCanvas.Annotations");
            var interpretation = ConsistencyCheckService.GetEigenInterpretation(firstFactor);
            var rgb = ConsistencyCheckService.GetInterpretationColor(interpretation);
            paint.Color = new SKColor(rgb.R, rgb.G, rgb.B);

            var y = info.Height - 60;
            string annotationText = interpretation switch
            {
                EigenInterpretation.StrongSharedAxis => "λ₁ dominates: Shared evaluative axis",
                EigenInterpretation.ModerateUnification => "λ₁ moderate: Partial unification",
                _ => "λ₁ weak: Orthogonal evaluators"
            };
            canvas.DrawText(annotationText, 15, y, SKTextAlign.Left, annotFont, paint);
        }
    }

    private void DrawMetrics(SKCanvas canvas, SKImageInfo info)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count == 0) return;

        var idx = (int)(CurrentTime * (steps.Count - 1));
        idx = Math.Clamp(idx, 0, steps.Count - 1);
        var current = steps[idx];

        using var font = new SKFont(SKTypeface.Default, 11);
        using var paint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            IsAntialias = true
        };

        var x = 15f;
        var y = info.Height - 40f;

        canvas.DrawText($"Eff.Dim: {current.EffectiveDim:F2}", x, y, SKTextAlign.Left, font, paint);
        y += 15;
        canvas.DrawText($"Curvature: {current.Curvature:F3}", x, y, SKTextAlign.Left, font, paint);
    }

    private void DrawArrow(SKCanvas canvas, SKPoint from, SKPoint to, SKPaint paint)
    {
        canvas.DrawLine(from, to, paint);

        var angle = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var headLen = 6f;
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
            _center.Y - (float)state[1] * _scale
        );
    }

    private void DrawDominanceIndicator(SKCanvas canvas, SKImageInfo info)
    {
        if (IsDominant == null) return;

        using var boldFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 10);
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 9);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var x = info.Width - 90;
        var y = 25f;

        if (IsDominant == true)
        {
            paint.Color = SKColor.Parse("#4ecdc4");
            canvas.DrawText("★ STRONGER", x, y, SKTextAlign.Left, boldFont, paint);
        }
        else
        {
            paint.Color = SKColor.Parse("#888888");
            canvas.DrawText("○ WEAKER", x, y, SKTextAlign.Left, boldFont, paint);
        }

        // Draw evaluator alignment indicator
        var eigenvalues = Run?.Geometry.Eigenvalues;
        if (eigenvalues != null && eigenvalues.Count > 0)
        {
            var eigenIdx = (int)(CurrentTime * (eigenvalues.Count - 1));
            eigenIdx = Math.Clamp(eigenIdx, 0, eigenvalues.Count - 1);
            var eigen = eigenvalues[eigenIdx];

            if (eigen.Values.Count > 0)
            {
                // Use centralized calculation and thresholds
                var firstFactor = ConsistencyCheckService.ComputeFirstFactorVariance(eigen.Values, "ComparisonTrajectoryCanvas.Dominance");

                y += 15;

                // Use centralized thresholds for dominance indicator
                if (firstFactor > ConsistencyCheckService.DominantFirstFactorThreshold)
                {
                    paint.Color = SKColor.Parse("#4ecdc4");
                    canvas.DrawText("Aligned evaluators", x, y, SKTextAlign.Left, smallFont, paint);
                }
                else if (firstFactor > ConsistencyCheckService.ModerateFirstFactorThreshold)
                {
                    paint.Color = SKColor.Parse("#ffd93d");
                    canvas.DrawText("Partial alignment", x, y, SKTextAlign.Left, smallFont, paint);
                }
                else
                {
                    paint.Color = SKColor.Parse("#ff6b6b");
                    canvas.DrawText("Orthogonal evaluators", x, y, SKTextAlign.Left, smallFont, paint);
                }
            }
        }
    }
}
