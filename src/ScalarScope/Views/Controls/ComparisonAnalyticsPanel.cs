using ScalarScope.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Panel showing comparative analytics between two runs.
/// Displays delta metrics, correlation analysis, and phase comparison.
/// </summary>
public class ComparisonAnalyticsPanel : SKCanvasView
{
    public static readonly BindableProperty LeftRunProperty =
        BindableProperty.Create(nameof(LeftRun), typeof(GeometryRun), typeof(ComparisonAnalyticsPanel),
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty RightRunProperty =
        BindableProperty.Create(nameof(RightRun), typeof(GeometryRun), typeof(ComparisonAnalyticsPanel),
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(ComparisonAnalyticsPanel), 0.0,
            propertyChanged: OnPropertyChangedInvalidate);

    public GeometryRun? LeftRun
    {
        get => (GeometryRun?)GetValue(LeftRunProperty);
        set => SetValue(LeftRunProperty, value);
    }

    public GeometryRun? RightRun
    {
        get => (GeometryRun?)GetValue(RightRunProperty);
        set => SetValue(RightRunProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    private static readonly SKColor BackgroundColor = SKColor.Parse("#16213e");
    private static readonly SKColor LeftColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor RightColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor NeutralColor = SKColor.Parse("#ffd93d");
    private static readonly SKColor TextColor = SKColors.White;

    public ComparisonAnalyticsPanel()
    {
        PaintSurface += OnPaintSurface;
    }

    private static void OnPropertyChangedInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ComparisonAnalyticsPanel panel)
            panel.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        if (LeftRun == null || RightRun == null)
        {
            DrawNoDataMessage(canvas, info);
            return;
        }

        var metrics = ComputeMetrics();

        // Layout: Three columns
        var colWidth = info.Width / 3f;

        DrawEigenAnalysis(canvas, metrics, 0, colWidth, info.Height);
        DrawTrajectoryAnalysis(canvas, metrics, colWidth, colWidth, info.Height);
        DrawOverallVerdict(canvas, metrics, colWidth * 2, colWidth, info.Height);
    }

    private void DrawNoDataMessage(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 14);
        using var paint = new SKPaint
        {
            Color = TextColor.WithAlpha(100),
            IsAntialias = true
        };
        canvas.DrawText("Load both runs to see analytics", info.Width / 2f, info.Height / 2f, SKTextAlign.Center, font, paint);
    }

    private void DrawEigenAnalysis(SKCanvas canvas, ComparisonMetricsData metrics, float x, float width, float height)
    {
        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 14);
        using var titlePaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        using var textFont = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(200),
            IsAntialias = true
        };

        using var valueFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 20);
        using var valuePaint = new SKPaint
        {
            IsAntialias = true
        };

        var y = 25f;
        canvas.DrawText("Eigenvalue Analysis", x + 15, y, SKTextAlign.Left, titleFont, titlePaint);

        y += 30;

        // Left λ₁
        textPaint.Color = LeftColor;
        canvas.DrawText("Path A λ₁:", x + 15, y, SKTextAlign.Left, textFont, textPaint);
        valuePaint.Color = LeftColor;
        canvas.DrawText($"{metrics.LeftFirstFactor:P0}", x + 15, y + 22, SKTextAlign.Left, valueFont, valuePaint);

        // Right λ₁
        y += 55;
        textPaint.Color = RightColor;
        canvas.DrawText("Path B λ₁:", x + 15, y, SKTextAlign.Left, textFont, textPaint);
        valuePaint.Color = RightColor;
        canvas.DrawText($"{metrics.RightFirstFactor:P0}", x + 15, y + 22, SKTextAlign.Left, valueFont, valuePaint);

        // Delta
        y += 55;
        var deltaColor = metrics.FirstFactorDelta > 0.1 ? LeftColor
            : metrics.FirstFactorDelta < -0.1 ? RightColor : NeutralColor;
        textPaint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Δ λ₁:", x + 15, y, SKTextAlign.Left, textFont, textPaint);
        valuePaint.Color = deltaColor;
        var sign = metrics.FirstFactorDelta >= 0 ? "+" : "";
        canvas.DrawText($"{sign}{metrics.FirstFactorDelta:P0}", x + 15, y + 22, SKTextAlign.Left, valueFont, valuePaint);

        // Interpretation
        y += 50;
        textPaint.Color = TextColor.WithAlpha(150);
        using var smallTextFont = new SKFont(SKTypeface.Default, 10);
        var interpretation = metrics.RightFirstFactor > 0.7
            ? "B shows shared evaluative axis"
            : metrics.RightFirstFactor > metrics.LeftFirstFactor + 0.1
                ? "B shows more unification"
                : "Similar latent structure";
        canvas.DrawText(interpretation, x + 15, y, SKTextAlign.Left, smallTextFont, textPaint);
    }

    private void DrawTrajectoryAnalysis(SKCanvas canvas, ComparisonMetricsData metrics, float x, float width, float height)
    {
        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 14);
        using var titlePaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        using var textPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(200),
            IsAntialias = true
        };

        var y = 25f;
        canvas.DrawText("Trajectory Metrics", x + 15, y, SKTextAlign.Left, titleFont, titlePaint);

        y += 30;

        // Effective dimensions
        DrawComparisonBar(canvas, "Eff.Dim", metrics.LeftEffDim, metrics.RightEffDim,
            x + 15, y, width - 30, true);

        y += 50;

        // Curvature
        DrawComparisonBar(canvas, "Curvature", metrics.LeftCurvature, metrics.RightCurvature,
            x + 15, y, width - 30, true);

        y += 50;

        // Failure count
        DrawComparisonBar(canvas, "Failures", metrics.LeftFailures, metrics.RightFailures,
            x + 15, y, width - 30, true);

        y += 50;

        // Path length (as proxy for stability)
        textPaint.Color = TextColor.WithAlpha(150);
        using var smallTextFont = new SKFont(SKTypeface.Default, 10);
        var stabilityInterpretation = metrics.RightCurvature < metrics.LeftCurvature
            ? "B shows smoother trajectory"
            : metrics.RightCurvature > metrics.LeftCurvature
                ? "A shows smoother trajectory"
                : "Similar trajectory smoothness";
        canvas.DrawText(stabilityInterpretation, x + 15, y, SKTextAlign.Left, smallTextFont, textPaint);
    }

    private void DrawComparisonBar(SKCanvas canvas, string label, double leftValue, double rightValue,
        float x, float y, float width, bool lowerIsBetter)
    {
        using var labelFont = new SKFont(SKTypeface.Default, 10);
        using var labelPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(180),
            IsAntialias = true
        };

        using var valueFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 12);
        using var valuePaint = new SKPaint
        {
            IsAntialias = true
        };

        using var barPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Label
        canvas.DrawText(label, x, y, SKTextAlign.Left, labelFont, labelPaint);

        // Values
        var barY = y + 12;
        var barHeight = 16f;
        var halfWidth = (width - 10) / 2;

        // Left bar
        var maxVal = Math.Max(Math.Max(leftValue, rightValue), 0.001);
        var leftBarWidth = (float)(leftValue / maxVal * halfWidth * 0.8);
        barPaint.Color = LeftColor;
        canvas.DrawRoundRect(x, barY, leftBarWidth, barHeight, 3, 3, barPaint);
        valuePaint.Color = LeftColor;
        canvas.DrawText($"{leftValue:F2}", x + leftBarWidth + 5, barY + 12, SKTextAlign.Left, valueFont, valuePaint);

        // Right bar
        var rightBarWidth = (float)(rightValue / maxVal * halfWidth * 0.8);
        barPaint.Color = RightColor;
        canvas.DrawRoundRect(x + halfWidth + 10, barY, rightBarWidth, barHeight, 3, 3, barPaint);
        valuePaint.Color = RightColor;
        canvas.DrawText($"{rightValue:F2}", x + halfWidth + 10 + rightBarWidth + 5, barY + 12, SKTextAlign.Left, valueFont, valuePaint);
    }

    private void DrawOverallVerdict(SKCanvas canvas, ComparisonMetricsData metrics, float x, float width, float height)
    {
        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 14);
        using var titlePaint = new SKPaint
        {
            Color = TextColor,
            IsAntialias = true
        };

        var y = 25f;
        canvas.DrawText("Overall Assessment", x + 15, y, SKTextAlign.Left, titleFont, titlePaint);

        y += 30;

        // Compute verdict
        var verdict = ComputeVerdict(metrics);

        using var verdictFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 16);
        using var verdictPaint = new SKPaint
        {
            Color = verdict.Color,
            IsAntialias = true
        };

        canvas.DrawText(verdict.Title, x + 15, y, SKTextAlign.Left, verdictFont, verdictPaint);

        y += 25;

        using var descFont = new SKFont(SKTypeface.Default, 11);
        using var descPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(180),
            IsAntialias = true
        };

        // Word wrap the description
        var words = verdict.Description.Split(' ');
        var line = "";
        var lineHeight = 15f;

        foreach (var word in words)
        {
            var testLine = line.Length == 0 ? word : $"{line} {word}";
            var testWidth = descFont.MeasureText(testLine, descPaint);

            if (testWidth > width - 30)
            {
                canvas.DrawText(line, x + 15, y, SKTextAlign.Left, descFont, descPaint);
                y += lineHeight;
                line = word;
            }
            else
            {
                line = testLine;
            }
        }

        if (line.Length > 0)
        {
            canvas.DrawText(line, x + 15, y, SKTextAlign.Left, descFont, descPaint);
        }

        // Draw confidence indicator
        y += 40;
        var confidence = ComputeConfidence(metrics);
        DrawConfidenceMeter(canvas, x + 15, y, width - 30, confidence);
    }

    private void DrawConfidenceMeter(SKCanvas canvas, float x, float y, float width, double confidence)
    {
        using var labelFont = new SKFont(SKTypeface.Default, 10);
        using var labelPaint = new SKPaint
        {
            Color = TextColor.WithAlpha(150),
            IsAntialias = true
        };

        canvas.DrawText("Confidence", x, y, SKTextAlign.Left, labelFont, labelPaint);

        y += 12;

        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            Style = SKPaintStyle.Fill
        };

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        var barWidth = width * 0.7f;
        canvas.DrawRoundRect(x, y, barWidth, 8, 4, 4, bgPaint);

        fillPaint.Color = confidence > 0.7 ? LeftColor : confidence > 0.4 ? NeutralColor : RightColor;
        canvas.DrawRoundRect(x, y, (float)(barWidth * confidence), 8, 4, 4, fillPaint);

        labelPaint.Color = TextColor;
        canvas.DrawText($"{confidence:P0}", x + barWidth + 10, y + 7, SKTextAlign.Left, labelFont, labelPaint);
    }

    private ComparisonMetricsData ComputeMetrics()
    {
        var leftSteps = LeftRun!.Trajectory.Timesteps;
        var rightSteps = RightRun!.Trajectory.Timesteps;
        var leftEigen = LeftRun.Geometry.Eigenvalues;
        var rightEigen = RightRun.Geometry.Eigenvalues;

        var leftIdx = leftSteps.Count > 0 ? (int)(CurrentTime * (leftSteps.Count - 1)) : 0;
        var rightIdx = rightSteps.Count > 0 ? (int)(CurrentTime * (rightSteps.Count - 1)) : 0;
        var leftEigenIdx = leftEigen.Count > 0 ? (int)(CurrentTime * (leftEigen.Count - 1)) : 0;
        var rightEigenIdx = rightEigen.Count > 0 ? (int)(CurrentTime * (rightEigen.Count - 1)) : 0;

        leftIdx = Math.Clamp(leftIdx, 0, Math.Max(0, leftSteps.Count - 1));
        rightIdx = Math.Clamp(rightIdx, 0, Math.Max(0, rightSteps.Count - 1));
        leftEigenIdx = Math.Clamp(leftEigenIdx, 0, Math.Max(0, leftEigen.Count - 1));
        rightEigenIdx = Math.Clamp(rightEigenIdx, 0, Math.Max(0, rightEigen.Count - 1));

        var leftEigenValues = leftEigenIdx < leftEigen.Count ? leftEigen[leftEigenIdx].Values : new List<double>();
        var rightEigenValues = rightEigenIdx < rightEigen.Count ? rightEigen[rightEigenIdx].Values : new List<double>();

        var leftTotal = leftEigenValues.Sum();
        var rightTotal = rightEigenValues.Sum();

        var leftFirstFactor = leftTotal > 0 && leftEigenValues.Count > 0 ? leftEigenValues[0] / leftTotal : 0;
        var rightFirstFactor = rightTotal > 0 && rightEigenValues.Count > 0 ? rightEigenValues[0] / rightTotal : 0;

        var leftCurrent = leftIdx < leftSteps.Count ? leftSteps[leftIdx] : null;
        var rightCurrent = rightIdx < rightSteps.Count ? rightSteps[rightIdx] : null;

        return new ComparisonMetricsData
        {
            LeftFirstFactor = leftFirstFactor,
            RightFirstFactor = rightFirstFactor,
            FirstFactorDelta = rightFirstFactor - leftFirstFactor,
            LeftEffDim = leftCurrent?.EffectiveDim ?? 0,
            RightEffDim = rightCurrent?.EffectiveDim ?? 0,
            LeftCurvature = leftCurrent?.Curvature ?? 0,
            RightCurvature = rightCurrent?.Curvature ?? 0,
            LeftFailures = LeftRun.Failures.Count,
            RightFailures = RightRun.Failures.Count
        };
    }

    private (string Title, string Description, SKColor Color) ComputeVerdict(ComparisonMetricsData metrics)
    {
        // Strong evidence for shared latent structure in Path B
        if (metrics.RightFirstFactor > 0.8 && metrics.FirstFactorDelta > 0.2)
        {
            return ("Path B: Strong Unification",
                "Correlated professors successfully created shared evaluative axis. Holdout transfer should succeed.",
                LeftColor);
        }

        // Moderate evidence
        if (metrics.RightFirstFactor > 0.5 && metrics.FirstFactorDelta > 0.1)
        {
            return ("Path B: Partial Unification",
                "Some shared structure detected. Transfer may partially succeed depending on professor overlap.",
                NeutralColor);
        }

        // Both show similar structure
        if (Math.Abs(metrics.FirstFactorDelta) < 0.1)
        {
            return ("Similar Structure",
                "Both paths show comparable latent structure. Difference may be due to noise rather than professor design.",
                TextColor.WithAlpha(150));
        }

        // Path A shows more unification (unexpected)
        if (metrics.LeftFirstFactor > metrics.RightFirstFactor + 0.1)
        {
            return ("Unexpected: Path A Higher",
                "Orthogonal professors showing more unification than correlated. Check professor design or sample variance.",
                RightColor);
        }

        return ("Inconclusive",
            "Insufficient difference to draw conclusions. Consider longer training or more diverse samples.",
            TextColor.WithAlpha(100));
    }

    private double ComputeConfidence(ComparisonMetricsData metrics)
    {
        // Confidence based on:
        // - Magnitude of difference
        // - Consistency of metrics
        // - Sample size (approximated by step count)

        var eigenConfidence = Math.Min(1.0, Math.Abs(metrics.FirstFactorDelta) * 3);
        var dimConfidence = 1.0 - Math.Min(1.0, Math.Abs(metrics.LeftEffDim - metrics.RightEffDim) * 0.5);
        var failureConfidence = metrics.RightFailures <= metrics.LeftFailures ? 1.0 : 0.7;

        return (eigenConfidence + dimConfidence + failureConfidence) / 3;
    }

    private record ComparisonMetricsData
    {
        public double LeftFirstFactor { get; init; }
        public double RightFirstFactor { get; init; }
        public double FirstFactorDelta { get; init; }
        public double LeftEffDim { get; init; }
        public double RightEffDim { get; init; }
        public double LeftCurvature { get; init; }
        public double RightCurvature { get; init; }
        public int LeftFailures { get; init; }
        public int RightFailures { get; init; }
    }
}
