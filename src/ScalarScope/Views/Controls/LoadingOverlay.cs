using ScalarScope.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Phase 5.2: Loading overlay with structural skeleton (plot scaffold).
/// Replaces generic spinners with content-shaped placeholders.
/// </summary>
public class LoadingOverlay : SKCanvasView
{
    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay), false,
            propertyChanged: OnIsLoadingChanged);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingOverlay), "Loading...");
    
    /// <summary>
    /// Phase 5.2: Skeleton type determines structural shape.
    /// </summary>
    public static readonly BindableProperty SkeletonTypeProperty =
        BindableProperty.Create(nameof(SkeletonType), typeof(LoadingSkeletonType), typeof(LoadingOverlay), LoadingSkeletonType.Bars);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }
    
    public LoadingSkeletonType SkeletonType
    {
        get => (LoadingSkeletonType)GetValue(SkeletonTypeProperty);
        set => SetValue(SkeletonTypeProperty, value);
    }

    private IDispatcherTimer? _animationTimer;
    private float _shimmerOffset;
    private const float ShimmerSpeed = 3f;

    public LoadingOverlay()
    {
        PaintSurface += OnPaintSurface;
        InputTransparent = false;
    }

    private static void OnIsLoadingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LoadingOverlay overlay)
        {
            if ((bool)newValue)
            {
                overlay.StartAnimation();
            }
            else
            {
                overlay.StopAnimation();
            }
        }
    }

    private void StartAnimation()
    {
        _animationTimer?.Stop();
        _shimmerOffset = 0;
        
        _animationTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_animationTimer != null)
        {
            _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
            _animationTimer.Tick += (s, e) =>
            {
                _shimmerOffset += ShimmerSpeed;
                if (_shimmerOffset > 400) _shimmerOffset = -200;
                InvalidateSurface();
            };
            _animationTimer.Start();
        }
        
        IsVisible = true;
        InvalidateSurface();
    }

    private void StopAnimation()
    {
        _animationTimer?.Stop();
        _animationTimer = null;
        IsVisible = false;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Semi-transparent dark background
        canvas.Clear(SKColor.Parse("#dd0f0f1a"));

        var centerX = info.Width / 2f;
        var centerY = info.Height / 2f;

        // Phase 5.2: Draw content-appropriate skeleton
        switch (SkeletonType)
        {
            case LoadingSkeletonType.PlotScaffold:
                DrawPlotScaffold(canvas, info.Width, info.Height);
                break;
            case LoadingSkeletonType.DeltaList:
                DrawDeltaListSkeleton(canvas, centerX, centerY - 80);
                break;
            default:
                DrawShimmerBars(canvas, centerX, centerY - 60, info.Width);
                break;
        }

        // Draw loading message
        using var textFont = new SKFont(SKTypeface.Default, 18);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawText(Message, centerX, centerY + 40, SKTextAlign.Center, textFont, textPaint);

        // Skip spinner dots if using structural skeleton (reduced motion friendliness)
        if (SkeletonType == LoadingSkeletonType.Bars && MotionTokens.ShouldAnimate("loading.dots"))
        {
            DrawSpinnerDots(canvas, centerX, centerY + 70);
        }
    }
    
    /// <summary>
    /// Phase 5.2: Draw plot scaffold skeleton with axes outlines.
    /// Gives user a structural preview of what's loading.
    /// </summary>
    private void DrawPlotScaffold(SKCanvas canvas, float width, float height)
    {
        var margin = 60f;
        var plotRect = new SKRect(margin, margin, width - margin, height - margin - 80);
        
        using var scaffoldPaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([8, 8], _shimmerOffset / 10)
        };
        
        // Draw axes placeholder
        canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Left, plotRect.Top, scaffoldPaint);
        canvas.DrawLine(plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom, scaffoldPaint);
        
        // Draw placeholder grid lines
        for (int i = 1; i < 4; i++)
        {
            var y = plotRect.Top + plotRect.Height * i / 4;
            canvas.DrawLine(plotRect.Left, y, plotRect.Right, y, scaffoldPaint);
            
            var x = plotRect.Left + plotRect.Width * i / 4;
            canvas.DrawLine(x, plotRect.Top, x, plotRect.Bottom, scaffoldPaint);
        }
        
        // Draw trajectory placeholder curve
        using var curvePaint = new SKPaint
        {
            Color = SKColor.Parse("#3a3a6e"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            IsAntialias = true
        };
        
        using var path = new SKPath();
        path.MoveTo(plotRect.Left + 20, plotRect.MidY + 30);
        path.CubicTo(
            plotRect.Left + plotRect.Width * 0.3f, plotRect.MidY - 50,
            plotRect.Left + plotRect.Width * 0.7f, plotRect.MidY + 50,
            plotRect.Right - 20, plotRect.MidY - 20);
        
        canvas.DrawPath(path, curvePaint);
        
        // Draw shimmer highlight on curve
        DrawPathShimmer(canvas, path, plotRect);
    }
    
    /// <summary>
    /// Phase 5.2: Draw delta list skeleton placeholders.
    /// </summary>
    private void DrawDeltaListSkeleton(SKCanvas canvas, float centerX, float startY)
    {
        using var cardPaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        var cardWidth = 280f;
        var cardHeight = 50f;
        var cardSpacing = 60f;
        
        for (int i = 0; i < 3; i++)
        {
            var y = startY + i * cardSpacing;
            var rect = new SKRect(centerX - cardWidth / 2, y, centerX + cardWidth / 2, y + cardHeight);
            canvas.DrawRoundRect(rect, 8, 8, cardPaint);
            
            // Content placeholder lines
            using var linePaint = new SKPaint
            {
                Color = SKColor.Parse("#3a3a6e"),
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRect(rect.Left + 12, rect.Top + 12, rect.Left + 80, rect.Top + 22), 4, 4, linePaint);
            canvas.DrawRoundRect(new SKRect(rect.Left + 12, rect.Top + 30, rect.Right - 40, rect.Top + 38), 4, 4, linePaint);
            
            // Delta icon placeholder circle
            canvas.DrawCircle(rect.Right - 24, rect.MidY, 12, linePaint);
            
            DrawShimmerHighlight(canvas, rect);
        }
    }
    
    /// <summary>
    /// Phase 5.2: Draw shimmer along path for curve skeleton.
    /// </summary>
    private void DrawPathShimmer(SKCanvas canvas, SKPath path, SKRect bounds)
    {
        using var shimmerPaint = new SKPaint
        {
            Color = SKColor.Parse("#4000d9ff"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            IsAntialias = true
        };
        
        var shimmerPosition = (_shimmerOffset / 400f) % 1f;
        
        using var measure = new SKPathMeasure(path, false);
        var length = measure.Length;
        var position = shimmerPosition * length;
        
        // Draw small lit segment
        using var shimmerPath = new SKPath();
        measure.GetSegment(Math.Max(0, position - 40), Math.Min(length, position + 40), shimmerPath, true);
        
        canvas.DrawPath(shimmerPath, shimmerPaint);
    }

    private void DrawShimmerBars(SKCanvas canvas, float centerX, float startY, float width)
    {
        var barWidth = Math.Min(400f, width * 0.6f);
        var barHeight = 12f;
        var barSpacing = 20f;
        var startX = centerX - barWidth / 2;

        using var basePaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Draw 3 shimmer bars with varying widths
        float[] barWidthMultipliers = [1f, 0.7f, 0.85f];
        for (int i = 0; i < 3; i++)
        {
            var y = startY + i * barSpacing;
            var w = barWidth * barWidthMultipliers[i];
            var rect = new SKRect(startX, y, startX + w, y + barHeight);
            
            canvas.DrawRoundRect(rect, 6, 6, basePaint);

            // Draw shimmer highlight
            DrawShimmerHighlight(canvas, rect);
        }
    }

    private void DrawShimmerHighlight(SKCanvas canvas, SKRect rect)
    {
        var shimmerX = rect.Left + _shimmerOffset;
        
        // Only draw if shimmer is in visible range
        if (shimmerX < rect.Left - 100 || shimmerX > rect.Right + 100) return;

        using var highlightPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Create gradient shimmer effect
        highlightPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(shimmerX - 100, 0),
            new SKPoint(shimmerX + 100, 0),
            new[] { SKColor.Parse("#002a2a4e"), SKColor.Parse("#4000d9ff"), SKColor.Parse("#002a2a4e") },
            new[] { 0f, 0.5f, 1f },
            SKShaderTileMode.Clamp);

        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(rect, 6, 6));
        canvas.DrawRect(rect, highlightPaint);
        canvas.Restore();
    }

    private void DrawSpinnerDots(SKCanvas canvas, float centerX, float centerY)
    {
        var dotCount = 3;
        var dotRadius = 5f;
        var dotSpacing = 20f;
        var startX = centerX - (dotCount - 1) * dotSpacing / 2;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var time = _shimmerOffset / 50f; // Use shimmer offset for animation timing
        for (int i = 0; i < dotCount; i++)
        {
            // Staggered bounce animation
            var phase = (time + i * 0.4f) % 3f;
            var scale = phase < 1f ? 1f + MathF.Sin(phase * MathF.PI) * 0.5f : 1f;
            var alpha = (byte)(180 + (scale - 1f) * 150);
            
            paint.Color = SKColor.Parse("#00d9ff").WithAlpha(alpha);
            canvas.DrawCircle(startX + i * dotSpacing, centerY - (scale - 1f) * 10, dotRadius * scale, paint);
        }
    }
}

/// <summary>
/// Phase 5.2: Skeleton types for loading indication.
/// </summary>
public enum LoadingSkeletonType
{
    /// <summary>Generic shimmer bars.</summary>
    Bars,
    
    /// <summary>Plot scaffold with axes outlines and trajectory placeholder.</summary>
    PlotScaffold,
    
    /// <summary>Delta list card placeholders.</summary>
    DeltaList
}