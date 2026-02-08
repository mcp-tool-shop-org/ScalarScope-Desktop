using ScalarScope.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Timeline visualization of failure events during training.
/// Shows failures as colored markers along a time axis with severity and category.
/// </summary>
public class FailuresTimeline : SKCanvasView
{
    public static readonly BindableProperty FailuresProperty =
        BindableProperty.Create(nameof(Failures), typeof(IList<FailureEvent>), typeof(FailuresTimeline),
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(FailuresTimeline), 0.0,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty SelectedFailureProperty =
        BindableProperty.Create(nameof(SelectedFailure), typeof(FailureEvent), typeof(FailuresTimeline),
            defaultBindingMode: BindingMode.TwoWay);

    public IList<FailureEvent>? Failures
    {
        get => (IList<FailureEvent>?)GetValue(FailuresProperty);
        set => SetValue(FailuresProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    public FailureEvent? SelectedFailure
    {
        get => (FailureEvent?)GetValue(SelectedFailureProperty);
        set => SetValue(SelectedFailureProperty, value);
    }

    // Colors by severity
    private static readonly SKColor CriticalColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor WarningColor = SKColor.Parse("#ff9f43");
    private static readonly SKColor InfoColor = SKColor.Parse("#ffd93d");
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor TrackColor = SKColor.Parse("#2a2a4e");
    private static readonly SKColor TextColor = SKColors.White.WithAlpha(200);

    public FailuresTimeline()
    {
        PaintSurface += OnPaintSurface;
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    private static void OnPropertyChangedInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FailuresTimeline timeline)
            timeline.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        DrawTimeline(canvas, info);
        DrawFailureMarkers(canvas, info);
        DrawCurrentTimeIndicator(canvas, info);
        DrawLegend(canvas, info);

        if (SelectedFailure != null)
        {
            DrawSelectedFailureDetails(canvas, info);
        }
    }

    private void DrawTimeline(SKCanvas canvas, SKImageInfo info)
    {
        var trackY = info.Height / 2f;
        var trackLeft = 80f;
        var trackRight = info.Width - 20f;

        using var paint = new SKPaint
        {
            Color = TrackColor,
            StrokeWidth = 6,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        canvas.DrawLine(trackLeft, trackY, trackRight, trackY, paint);

        // Time markers
        using var timeFont = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(100),
            IsAntialias = true
        };

        for (int i = 0; i <= 10; i++)
        {
            var t = i / 10f;
            var x = trackLeft + t * (trackRight - trackLeft);

            paint.StrokeWidth = 1;
            paint.Color = TrackColor;
            canvas.DrawLine(x, trackY - 5, x, trackY + 5, paint);

            canvas.DrawText($"{t * 100:F0}%", x, trackY + 18, SKTextAlign.Center, timeFont, textPaint);
        }

        // Labels
        using var labelFont = new SKFont(SKTypeface.Default, 11);
        textPaint.Color = TextColor;
        canvas.DrawText("Timeline", 10, trackY + 4, SKTextAlign.Left, labelFont, textPaint);
    }

    private void DrawFailureMarkers(SKCanvas canvas, SKImageInfo info)
    {
        if (Failures == null || Failures.Count == 0) return;

        var trackY = info.Height / 2f;
        var trackLeft = 80f;
        var trackRight = info.Width - 20f;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            Color = SKColors.White.WithAlpha(100)
        };

        foreach (var failure in Failures)
        {
            var x = trackLeft + (float)failure.T * (trackRight - trackLeft);
            var color = GetSeverityColor(failure.Severity);
            var radius = GetSeverityRadius(failure.Severity);

            // Only show failures up to current time (or all if we're at the end)
            var alpha = failure.T <= CurrentTime ? (byte)255 : (byte)80;
            paint.Color = color.WithAlpha(alpha);

            // Draw marker
            canvas.DrawCircle(x, trackY, radius, paint);

            // Highlight selected
            if (SelectedFailure == failure)
            {
                strokePaint.Color = SKColors.White;
                strokePaint.StrokeWidth = 3;
                canvas.DrawCircle(x, trackY, radius + 3, strokePaint);
            }

            // Draw category icon for critical failures
            if (failure.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase))
            {
                using var iconFont = new SKFont(SKTypeface.Default, 10);
                using var iconPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true
                };
                canvas.DrawText("!", x, trackY + 4, SKTextAlign.Center, iconFont, iconPaint);
            }
        }
    }

    private void DrawCurrentTimeIndicator(SKCanvas canvas, SKImageInfo info)
    {
        var trackY = info.Height / 2f;
        var trackLeft = 80f;
        var trackRight = info.Width - 20f;

        var x = trackLeft + (float)CurrentTime * (trackRight - trackLeft);

        using var paint = new SKPaint
        {
            Color = SKColor.Parse("#00d9ff"),
            StrokeWidth = 2,
            IsAntialias = true
        };

        // Vertical line
        canvas.DrawLine(x, trackY - 25, x, trackY + 25, paint);

        // Triangle pointer
        using var path = new SKPath();
        path.MoveTo(x, trackY - 25);
        path.LineTo(x - 6, trackY - 35);
        path.LineTo(x + 6, trackY - 35);
        path.Close();

        paint.Style = SKPaintStyle.Fill;
        canvas.DrawPath(path, paint);
    }

    private void DrawLegend(SKCanvas canvas, SKImageInfo info)
    {
        var y = 20f;
        var x = info.Width - 180f;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var legendFont = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        // Critical
        paint.Color = CriticalColor;
        canvas.DrawCircle(x, y, 6, paint);
        canvas.DrawText("Critical", x + 12, y + 4, SKTextAlign.Left, legendFont, textPaint);

        // Warning
        x += 60;
        paint.Color = WarningColor;
        canvas.DrawCircle(x, y, 5, paint);
        canvas.DrawText("Warning", x + 12, y + 4, SKTextAlign.Left, legendFont, textPaint);

        // Info
        x += 60;
        paint.Color = InfoColor;
        canvas.DrawCircle(x, y, 4, paint);
        canvas.DrawText("Info", x + 12, y + 4, SKTextAlign.Left, legendFont, textPaint);
    }

    private void DrawSelectedFailureDetails(SKCanvas canvas, SKImageInfo info)
    {
        if (SelectedFailure == null) return;

        var boxWidth = 300f;
        var boxHeight = 80f;
        var boxX = (info.Width - boxWidth) / 2;
        var boxY = info.Height - boxHeight - 10;

        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#16213e"),
            Style = SKPaintStyle.Fill
        };

        using var borderPaint = new SKPaint
        {
            Color = GetSeverityColor(SelectedFailure.Severity),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };

        var rect = new SKRect(boxX, boxY, boxX + boxWidth, boxY + boxHeight);
        canvas.DrawRoundRect(rect, 8, 8, bgPaint);
        canvas.DrawRoundRect(rect, 8, 8, borderPaint);

        using var textPaint = new SKPaint
        {
            IsAntialias = true
        };

        var y = boxY + 20;

        using var boldFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 12);
        textPaint.Color = GetSeverityColor(SelectedFailure.Severity);
        canvas.DrawText($"{SelectedFailure.Category} ({SelectedFailure.Severity})", boxX + 15, y, SKTextAlign.Left, boldFont, textPaint);

        y += 18;
        using var normalFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 11);
        textPaint.Color = TextColor;

        // Truncate description if too long
        var desc = SelectedFailure.Description;
        if (desc.Length > 50)
            desc = desc[..47] + "...";

        canvas.DrawText(desc, boxX + 15, y, SKTextAlign.Left, normalFont, textPaint);

        y += 16;
        using var smallFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 10);
        textPaint.Color = TextColor.WithAlpha(150);
        canvas.DrawText($"Time: {SelectedFailure.T:P0}", boxX + 15, y, SKTextAlign.Left, smallFont, textPaint);
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType != SKTouchAction.Pressed || Failures == null) return;

        var trackLeft = 80f;
        var trackRight = Width - 20f;
        var trackY = Height / 2f;

        // Find closest failure
        FailureEvent? closest = null;
        var minDist = 20f; // Touch radius

        foreach (var failure in Failures)
        {
            var x = trackLeft + (float)failure.T * (trackRight - trackLeft);
            var dist = Math.Abs(e.Location.X - x);

            if (dist < minDist && Math.Abs(e.Location.Y - trackY) < 30)
            {
                minDist = (float)dist;
                closest = failure;
            }
        }

        SelectedFailure = closest;
        e.Handled = true;
        InvalidateSurface();
    }

    private static SKColor GetSeverityColor(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => CriticalColor,
        "warning" => WarningColor,
        _ => InfoColor
    };

    private static float GetSeverityRadius(string severity) => severity.ToLowerInvariant() switch
    {
        "critical" => 10f,
        "warning" => 7f,
        _ => 5f
    };
}
