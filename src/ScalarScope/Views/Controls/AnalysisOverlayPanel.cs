using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Quantitative analysis overlay showing eigenvalue timeline, Lyapunov exponent,
/// bifurcation markers, and dimension collapse alerts synchronized with playback.
/// Phase 3.1 implementation.
/// </summary>
public class AnalysisOverlayPanel : SKCanvasView
{
    private readonly AnalysisService _analysisService = new();
    private ComprehensiveAnalysis? _analysis;

    public static readonly BindableProperty SessionProperty =
        BindableProperty.Create(nameof(Session), typeof(VortexSessionViewModel), typeof(AnalysisOverlayPanel),
            propertyChanged: OnSessionChanged);

    public static readonly BindableProperty ShowEigenvaluesProperty =
        BindableProperty.Create(nameof(ShowEigenvalues), typeof(bool), typeof(AnalysisOverlayPanel), true,
            propertyChanged: OnDisplayPropertyChanged);

    public static readonly BindableProperty ShowLyapunovProperty =
        BindableProperty.Create(nameof(ShowLyapunov), typeof(bool), typeof(AnalysisOverlayPanel), true,
            propertyChanged: OnDisplayPropertyChanged);

    public static readonly BindableProperty ShowBifurcationsProperty =
        BindableProperty.Create(nameof(ShowBifurcations), typeof(bool), typeof(AnalysisOverlayPanel), true,
            propertyChanged: OnDisplayPropertyChanged);

    public static readonly BindableProperty ShowDimensionAlertsProperty =
        BindableProperty.Create(nameof(ShowDimensionAlerts), typeof(bool), typeof(AnalysisOverlayPanel), true,
            propertyChanged: OnDisplayPropertyChanged);

    public VortexSessionViewModel? Session
    {
        get => (VortexSessionViewModel?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    public bool ShowEigenvalues
    {
        get => (bool)GetValue(ShowEigenvaluesProperty);
        set => SetValue(ShowEigenvaluesProperty, value);
    }

    public bool ShowLyapunov
    {
        get => (bool)GetValue(ShowLyapunovProperty);
        set => SetValue(ShowLyapunovProperty, value);
    }

    public bool ShowBifurcations
    {
        get => (bool)GetValue(ShowBifurcationsProperty);
        set => SetValue(ShowBifurcationsProperty, value);
    }

    public bool ShowDimensionAlerts
    {
        get => (bool)GetValue(ShowDimensionAlertsProperty);
        set => SetValue(ShowDimensionAlertsProperty, value);
    }

    // Colors
    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor Lambda1Color = SKColor.Parse("#00d9ff");
    private static readonly SKColor Lambda2Color = SKColor.Parse("#00ff88");
    private static readonly SKColor LyapunovPositiveColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor LyapunovNegativeColor = SKColor.Parse("#4caf50");
    private static readonly SKColor LyapunovNeutralColor = SKColor.Parse("#ffd93d");
    private static readonly SKColor BifurcationMarkerColor = SKColor.Parse("#ff9500");
    private static readonly SKColor DimensionAlertColor = SKColor.Parse("#e91e63");
    private static readonly SKColor TimeIndicatorColor = SKColor.Parse("#ffffff");
    private static readonly SKColor GridColor = SKColor.Parse("#333355");
    private static readonly SKColor TextColor = SKColor.Parse("#aaaacc");

    // Phase 1: Demo state fields
    private Models.GeometryRun? _currentRenderRun;
    private bool _isRenderingDemo;

    public AnalysisOverlayPanel()
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
        if (bindable is AnalysisOverlayPanel panel)
        {
            if (oldValue is VortexSessionViewModel oldSession)
            {
                oldSession.Player.TimeChanged -= panel.OnTimeChanged;
            }
            if (newValue is VortexSessionViewModel newSession)
            {
                newSession.Player.TimeChanged += panel.OnTimeChanged;
                panel.RefreshAnalysis();
            }
            else
            {
                panel._analysis = null;
            }
            panel.InvalidateSurface();
        }
    }

    private static void OnDisplayPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnalysisOverlayPanel panel)
        {
            panel.InvalidateSurface();
        }
    }

    private void OnTimeChanged()
    {
        MainThread.BeginInvokeOnMainThread(InvalidateSurface);
    }

    /// <summary>
    /// Recompute analysis when run changes.
    /// </summary>
    public void RefreshAnalysis()
    {
        if (Session?.Run != null)
        {
            _analysis = _analysisService.AnalyzeRun(Session.Run);
        }
        else
        {
            _analysis = null;
        }
        InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        // Phase 1: Use demo data when no session available
        _currentRenderRun = Session?.Run;
        _isRenderingDemo = false;
        
        if (_currentRenderRun == null)
        {
            _currentRenderRun = DemoStateService.Instance.DemoRun;
            _isRenderingDemo = true;
            
            // Generate analysis for demo run if needed
            if (_currentRenderRun != null && _analysis == null)
            {
                _analysis = _analysisService.AnalyzeRun(_currentRenderRun);
            }
        }

        if (_currentRenderRun == null || _analysis == null)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        // Phase 1: Get time from demo or session
        var currentTime = _isRenderingDemo ? DemoStateService.Instance.AnimationTime : Session!.Player.Time;
        var duration = _isRenderingDemo ? 1.0 : Session!.Player.Duration;

        // Layout: divide panel into sections
        var margin = 10f;
        var labelWidth = 80f;
        var plotArea = new SKRect(
            margin + labelWidth,
            margin,
            info.Width - margin,
            info.Height - margin);

        var sectionHeight = plotArea.Height / 3;

        // Draw grid and time indicator
        DrawTimeGrid(canvas, plotArea, duration);
        DrawTimeIndicator(canvas, plotArea, currentTime, duration);

        // Section 1: Eigenvalue Timeline
        if (ShowEigenvalues)
        {
            var eigenRect = new SKRect(plotArea.Left, plotArea.Top, plotArea.Right, plotArea.Top + sectionHeight);
            DrawEigenvalueTimeline(canvas, eigenRect, currentTime);
            DrawLabel(canvas, "λ₁/λ₂", margin, eigenRect.MidY);
        }

        // Section 2: Lyapunov Exponent
        if (ShowLyapunov)
        {
            var lyapRect = new SKRect(plotArea.Left, plotArea.Top + sectionHeight, plotArea.Right, plotArea.Top + sectionHeight * 2);
            DrawLyapunovTimeline(canvas, lyapRect, currentTime);
            DrawLabel(canvas, "Lyapunov", margin, lyapRect.MidY);
        }

        // Section 3: Bifurcations & Dimension
        if (ShowBifurcations || ShowDimensionAlerts)
        {
            var eventsRect = new SKRect(plotArea.Left, plotArea.Top + sectionHeight * 2, plotArea.Right, plotArea.Bottom);
            DrawEventsTimeline(canvas, eventsRect, currentTime, duration);
            DrawLabel(canvas, "Events", margin, eventsRect.MidY);
        }

        // Draw classification badge
        DrawClassificationBadge(canvas, info);
        
        // Phase 1: Draw demo badge if showing demo data
        if (_isRenderingDemo)
        {
            DrawDemoBadge(canvas, info);
        }
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        using var paint = new SKPaint { Color = TextColor, IsAntialias = true };
        canvas.DrawText("Load a training run to see analysis", 
            info.Width / 2f, info.Height / 2f, SKTextAlign.Center, font, paint);
    }

    private void DrawLabel(SKCanvas canvas, string text, float x, float y)
    {
        using var font = new SKFont(SKTypeface.Default, 11);
        using var paint = new SKPaint { Color = TextColor, IsAntialias = true };
        canvas.DrawText(text, x, y + 4, SKTextAlign.Left, font, paint);
    }

    private void DrawTimeGrid(SKCanvas canvas, SKRect area, double duration)
    {
        using var paint = new SKPaint
        {
            Color = GridColor,
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        // Vertical time lines
        var numLines = 10;
        for (int i = 0; i <= numLines; i++)
        {
            var x = area.Left + (area.Width * i / numLines);
            canvas.DrawLine(x, area.Top, x, area.Bottom, paint);
        }

        // Horizontal section dividers
        var sectionHeight = area.Height / 3;
        for (int i = 1; i < 3; i++)
        {
            var y = area.Top + sectionHeight * i;
            canvas.DrawLine(area.Left, y, area.Right, y, paint);
        }
    }

    private void DrawTimeIndicator(SKCanvas canvas, SKRect area, double currentTime, double duration)
    {
        if (duration <= 0) return;

        var x = (float)(area.Left + (currentTime / duration) * area.Width);

        using var paint = new SKPaint
        {
            Color = TimeIndicatorColor.WithAlpha(180),
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        canvas.DrawLine(x, area.Top, x, area.Bottom, paint);
    }

    private void DrawEigenvalueTimeline(SKCanvas canvas, SKRect area, double currentTime)
    {
        var timeline = _analysis?.Eigenvalues;
        if (timeline?.Points == null || timeline.Points.Count == 0) return;

        var timeRange = timeline.TimeEnd - timeline.TimeStart;
        if (timeRange <= 0) return;

        // Normalize eigenvalues to [0,1] for display
        var maxLambda = Math.Max(timeline.MaxLambda1, timeline.MaxLambda2);
        if (maxLambda <= 0) maxLambda = 1;

        using var lambda1Paint = new SKPaint
        {
            Color = Lambda1Color,
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        using var lambda2Paint = new SKPaint
        {
            Color = Lambda2Color,
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        var path1 = new SKPath();
        var path2 = new SKPath();

        for (int i = 0; i < timeline.Points.Count; i++)
        {
            var point = timeline.Points[i];
            var x = (float)(area.Left + ((point.Time - timeline.TimeStart) / timeRange) * area.Width);
            var y1 = (float)(area.Bottom - (point.Lambda1 / maxLambda) * area.Height * 0.9);
            var y2 = (float)(area.Bottom - (point.Lambda2 / maxLambda) * area.Height * 0.9);

            y1 = Math.Clamp(y1, area.Top + 5, area.Bottom - 5);
            y2 = Math.Clamp(y2, area.Top + 5, area.Bottom - 5);

            if (i == 0)
            {
                path1.MoveTo(x, y1);
                path2.MoveTo(x, y2);
            }
            else
            {
                path1.LineTo(x, y1);
                path2.LineTo(x, y2);
            }
        }

        canvas.DrawPath(path1, lambda1Paint);
        canvas.DrawPath(path2, lambda2Paint);

        // Draw current values
        var currentPoint = timeline.Points.MinBy(p => Math.Abs(p.Time - currentTime));
        if (currentPoint != null)
        {
            var cx = (float)(area.Left + ((currentPoint.Time - timeline.TimeStart) / timeRange) * area.Width);
            var cy1 = (float)(area.Bottom - (currentPoint.Lambda1 / maxLambda) * area.Height * 0.9);
            var cy2 = (float)(area.Bottom - (currentPoint.Lambda2 / maxLambda) * area.Height * 0.9);

            lambda1Paint.Style = SKPaintStyle.Fill;
            lambda2Paint.Style = SKPaintStyle.Fill;

            canvas.DrawCircle(cx, Math.Clamp(cy1, area.Top + 5, area.Bottom - 5), 4, lambda1Paint);
            canvas.DrawCircle(cx, Math.Clamp(cy2, area.Top + 5, area.Bottom - 5), 4, lambda2Paint);
        }

        // Legend
        using var font = new SKFont(SKTypeface.Default, 9);
        using var textPaint = new SKPaint { IsAntialias = true };
        
        textPaint.Color = Lambda1Color;
        canvas.DrawText("λ₁", area.Right - 30, area.Top + 12, SKTextAlign.Left, font, textPaint);
        
        textPaint.Color = Lambda2Color;
        canvas.DrawText("λ₂", area.Right - 15, area.Top + 12, SKTextAlign.Left, font, textPaint);
    }

    private void DrawLyapunovTimeline(SKCanvas canvas, SKRect area, double currentTime)
    {
        var lyapunov = _analysis?.Lyapunov;
        if (lyapunov?.Points == null || lyapunov.Points.Count == 0) return;

        var timeStart = lyapunov.Points.Min(p => p.Time);
        var timeEnd = lyapunov.Points.Max(p => p.Time);
        var timeRange = timeEnd - timeStart;
        if (timeRange <= 0) return;

        var maxAbs = lyapunov.Points.Max(p => Math.Abs(p.Value));
        if (maxAbs < 0.001) maxAbs = 0.1;

        // Draw zero line
        var zeroY = area.MidY;
        using var zeroPaint = new SKPaint
        {
            Color = GridColor.WithAlpha(150),
            StrokeWidth = 1,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([4, 4], 0)
        };
        canvas.DrawLine(area.Left, zeroY, area.Right, zeroY, zeroPaint);

        // Draw Lyapunov curve with color indicating chaos
        for (int i = 1; i < lyapunov.Points.Count; i++)
        {
            var p1 = lyapunov.Points[i - 1];
            var p2 = lyapunov.Points[i];

            var x1 = (float)(area.Left + ((p1.Time - timeStart) / timeRange) * area.Width);
            var x2 = (float)(area.Left + ((p2.Time - timeStart) / timeRange) * area.Width);
            var y1 = (float)(zeroY - (p1.Value / maxAbs) * (area.Height / 2) * 0.8);
            var y2 = (float)(zeroY - (p2.Value / maxAbs) * (area.Height / 2) * 0.8);

            y1 = Math.Clamp(y1, area.Top + 3, area.Bottom - 3);
            y2 = Math.Clamp(y2, area.Top + 3, area.Bottom - 3);

            var color = p2.IsChaotic ? LyapunovPositiveColor
                      : p2.Value < -0.01 ? LyapunovNegativeColor
                      : LyapunovNeutralColor;

            using var paint = new SKPaint
            {
                Color = color,
                StrokeWidth = 2,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        // Current value indicator
        var currentPoint = lyapunov.Points.MinBy(p => Math.Abs(p.Time - currentTime));
        if (currentPoint != null)
        {
            var cx = (float)(area.Left + ((currentPoint.Time - timeStart) / timeRange) * area.Width);
            var cy = (float)(zeroY - (currentPoint.Value / maxAbs) * (area.Height / 2) * 0.8);
            cy = Math.Clamp(cy, area.Top + 3, area.Bottom - 3);

            var color = currentPoint.IsChaotic ? LyapunovPositiveColor
                      : currentPoint.Value < -0.01 ? LyapunovNegativeColor
                      : LyapunovNeutralColor;

            using var paint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawCircle(cx, cy, 5, paint);
        }

        // Labels
        using var font = new SKFont(SKTypeface.Default, 8);
        using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };
        canvas.DrawText("+chaos", area.Right - 35, area.Top + 10, SKTextAlign.Left, font, textPaint);
        canvas.DrawText("-stable", area.Right - 35, area.Bottom - 5, SKTextAlign.Left, font, textPaint);
    }

    private void DrawEventsTimeline(SKCanvas canvas, SKRect area, double currentTime, double duration)
    {
        if (duration <= 0) return;

        // Draw bifurcation markers
        if (ShowBifurcations && _analysis?.Bifurcations.Markers != null)
        {
            using var paint = new SKPaint
            {
                Color = BifurcationMarkerColor,
                IsAntialias = true
            };

            using var font = new SKFont(SKTypeface.Default, 8);

            foreach (var marker in _analysis.Bifurcations.Markers)
            {
                var x = (float)(area.Left + (marker.Time / duration) * area.Width);
                var y = area.Top + area.Height * 0.3f;

                // Draw diamond marker
                var path = new SKPath();
                path.MoveTo(x, y - 6);
                path.LineTo(x + 5, y);
                path.LineTo(x, y + 6);
                path.LineTo(x - 5, y);
                path.Close();

                paint.Style = SKPaintStyle.Fill;
                paint.Color = BifurcationMarkerColor.WithAlpha((byte)(marker.Confidence * 200 + 55));
                canvas.DrawPath(path, paint);

                // Type label
                var typeChar = marker.Type switch
                {
                    BifurcationType.FoldBifurcation => "F",
                    BifurcationType.HopfBifurcation => "H",
                    BifurcationType.PitchforkBifurcation => "P",
                    BifurcationType.Saddle => "S",
                    _ => "?"
                };

                paint.Color = SKColors.White;
                canvas.DrawText(typeChar, x, y + 3, SKTextAlign.Center, font, paint);
            }
        }

        // Draw dimension collapse alerts
        if (ShowDimensionAlerts && _analysis?.DimensionCollapse.Alerts != null)
        {
            foreach (var alert in _analysis.DimensionCollapse.Alerts)
            {
                var x1 = (float)(area.Left + (alert.StartTime / duration) * area.Width);
                var x2 = (float)(area.Left + (alert.EndTime / duration) * area.Width);
                if (x2 < x1 + 2) x2 = x1 + 4; // Minimum width

                var y = area.Top + area.Height * 0.7f;
                var height = 12f;

                var alpha = alert.Severity switch
                {
                    CollapseSeverity.Critical => (byte)200,
                    CollapseSeverity.Warning => (byte)150,
                    _ => (byte)100
                };

                using var paint = new SKPaint
                {
                    Color = DimensionAlertColor.WithAlpha(alpha),
                    IsAntialias = true
                };

                var rect = new SKRect(x1, y - height / 2, x2, y + height / 2);
                canvas.DrawRoundRect(rect, 3, 3, paint);

                // Draw dimension value if there's room
                if (x2 - x1 > 20)
                {
                    using var font = new SKFont(SKTypeface.Default, 8);
                    using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
                    canvas.DrawText($"d={alert.MinDimension:F1}", (x1 + x2) / 2, y + 3, SKTextAlign.Center, font, textPaint);
                }
            }
        }

        // Legend
        using var legendFont = new SKFont(SKTypeface.Default, 8);
        using var legendPaint = new SKPaint { IsAntialias = true };

        if (ShowBifurcations)
        {
            legendPaint.Color = BifurcationMarkerColor;
            canvas.DrawText("◆ Bifurcation", area.Left + 5, area.Bottom - 5, SKTextAlign.Left, legendFont, legendPaint);
        }

        if (ShowDimensionAlerts)
        {
            legendPaint.Color = DimensionAlertColor;
            canvas.DrawText("■ Dim Collapse", area.Left + 80, area.Bottom - 5, SKTextAlign.Left, legendFont, legendPaint);
        }
    }

    private void DrawClassificationBadge(SKCanvas canvas, SKImageInfo info)
    {
        var classification = _analysis?.Lyapunov.Classification ?? "Unknown";
        
        var badgeColor = classification switch
        {
            "Chaotic" => LyapunovPositiveColor,
            "Edge of Chaos" => LyapunovNeutralColor,
            "Stable" => LyapunovNegativeColor,
            _ => TextColor
        };

        using var font = new SKFont(SKTypeface.Default, 10);
        using var bgPaint = new SKPaint
        {
            Color = badgeColor.WithAlpha(40),
            IsAntialias = true
        };
        using var textPaint = new SKPaint
        {
            Color = badgeColor,
            IsAntialias = true
        };

        var text = $"Dynamics: {classification}";
        var textWidth = font.MeasureText(text);
        
        var badgeRect = new SKRect(
            info.Width - textWidth - 20,
            5,
            info.Width - 5,
            22);

        canvas.DrawRoundRect(badgeRect, 4, 4, bgPaint);
        canvas.DrawText(text, badgeRect.Left + 7, badgeRect.MidY + 4, SKTextAlign.Left, font, textPaint);
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
            8,  // Left side to avoid overlapping with classification badge
            8,
            8 + textBounds.Width + padding * 2,
            8 + textBounds.Height + padding * 2
        );

        canvas.DrawRoundRect(rect, 3, 3, bgPaint);
        canvas.DrawText(text, rect.MidX, rect.MidY + textBounds.Height / 2, SKTextAlign.Center, font, textPaint);
    }
}
