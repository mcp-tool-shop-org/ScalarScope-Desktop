using ScalarScope.Models;
using ScalarScope.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Overlay component for rendering interpretive annotations on trajectories.
/// Provides phase labels, curvature warnings, eigenvalue insights, and failure markers.
/// Supports density levels: Minimal, Standard, Full.
/// </summary>
public class AnnotationOverlay : SKCanvasView
{
    public static readonly BindableProperty RunProperty =
        BindableProperty.Create(nameof(Run), typeof(GeometryRun), typeof(AnnotationOverlay),
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(AnnotationOverlay), 0.0,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty ShowPhasesProperty =
        BindableProperty.Create(nameof(ShowPhases), typeof(bool), typeof(AnnotationOverlay), true,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty ShowCurvatureWarningsProperty =
        BindableProperty.Create(nameof(ShowCurvatureWarnings), typeof(bool), typeof(AnnotationOverlay), true,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty ShowEigenInsightsProperty =
        BindableProperty.Create(nameof(ShowEigenInsights), typeof(bool), typeof(AnnotationOverlay), true,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty ShowFailureMarkersProperty =
        BindableProperty.Create(nameof(ShowFailureMarkers), typeof(bool), typeof(AnnotationOverlay), true,
            propertyChanged: OnPropertyChangedInvalidate);

    public static readonly BindableProperty ScaleProperty =
        BindableProperty.Create(nameof(Scale), typeof(float), typeof(AnnotationOverlay), 100f);

    public static readonly BindableProperty CenterProperty =
        BindableProperty.Create(nameof(Center), typeof(SKPoint), typeof(AnnotationOverlay));

    public static readonly BindableProperty DensityProperty =
        BindableProperty.Create(nameof(Density), typeof(AnnotationDensity), typeof(AnnotationOverlay),
            AnnotationDensity.Standard, propertyChanged: OnPropertyChangedInvalidate);

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

    public bool ShowPhases
    {
        get => (bool)GetValue(ShowPhasesProperty);
        set => SetValue(ShowPhasesProperty, value);
    }

    public bool ShowCurvatureWarnings
    {
        get => (bool)GetValue(ShowCurvatureWarningsProperty);
        set => SetValue(ShowCurvatureWarningsProperty, value);
    }

    public bool ShowEigenInsights
    {
        get => (bool)GetValue(ShowEigenInsightsProperty);
        set => SetValue(ShowEigenInsightsProperty, value);
    }

    public bool ShowFailureMarkers
    {
        get => (bool)GetValue(ShowFailureMarkersProperty);
        set => SetValue(ShowFailureMarkersProperty, value);
    }

    public new float Scale
    {
        get => (float)GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public SKPoint Center
    {
        get => (SKPoint)GetValue(CenterProperty);
        set => SetValue(CenterProperty, value);
    }

    public AnnotationDensity Density
    {
        get => (AnnotationDensity)GetValue(DensityProperty);
        set => SetValue(DensityProperty, value);
    }

    // Density-based limits
    private int MaxPhaseAnnotations => Density switch
    {
        AnnotationDensity.Minimal => 2,
        AnnotationDensity.Standard => 5,
        AnnotationDensity.Full => 10,
        _ => 5
    };

    private float CurvatureThreshold => Density switch
    {
        AnnotationDensity.Minimal => 0.3f,
        AnnotationDensity.Standard => 0.15f,
        AnnotationDensity.Full => 0.1f,
        _ => 0.15f
    };

    private bool ShowDetailedCurvatureLabels => Density != AnnotationDensity.Minimal;

    // Annotation colors
    private static readonly SKColor PhaseColor = SKColor.Parse("#a29bfe");
    private static readonly SKColor CurvatureWarningColor = SKColor.Parse("#ff9f43");
    private static readonly SKColor EigenInsightColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor FailureColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor TextBackgroundColor = SKColor.Parse("#1a1a2e").WithAlpha(200);

    public AnnotationOverlay()
    {
        PaintSurface += OnPaintSurface;
        InputTransparent = true; // Allow clicks to pass through
    }

    private static void OnPropertyChangedInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is AnnotationOverlay overlay)
            overlay.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (Run == null) return;

        var info = e.Info;
        var center = Center == default ? new SKPoint(info.Width / 2f, info.Height / 2f) : Center;
        var scale = Scale > 0 ? Scale : Math.Min(info.Width, info.Height) / 4f;

        if (ShowPhases)
            DrawPhaseAnnotations(canvas, center, scale);

        if (ShowCurvatureWarnings)
            DrawCurvatureWarnings(canvas, center, scale);

        if (ShowEigenInsights)
            DrawEigenInsights(canvas, info);

        if (ShowFailureMarkers)
            DrawFailureMarkers(canvas, center, scale);
    }

    private void DrawPhaseAnnotations(SKCanvas canvas, SKPoint center, float scale)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count == 0) return;

        var currentIdx = (int)(CurrentTime * (steps.Count - 1));
        currentIdx = Math.Clamp(currentIdx, 0, steps.Count - 1);

        // Detect phase transitions (sudden changes in effective dimensionality)
        var phases = DetectPhases(steps, currentIdx);

        using var paint = new SKPaint
        {
            Color = PhaseColor,
            TextSize = 11,
            IsAntialias = true
        };

        using var bgPaint = new SKPaint
        {
            Color = TextBackgroundColor,
            Style = SKPaintStyle.Fill
        };

        foreach (var phase in phases)
        {
            if (phase.StepIndex > currentIdx) continue;

            var step = steps[phase.StepIndex];
            if (step.State2D.Count < 2) continue;

            var pos = ToScreen(step.State2D, center, scale);

            // Draw phase marker
            var label = phase.Label;
            var textWidth = paint.MeasureText(label);
            var labelRect = new SKRect(
                pos.X + 10, pos.Y - 15,
                pos.X + 20 + textWidth, pos.Y + 2);

            canvas.DrawRoundRect(labelRect, 3, 3, bgPaint);
            canvas.DrawText(label, pos.X + 15, pos.Y - 2, paint);

            // Draw connecting line
            paint.StrokeWidth = 1;
            canvas.DrawLine(pos.X, pos.Y, pos.X + 10, pos.Y - 7, paint);
        }
    }

    private void DrawCurvatureWarnings(SKCanvas canvas, SKPoint center, float scale)
    {
        var steps = Run!.Trajectory.Timesteps;
        if (steps.Count == 0) return;

        var currentIdx = (int)(CurrentTime * (steps.Count - 1));
        currentIdx = Math.Clamp(currentIdx, 0, steps.Count - 1);

        using var paint = new SKPaint
        {
            Color = CurvatureWarningColor,
            TextSize = 10,
            IsAntialias = true
        };

        using var circlePaint = new SKPaint
        {
            Color = CurvatureWarningColor.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        // Find high-curvature points up to current time
        // Use density-aware threshold and avoid center overlap
        var drawnPositions = new List<SKPoint>();
        var centerZone = new SKRect(center.X - scale * 0.3f, center.Y - scale * 0.3f,
                                     center.X + scale * 0.3f, center.Y + scale * 0.3f);

        for (int i = 0; i <= currentIdx && i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.Curvature > CurvatureThreshold && step.State2D.Count >= 2)
            {
                var pos = ToScreen(step.State2D, center, scale);

                // Skip if too close to center (protect trajectory visibility)
                if (Density == AnnotationDensity.Minimal && centerZone.Contains(pos))
                    continue;

                // Skip if too close to existing annotation
                if (drawnPositions.Any(p => Math.Abs(p.X - pos.X) < 30 && Math.Abs(p.Y - pos.Y) < 30))
                    continue;

                drawnPositions.Add(pos);

                // Draw warning circle with edge fade for minimal density
                var radius = Math.Min(20, (float)(step.Curvature * 50));
                var alpha = Density == AnnotationDensity.Minimal ? (byte)60 : (byte)100;
                circlePaint.Color = CurvatureWarningColor.WithAlpha(alpha);
                canvas.DrawCircle(pos, radius, circlePaint);

                // Draw curvature icon and value (only in Standard/Full)
                if (ShowDetailedCurvatureLabels && step.Curvature > 0.25)
                {
                    canvas.DrawText($"⚠ {step.Curvature:F2}", pos.X + radius + 5, pos.Y + 4, paint);
                }
            }
        }
    }

    private void DrawEigenInsights(SKCanvas canvas, SKImageInfo info)
    {
        var eigenvalues = Run!.Geometry.Eigenvalues;
        if (eigenvalues.Count == 0) return;

        var eigenIdx = (int)(CurrentTime * (eigenvalues.Count - 1));
        eigenIdx = Math.Clamp(eigenIdx, 0, eigenvalues.Count - 1);
        var eigen = eigenvalues[eigenIdx];

        if (eigen.Values.Count == 0) return;

        var total = eigen.Values.Sum();
        if (total <= 0) return;

        var firstFactor = eigen.Values[0] / total;
        var secondFactor = eigen.Values.Count > 1 ? eigen.Values[1] / total : 0;

        using var paint = new SKPaint
        {
            TextSize = 12,
            IsAntialias = true
        };

        using var bgPaint = new SKPaint
        {
            Color = TextBackgroundColor,
            Style = SKPaintStyle.Fill
        };

        // Position in bottom-right corner
        var x = info.Width - 200;
        var y = info.Height - 80;

        // Draw insight box
        var boxRect = new SKRect(x - 10, y - 25, info.Width - 10, info.Height - 10);
        canvas.DrawRoundRect(boxRect, 5, 5, bgPaint);

        // Determine insight text based on eigenvalue structure
        string insight;
        SKColor insightColor;

        if (firstFactor > 0.8)
        {
            insight = "Strong shared axis detected";
            insightColor = SKColor.Parse("#4ecdc4");
        }
        else if (firstFactor > 0.5)
        {
            insight = "Moderate latent unification";
            insightColor = SKColor.Parse("#ffd93d");
        }
        else if (firstFactor > 0.35 && secondFactor > 0.25)
        {
            insight = "Two-factor structure";
            insightColor = SKColor.Parse("#a29bfe");
        }
        else
        {
            insight = "Orthogonal evaluator space";
            insightColor = SKColor.Parse("#ff6b6b");
        }

        paint.Color = insightColor;
        canvas.DrawText(insight, x, y, paint);

        paint.Color = SKColors.White.WithAlpha(180);
        paint.TextSize = 10;
        canvas.DrawText($"λ₁: {firstFactor:P0}  λ₂: {secondFactor:P0}", x, y + 18, paint);

        // Calculate effective dimensionality
        var effDim = CalculateEffectiveDim(eigen.Values);
        canvas.DrawText($"Eff.Dim: {effDim:F2} / {eigen.Values.Count}", x, y + 33, paint);
    }

    private void DrawFailureMarkers(SKCanvas canvas, SKPoint center, float scale)
    {
        var failures = Run!.Failures;
        if (failures.Count == 0) return;

        using var paint = new SKPaint
        {
            Color = FailureColor,
            TextSize = 10,
            IsAntialias = true
        };

        using var markerPaint = new SKPaint
        {
            Color = FailureColor.WithAlpha(150),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true,
            PathEffect = SKPathEffect.CreateDash([3, 3], 0)
        };

        using var bgPaint = new SKPaint
        {
            Color = TextBackgroundColor,
            Style = SKPaintStyle.Fill
        };

        var steps = Run.Trajectory.Timesteps;
        var currentIdx = (int)(CurrentTime * (steps.Count - 1));

        foreach (var failure in failures)
        {
            // Convert failure time (0-1) to step index
            var failureIdx = (int)(failure.T * (steps.Count - 1));

            if (failureIdx > currentIdx || failureIdx >= steps.Count) continue;

            var step = steps[failureIdx];
            if (step.State2D.Count < 2) continue;

            var pos = ToScreen(step.State2D, center, scale);

            // Draw failure marker (X)
            var size = 8f;
            canvas.DrawLine(pos.X - size, pos.Y - size, pos.X + size, pos.Y + size, markerPaint);
            canvas.DrawLine(pos.X + size, pos.Y - size, pos.X - size, pos.Y + size, markerPaint);

            // Draw failure label
            var label = TruncateLabel(failure.Category, 20);
            var textWidth = paint.MeasureText(label);
            var labelRect = new SKRect(
                pos.X + 12, pos.Y - 8,
                pos.X + 22 + textWidth, pos.Y + 8);

            canvas.DrawRoundRect(labelRect, 2, 2, bgPaint);
            canvas.DrawText(label, pos.X + 17, pos.Y + 4, paint);
        }
    }

    private List<PhaseAnnotation> DetectPhases(List<TrajectoryTimestep> steps, int maxIdx)
    {
        var phases = new List<PhaseAnnotation>();

        if (steps.Count < 10) return phases;

        // Mark initial phase
        phases.Add(new PhaseAnnotation
        {
            StepIndex = 0,
            Label = "Initial State"
        });

        // Detect significant changes in effective dimensionality
        double prevEffDim = steps[0].EffectiveDim;
        double accumulatedChange = 0;

        for (int i = 5; i < steps.Count && i <= maxIdx; i += 5)
        {
            var step = steps[i];
            var dimChange = Math.Abs(step.EffectiveDim - prevEffDim);
            accumulatedChange += dimChange;

            // Detect sudden changes
            if (dimChange > 0.3)
            {
                var direction = step.EffectiveDim > prevEffDim ? "expanding" : "collapsing";
                phases.Add(new PhaseAnnotation
                {
                    StepIndex = i,
                    Label = $"Dim {direction}"
                });
            }

            // Detect high curvature regions
            if (step.Curvature > 0.2)
            {
                phases.Add(new PhaseAnnotation
                {
                    StepIndex = i,
                    Label = "Phase transition"
                });
            }

            prevEffDim = step.EffectiveDim;
        }

        // Remove duplicates (keep most significant within 10-step windows)
        // Limit based on density setting
        return phases
            .GroupBy(p => p.StepIndex / 10)
            .Select(g => g.First())
            .Take(MaxPhaseAnnotations)
            .ToList();
    }

    private static double CalculateEffectiveDim(IList<double> eigenvalues)
    {
        if (eigenvalues.Count == 0) return 0;

        var total = eigenvalues.Sum();
        if (total <= 0) return 0;

        var entropy = 0.0;
        foreach (var ev in eigenvalues)
        {
            if (ev > 0)
            {
                var p = ev / total;
                entropy -= p * Math.Log(p);
            }
        }

        return Math.Exp(entropy);
    }

    private static string TruncateLabel(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    private static SKPoint ToScreen(IList<double> state, SKPoint center, float scale)
    {
        if (state.Count < 2) return center;
        return new SKPoint(
            center.X + (float)state[0] * scale,
            center.Y - (float)state[1] * scale
        );
    }

    private record PhaseAnnotation
    {
        public int StepIndex { get; init; }
        public string Label { get; init; } = "";
    }
}
