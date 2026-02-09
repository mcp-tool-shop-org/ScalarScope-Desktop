using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Scalar Ring Stack visualization.
/// Concentric rings where radius = scalar value, angular position = time.
/// Creates the literal "vortex" visual where phase-locked rings = evaluator coherence.
/// </summary>
public class ScalarRingStack : SKCanvasView
{
    public static readonly BindableProperty SessionProperty =
        BindableProperty.Create(nameof(Session), typeof(VortexSessionViewModel), typeof(ScalarRingStack),
            propertyChanged: OnSessionChanged);

    public VortexSessionViewModel? Session
    {
        get => (VortexSessionViewModel?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    // Dimension colors (distinct but harmonious)
    private static readonly SKColor[] DimensionColors =
    [
        SKColor.Parse("#00d9ff"), // Correctness - cyan
        SKColor.Parse("#00ff88"), // Coherence - green
        SKColor.Parse("#ff6b6b"), // Calibration - red
        SKColor.Parse("#ffd93d"), // Tradeoffs - yellow
        SKColor.Parse("#c56cf0"), // Clarity - purple
    ];

    private static readonly string[] DimensionNames =
        ["Correctness", "Coherence", "Calibration", "Tradeoffs", "Clarity"];

    private static new readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor GridColor = SKColor.Parse("#2a2a4e");

    private float _maxRadius;
    private SKPoint _center;

    // Phase 1: Demo state fields
    private Models.GeometryRun? _currentRenderRun;
    private bool _isRenderingDemo;

    public ScalarRingStack()
    {
        PaintSurface += OnPaintSurface;
        
        // Phase 1: Subscribe to demo animation for continuous repainting
        DemoStateService.Instance.OnAnimationFrame += OnDemoAnimationFrame;
    }

    private void OnDemoAnimationFrame()
    {
        // Only repaint if we're showing demo data
        if (Session?.Run is null)
        {
            MainThread.BeginInvokeOnMainThread(InvalidateSurface);
        }
    }

    private static void OnSessionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ScalarRingStack canvas)
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
        _maxRadius = Math.Min(info.Width, info.Height) / 2f - 60f;

        DrawConcentricGuides(canvas);

        // Phase 1: Use demo data when no session data available
        _currentRenderRun = Session?.Run;
        _isRenderingDemo = false;
        
        if (_currentRenderRun is null)
        {
            _currentRenderRun = DemoStateService.Instance.DemoRun;
            _isRenderingDemo = true;
        }

        if (_currentRenderRun is null)
        {
            DrawNoDataMessage(canvas);
            return;
        }

        DrawScalarRings(canvas);
        DrawCurrentMarkers(canvas);
        DrawLegend(canvas, info);
        
        // Phase 1: Draw demo badge if showing demo data
        if (_isRenderingDemo)
        {
            DrawDemoBadge(canvas, info);
        }
    }

    private void DrawConcentricGuides(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        // Draw concentric circles at 0.25, 0.5, 0.75, 1.0
        for (int i = 1; i <= 4; i++)
        {
            var radius = _maxRadius * i / 4f;
            canvas.DrawCircle(_center, radius, paint);
        }

        // Draw radial lines every 45 degrees
        for (int i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4;
            var end = new SKPoint(
                _center.X + MathF.Cos(angle) * _maxRadius,
                _center.Y + MathF.Sin(angle) * _maxRadius
            );
            canvas.DrawLine(_center, end, paint);
        }
    }

    private void DrawNoDataMessage(SKCanvas canvas)
    {
        using var font = new SKFont(SKTypeface.Default, 18);
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true
        };
        canvas.DrawText("Load a geometry run to visualize", _center.X, _center.Y, SKTextAlign.Center, font, paint);
    }

    private void DrawScalarRings(SKCanvas canvas)
    {
        var values = _currentRenderRun!.Scalars.Values;
        if (values.Count < 2) return;

        // Phase 1: Use animation time for demo, Session.Player.Time for real data
        var renderTime = _isRenderingDemo ? DemoStateService.Instance.AnimationTime : Session!.Player.Time;
        var currentTimeIdx = (int)(renderTime * (values.Count - 1));

        // Draw trailing rings (history) with fading alpha
        int trailLength = Math.Min(50, currentTimeIdx);

        for (int dim = 0; dim < 5; dim++)
        {
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            using var path = new SKPath();
            bool started = false;

            for (int i = Math.Max(0, currentTimeIdx - trailLength); i <= currentTimeIdx; i++)
            {
                var scalars = values[i].ToArray();
                if (dim >= scalars.Length) continue;

                var value = scalars[dim];
                var radius = (float)value * _maxRadius;

                // Angular position based on time (rotates as time progresses)
                var angle = (float)i / values.Count * MathF.PI * 2 - MathF.PI / 2;

                var x = _center.X + MathF.Cos(angle) * radius;
                var y = _center.Y + MathF.Sin(angle) * radius;

                if (!started)
                {
                    path.MoveTo(x, y);
                    started = true;
                }
                else
                {
                    path.LineTo(x, y);
                }
            }

            // Color with alpha gradient
            var baseColor = DimensionColors[dim];
            paint.Color = baseColor;
            canvas.DrawPath(path, paint);
        }
    }

    private void DrawCurrentMarkers(SKCanvas canvas)
    {
        // Phase 1: Get current scalars from session or demo as array
        double[]? scalarsArray;
        if (_isRenderingDemo)
        {
            scalarsArray = DemoStateService.Instance.GetAnimatedScalars().ToArray();
        }
        else
        {
            scalarsArray = Session?.CurrentScalars?.ToArray();
        }
        if (scalarsArray is null) return;

        var values = _currentRenderRun!.Scalars.Values;
        // Phase 1: Use animation time for demo, Session.Player.Time for real data
        var renderTime = _isRenderingDemo ? DemoStateService.Instance.AnimationTime : Session!.Player.Time;
        var currentTimeIdx = (int)(renderTime * (values.Count - 1));
        var currentAngle = (float)currentTimeIdx / values.Count * MathF.PI * 2 - MathF.PI / 2;

        var scalars = scalarsArray;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var glowPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5)
        };

        for (int dim = 0; dim < Math.Min(5, scalars.Length); dim++)
        {
            var radius = (float)scalars[dim] * _maxRadius;
            var x = _center.X + MathF.Cos(currentAngle) * radius;
            var y = _center.Y + MathF.Sin(currentAngle) * radius;

            // Glow
            glowPaint.Color = DimensionColors[dim].WithAlpha(100);
            canvas.DrawCircle(x, y, 12, glowPaint);

            // Marker
            paint.Color = DimensionColors[dim];
            canvas.DrawCircle(x, y, 6, paint);
        }

        // Draw radial line showing current time position
        using var linePaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(100),
            StrokeWidth = 1,
            IsAntialias = true
        };

        var lineEnd = new SKPoint(
            _center.X + MathF.Cos(currentAngle) * _maxRadius,
            _center.Y + MathF.Sin(currentAngle) * _maxRadius
        );
        canvas.DrawLine(_center, lineEnd, linePaint);
    }

    private void DrawLegend(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 11);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var x = 10f;
        var y = 20f;
        var spacing = 18f;

        for (int i = 0; i < 5; i++)
        {
            paint.Color = DimensionColors[i];

            // Color dot
            canvas.DrawCircle(x + 5, y - 4, 5, paint);

            // Label - Phase 1: Use demo data when no session
            paint.Color = SKColors.White.WithAlpha(200);
            double value = 0;
            if (_isRenderingDemo)
            {
                var demoScalars = DemoStateService.Instance.GetAnimatedScalars();
                if (i < demoScalars.Count) value = demoScalars[i];
            }
            else
            {
                var sessionScalars = Session?.CurrentScalars?.ToArray();
                if (sessionScalars != null && i < sessionScalars.Length) value = sessionScalars[i];
            }
            canvas.DrawText($"{DimensionNames[i]}: {value:F2}", x + 15, y, SKTextAlign.Left, font, paint);

            y += spacing;
        }

        // Phase lock indicator
        y += 10;
        // Phase 1: Use demo data when no session
        double[]? scalars;
        if (_isRenderingDemo)
        {
            scalars = DemoStateService.Instance.GetAnimatedScalars().ToArray();
        }
        else
        {
            scalars = Session?.CurrentScalars?.ToArray();
        }
        if (scalars is { Length: >= 5 })
        {
            var variance = CalculateVariance(scalars);
            var phaseLock = variance < 0.02 ? "LOCKED" : variance < 0.05 ? "Partial" : "Drift";
            paint.Color = variance < 0.02 ? SKColors.LightGreen : SKColors.Orange;
            canvas.DrawText($"Phase: {phaseLock}", x, y, SKTextAlign.Left, font, paint);
        }
    }

    private static double CalculateVariance(double[] values)
    {
        if (values.Length == 0) return 0;
        var mean = values.Average();
        return values.Select(v => (v - mean) * (v - mean)).Average();
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
}
