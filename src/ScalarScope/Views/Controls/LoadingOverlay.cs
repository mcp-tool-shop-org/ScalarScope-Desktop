using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Loading overlay with shimmer animation effect.
/// </summary>
public class LoadingOverlay : SKCanvasView
{
    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(LoadingOverlay), false,
            propertyChanged: OnIsLoadingChanged);

    public static readonly BindableProperty MessageProperty =
        BindableProperty.Create(nameof(Message), typeof(string), typeof(LoadingOverlay), "Loading...");

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

        // Draw shimmer bars (fake loading skeleton)
        DrawShimmerBars(canvas, centerX, centerY - 60, info.Width);

        // Draw loading message
        using var textFont = new SKFont(SKTypeface.Default, 18);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawText(Message, centerX, centerY + 40, SKTextAlign.Center, textFont, textPaint);

        // Draw animated spinner dots
        DrawSpinnerDots(canvas, centerX, centerY + 70);
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
