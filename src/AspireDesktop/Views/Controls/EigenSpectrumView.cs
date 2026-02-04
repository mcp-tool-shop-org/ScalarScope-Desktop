using AspireDesktop.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace AspireDesktop.Views.Controls;

/// <summary>
/// Eigen-Spectrum "Breathing" visualization.
/// Shows top N eigenvalues animated over time.
/// One bar growing dominant = shared latent structure (Path B success).
/// Multiple bars stable = plural evaluative axes (Path A).
/// </summary>
public class EigenSpectrumView : SKCanvasView
{
    public static readonly BindableProperty SessionProperty =
        BindableProperty.Create(nameof(Session), typeof(VortexSessionViewModel), typeof(EigenSpectrumView),
            propertyChanged: OnSessionChanged);

    public VortexSessionViewModel? Session
    {
        get => (VortexSessionViewModel?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    private static readonly SKColor BackgroundColor = SKColor.Parse("#1a1a2e");
    private static readonly SKColor[] EigenColors =
    [
        SKColor.Parse("#00d9ff"),
        SKColor.Parse("#00ff88"),
        SKColor.Parse("#ffd93d"),
        SKColor.Parse("#ff6b6b"),
        SKColor.Parse("#c56cf0"),
    ];

    public EigenSpectrumView()
    {
        PaintSurface += OnPaintSurface;
    }

    private static void OnSessionChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is EigenSpectrumView canvas)
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

        if (Session?.Run is null || Session.CurrentEigenvalues is null)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        DrawEigenBars(canvas, info);
        DrawEffectiveDimensionality(canvas, info);
        DrawInterpretation(canvas, info);
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            TextSize = 16,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText("Load a geometry run to visualize", info.Width / 2f, info.Height / 2f, paint);
    }

    private void DrawEigenBars(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Session!.CurrentEigenvalues!.Values;
        if (eigenvalues.Count == 0) return;

        var maxEigen = eigenvalues.Max();
        if (maxEigen < 0.001) maxEigen = 1;

        var padding = 40f;
        var barWidth = (info.Width - padding * 2) / eigenvalues.Count * 0.7f;
        var gap = (info.Width - padding * 2) / eigenvalues.Count * 0.3f;
        var maxHeight = info.Height - padding * 3;

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var glowPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8)
        };

        using var textPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            TextSize = 11,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        for (int i = 0; i < Math.Min(eigenvalues.Count, 5); i++)
        {
            var value = eigenvalues[i];
            var normalizedHeight = (float)(value / maxEigen) * maxHeight;
            var x = padding + i * (barWidth + gap);
            var y = info.Height - padding - normalizedHeight;

            // Glow effect
            glowPaint.Color = EigenColors[i % EigenColors.Length].WithAlpha(60);
            canvas.DrawRoundRect(x - 5, y - 5, barWidth + 10, normalizedHeight + 10, 8, 8, glowPaint);

            // Bar
            paint.Color = EigenColors[i % EigenColors.Length];
            canvas.DrawRoundRect(x, y, barWidth, normalizedHeight, 4, 4, paint);

            // Value label
            canvas.DrawText($"{value:F2}", x + barWidth / 2, y - 8, textPaint);

            // Index label
            canvas.DrawText($"λ{i + 1}", x + barWidth / 2, info.Height - padding + 15, textPaint);
        }
    }

    private void DrawEffectiveDimensionality(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Session!.CurrentEigenvalues!.Values;
        if (eigenvalues.Count == 0) return;

        // Compute effective dimensionality (participation ratio)
        var total = eigenvalues.Sum();
        if (total < 0.001) return;

        var normalized = eigenvalues.Select(e => e / total).ToList();
        var sumSq = normalized.Sum(n => n * n);
        var effDim = sumSq > 0 ? 1.0 / sumSq : eigenvalues.Count;

        // Compute first factor variance
        var firstFactorVar = eigenvalues[0] / total;

        using var paint = new SKPaint
        {
            TextSize = 14,
            IsAntialias = true
        };

        var x = 15f;
        var y = 25f;

        // Effective dimensionality
        paint.Color = SKColors.White;
        canvas.DrawText($"Effective Dim: {effDim:F2} / {eigenvalues.Count}", x, y, paint);

        y += 20;
        // First factor variance
        var varColor = firstFactorVar > 0.5 ? SKColors.LightGreen : SKColors.Orange;
        paint.Color = varColor;
        canvas.DrawText($"λ₁ Variance: {firstFactorVar:P0}", x, y, paint);
    }

    private void DrawInterpretation(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Session!.CurrentEigenvalues!.Values;
        if (eigenvalues.Count == 0) return;

        var total = eigenvalues.Sum();
        if (total < 0.001) return;

        var firstFactorVar = eigenvalues[0] / total;

        using var paint = new SKPaint
        {
            TextSize = 12,
            IsAntialias = true
        };

        var x = info.Width - 200f;
        var y = 25f;

        string interpretation;
        SKColor color;

        if (firstFactorVar > 0.7)
        {
            interpretation = "Strong shared axis";
            color = SKColors.LightGreen;
        }
        else if (firstFactorVar > 0.5)
        {
            interpretation = "Moderate unification";
            color = SKColors.Yellow;
        }
        else if (firstFactorVar > 0.3)
        {
            interpretation = "Partial structure";
            color = SKColors.Orange;
        }
        else
        {
            interpretation = "Orthogonal evaluators";
            color = SKColors.Red;
        }

        paint.Color = color;
        canvas.DrawText(interpretation, x, y, paint);

        y += 18;
        paint.Color = SKColors.Gray;
        var transferPrediction = firstFactorVar > 0.4 ? "Transfer viable" : "Transfer unlikely";
        canvas.DrawText(transferPrediction, x, y, paint);
    }
}
