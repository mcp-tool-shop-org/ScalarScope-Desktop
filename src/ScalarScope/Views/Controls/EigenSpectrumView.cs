using ScalarScope.Services;
using ScalarScope.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

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

        // Invariant checks for eigenvalues
        var eigenvalues = Session.CurrentEigenvalues.Values;
        InvariantGuard.AssertEigenvaluesSorted(eigenvalues, "EigenSpectrumView");
        InvariantGuard.AssertEigenvaluesNonNegative(eigenvalues, "EigenSpectrumView");

        DrawEigenBars(canvas, info);
        DrawEffectiveDimensionality(canvas, info);
        DrawInterpretation(canvas, info);
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 16);
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true
        };
        canvas.DrawText("Load a training run to see eigenvalues", info.Width / 2f, info.Height / 2f, SKTextAlign.Center, font, paint);
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

        using var textFont = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint
        {
            Color = SKColors.White.WithAlpha(180),
            IsAntialias = true
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
            canvas.DrawText($"{value:F2}", x + barWidth / 2, y - 8, SKTextAlign.Center, textFont, textPaint);

            // Index label
            canvas.DrawText($"λ{i + 1}", x + barWidth / 2, info.Height - padding + 15, SKTextAlign.Center, textFont, textPaint);
        }
    }

    private void DrawEffectiveDimensionality(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Session!.CurrentEigenvalues!.Values;
        if (eigenvalues.Count == 0) return;

        var total = eigenvalues.Sum();
        if (total < 0.001) return;

        // Use centralized calculations for consistency
        var effDim = ConsistencyCheckService.ComputeEffectiveDimensionality(eigenvalues, "EigenSpectrumView");
        var firstFactorVar = ConsistencyCheckService.ComputeFirstFactorVariance(eigenvalues, "EigenSpectrumView");

        using var font = new SKFont(SKTypeface.Default, 14);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var x = 15f;
        var y = 25f;

        // Effective dimensionality
        paint.Color = SKColors.White;
        canvas.DrawText($"Effective Dim: {effDim:F2} / {eigenvalues.Count}", x, y, SKTextAlign.Left, font, paint);

        y += 20;
        // First factor variance - use centralized threshold
        var interpretation = ConsistencyCheckService.GetEigenInterpretation(firstFactorVar);
        var varColor = interpretation == EigenInterpretation.StrongSharedAxis ||
                       interpretation == EigenInterpretation.ModerateUnification
            ? SKColors.LightGreen
            : SKColors.Orange;
        paint.Color = varColor;
        canvas.DrawText($"λ₁ Variance: {firstFactorVar:P0}", x, y, SKTextAlign.Left, font, paint);
    }

    private void DrawInterpretation(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Session!.CurrentEigenvalues!.Values;
        if (eigenvalues.Count == 0) return;

        // Use centralized calculation for consistency
        var firstFactorVar = ConsistencyCheckService.ComputeFirstFactorVariance(eigenvalues, "EigenSpectrumView");

        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint
        {
            IsAntialias = true
        };

        var x = info.Width - 200f;
        var y = 25f;

        // Use centralized interpretation for consistency
        var interpretation = ConsistencyCheckService.GetEigenInterpretation(firstFactorVar);
        var rgb = ConsistencyCheckService.GetInterpretationColor(interpretation);
        var color = new SKColor(rgb.R, rgb.G, rgb.B);

        string interpretationText = interpretation switch
        {
            EigenInterpretation.StrongSharedAxis => "Strong shared axis",
            EigenInterpretation.ModerateUnification => "Moderate unification",
            EigenInterpretation.PartialStructure => "Partial structure",
            EigenInterpretation.OrthogonalEvaluators => "Orthogonal evaluators",
            _ => "Unknown"
        };

        paint.Color = color;
        canvas.DrawText(interpretationText, x, y, SKTextAlign.Left, font, paint);

        y += 18;
        paint.Color = SKColors.Gray;
        var transferPrediction = firstFactorVar > 0.4 ? "Transfer viable" : "Transfer unlikely";
        canvas.DrawText(transferPrediction, x, y, SKTextAlign.Left, font, paint);
    }
}
