using ScalarScope.Models;
using ScalarScope.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Panel for displaying interactive probing information.
/// Shows time travel state, gradient inspector, and what-if projections.
/// Phase 3.3 - Interactive Probing
/// </summary>
public class ProbingInspectorPanel : SKCanvasView
{
    #region Bindable Properties

    public static readonly BindableProperty RunProperty =
        BindableProperty.Create(nameof(Run), typeof(GeometryRun), typeof(ProbingInspectorPanel),
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty SelectedIndexProperty =
        BindableProperty.Create(nameof(SelectedIndex), typeof(int?), typeof(ProbingInspectorPanel),
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ShowGradientInspectorProperty =
        BindableProperty.Create(nameof(ShowGradientInspector), typeof(bool), typeof(ProbingInspectorPanel), true,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty WhatIfScenarioProperty =
        BindableProperty.Create(nameof(WhatIfScenario), typeof(WhatIfScenario), typeof(ProbingInspectorPanel),
            propertyChanged: OnInvalidate);

    public GeometryRun? Run
    {
        get => (GeometryRun?)GetValue(RunProperty);
        set => SetValue(RunProperty, value);
    }

    public int? SelectedIndex
    {
        get => (int?)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public bool ShowGradientInspector
    {
        get => (bool)GetValue(ShowGradientInspectorProperty);
        set => SetValue(ShowGradientInspectorProperty, value);
    }

    public WhatIfScenario? WhatIfScenario
    {
        get => (WhatIfScenario?)GetValue(WhatIfScenarioProperty);
        set => SetValue(WhatIfScenarioProperty, value);
    }

    #endregion

    private static readonly SKColor BackgroundColor = SKColor.Parse("#16213e");
    private static readonly SKColor TextColor = SKColors.White;
    private static readonly SKColor AccentColor = SKColor.Parse("#00d2d3");
    private static readonly SKColor WarningColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor GradientActualColor = SKColor.Parse("#4ecdc4");
    private static readonly SKColor GradientIntendedColor = SKColor.Parse("#ffd93d");

    private readonly InteractiveProbingService _probingService = new();

    public ProbingInspectorPanel()
    {
        PaintSurface += OnPaintSurface;
    }

    private static void OnInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ProbingInspectorPanel panel)
            panel.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        if (Run == null || SelectedIndex == null)
        {
            DrawPlaceholder(canvas, info);
            return;
        }

        var state = _probingService.GetStateAt(Run, SelectedIndex.Value);
        if (state == null)
        {
            DrawPlaceholder(canvas, info);
            return;
        }

        // Draw sections
        var y = 15f;
        var padding = 15f;

        y = DrawTimeTravelSection(canvas, info, state, y, padding);
        
        if (ShowGradientInspector)
        {
            var gradient = _probingService.InspectGradient(Run, SelectedIndex.Value);
            if (gradient != null)
            {
                y = DrawGradientSection(canvas, info, gradient, y + 10, padding);
            }
        }

        if (WhatIfScenario != null)
        {
            var projection = _probingService.ProjectHypothetical(Run, SelectedIndex.Value, WhatIfScenario);
            if (projection.IsValid)
            {
                DrawWhatIfSection(canvas, info, projection, y + 10, padding);
            }
        }
    }

    private void DrawPlaceholder(SKCanvas canvas, SKImageInfo info)
    {
        using var font = new SKFont(SKTypeface.Default, 12);
        using var paint = new SKPaint
        {
            Color = SKColors.Gray,
            IsAntialias = true
        };

        canvas.DrawText("Click a trajectory point to inspect", 
            info.Width / 2f, info.Height / 2f, 
            SKTextAlign.Center, font, paint);
    }

    private float DrawTimeTravelSection(SKCanvas canvas, SKImageInfo info, TimeTravelState state, float y, float padding)
    {
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
        using var labelFont = new SKFont(SKTypeface.Default, 10);
        using var valueFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 11);
        using var paint = new SKPaint { IsAntialias = true };

        // Section header
        paint.Color = AccentColor;
        canvas.DrawText("⏱ TIME TRAVEL", padding, y, SKTextAlign.Left, headerFont, paint);
        y += 20;

        // Time indicator
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText($"t = {state.Time:F4}", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = SKColors.Gray;
        canvas.DrawText($"(step {state.TimestepIndex})", padding + 80, y, SKTextAlign.Left, labelFont, paint);
        y += 18;

        // Position
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Position:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText($"({state.Position.X:F4}, {state.Position.Y:F4})", padding + 60, y, SKTextAlign.Left, valueFont, paint);
        y += 16;

        // Velocity
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Velocity:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText($"({state.Velocity.Vx:F4}, {state.Velocity.Vy:F4})", padding + 60, y, SKTextAlign.Left, valueFont, paint);
        paint.Color = SKColors.Gray;
        canvas.DrawText($"speed: {state.Speed:F4}", padding + 200, y, SKTextAlign.Left, labelFont, paint);
        y += 16;

        // Curvature
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Curvature:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = state.IsAtBifurcation ? WarningColor : TextColor;
        canvas.DrawText($"{state.Curvature:F4}", padding + 60, y, SKTextAlign.Left, valueFont, paint);
        if (state.IsAtBifurcation)
        {
            paint.Color = WarningColor;
            canvas.DrawText("⚠ Phase transition", padding + 120, y, SKTextAlign.Left, labelFont, paint);
        }
        y += 18;

        // Eigenvalues (if available)
        if (state.Lambda1 != null)
        {
            paint.Color = TextColor.WithAlpha(150);
            canvas.DrawText("Eigenvalues:", padding, y, SKTextAlign.Left, labelFont, paint);
            paint.Color = SKColor.Parse("#a29bfe");
            canvas.DrawText($"λ₁={state.Lambda1:F4}", padding + 75, y, SKTextAlign.Left, valueFont, paint);
            paint.Color = SKColor.Parse("#fd79a8");
            canvas.DrawText($"λ₂={state.Lambda2:F4}", padding + 150, y, SKTextAlign.Left, valueFont, paint);
            y += 16;
        }

        // Loss/Accuracy (if available)
        if (state.Loss != null)
        {
            paint.Color = TextColor.WithAlpha(150);
            canvas.DrawText("Loss:", padding, y, SKTextAlign.Left, labelFont, paint);
            paint.Color = TextColor;
            canvas.DrawText($"{state.Loss:F6}", padding + 40, y, SKTextAlign.Left, valueFont, paint);

            if (state.Accuracy != null)
            {
                paint.Color = TextColor.WithAlpha(150);
                canvas.DrawText("Acc:", padding + 120, y, SKTextAlign.Left, labelFont, paint);
                paint.Color = SKColor.Parse("#2ecc71");
                canvas.DrawText($"{state.Accuracy:P1}", padding + 150, y, SKTextAlign.Left, valueFont, paint);
            }
            y += 16;
        }

        return y;
    }

    private float DrawGradientSection(SKCanvas canvas, SKImageInfo info, GradientInspection gradient, float y, float padding)
    {
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
        using var labelFont = new SKFont(SKTypeface.Default, 10);
        using var valueFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 11);
        using var paint = new SKPaint { IsAntialias = true };

        // Section header
        paint.Color = GradientActualColor;
        canvas.DrawText("🧭 GRADIENT INSPECTOR", padding, y, SKTextAlign.Left, headerFont, paint);
        y += 20;

        // Actual direction
        paint.Color = GradientActualColor;
        canvas.DrawText("Actual:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText($"({gradient.ActualDirection.Dx:F4}, {gradient.ActualDirection.Dy:F4})", 
            padding + 50, y, SKTextAlign.Left, valueFont, paint);
        paint.Color = SKColors.Gray;
        canvas.DrawText($"|{gradient.ActualMagnitude:F4}|", padding + 190, y, SKTextAlign.Left, labelFont, paint);
        y += 16;

        // Intended direction
        paint.Color = GradientIntendedColor;
        canvas.DrawText("Intended:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText($"({gradient.IntendedDirection.Dx:F4}, {gradient.IntendedDirection.Dy:F4})", 
            padding + 50, y, SKTextAlign.Left, valueFont, paint);
        paint.Color = SKColors.Gray;
        canvas.DrawText($"|{gradient.IntendedMagnitude:F4}|", padding + 190, y, SKTextAlign.Left, labelFont, paint);
        y += 18;

        // Alignment
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Alignment:", padding, y, SKTextAlign.Left, labelFont, paint);

        // Color-code alignment
        var alignmentColor = gradient.Alignment switch
        {
            > 0.9 => SKColor.Parse("#2ecc71"),   // Green - well aligned
            > 0.5 => SKColor.Parse("#f1c40f"),   // Yellow - moderate
            > 0.0 => SKColor.Parse("#e67e22"),   // Orange - weak
            _ => WarningColor                     // Red - opposing
        };

        paint.Color = alignmentColor;
        canvas.DrawText($"{gradient.Alignment:F3} ({gradient.AlignmentCategory})", 
            padding + 65, y, SKTextAlign.Left, valueFont, paint);

        paint.Color = SKColors.Gray;
        canvas.DrawText($"∠{gradient.DeviationAngle:F1}°", padding + 220, y, SKTextAlign.Left, labelFont, paint);
        y += 16;

        // Draw mini direction indicator
        DrawDirectionIndicator(canvas, info.Width - 60, y - 30, gradient);

        return y;
    }

    private void DrawDirectionIndicator(SKCanvas canvas, float cx, float cy, GradientInspection gradient)
    {
        const float radius = 25f;

        // Background circle
        using var bgPaint = new SKPaint
        {
            Color = SKColor.Parse("#1a1a2e"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Border
        using var borderPaint = new SKPaint
        {
            Color = SKColors.Gray.WithAlpha(100),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, radius, borderPaint);

        // Normalize directions
        var actualLen = gradient.ActualMagnitude;
        var intendedLen = gradient.IntendedMagnitude;

        if (actualLen > 0.001)
        {
            var ax = (float)(gradient.ActualDirection.Dx / actualLen * radius * 0.8);
            var ay = (float)(-gradient.ActualDirection.Dy / actualLen * radius * 0.8); // Flip Y

            using var actualPaint = new SKPaint
            {
                Color = GradientActualColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.5f,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(cx, cy, cx + ax, cy + ay, actualPaint);
        }

        if (intendedLen > 0.001)
        {
            var ix = (float)(gradient.IntendedDirection.Dx / intendedLen * radius * 0.8);
            var iy = (float)(-gradient.IntendedDirection.Dy / intendedLen * radius * 0.8);

            using var intendedPaint = new SKPaint
            {
                Color = GradientIntendedColor,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                PathEffect = SKPathEffect.CreateDash([4, 2], 0)
            };
            canvas.DrawLine(cx, cy, cx + ix, cy + iy, intendedPaint);
        }
    }

    private void DrawWhatIfSection(SKCanvas canvas, SKImageInfo info, WhatIfProjection projection, float y, float padding)
    {
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 13);
        using var labelFont = new SKFont(SKTypeface.Default, 10);
        using var valueFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 11);
        using var paint = new SKPaint { IsAntialias = true };

        // Section header
        paint.Color = SKColor.Parse("#c56cf0");
        canvas.DrawText("🔮 WHAT-IF PROJECTION", padding, y, SKTextAlign.Left, headerFont, paint);
        y += 20;

        // Scenario name
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Scenario:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText(projection.Scenario.Name, padding + 60, y, SKTextAlign.Left, valueFont, paint);
        y += 16;

        // Final position
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Final pos:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = TextColor;
        canvas.DrawText($"({projection.FinalPosition.X:F4}, {projection.FinalPosition.Y:F4})", 
            padding + 60, y, SKTextAlign.Left, valueFont, paint);
        y += 16;

        // Displacement
        paint.Color = TextColor.WithAlpha(150);
        canvas.DrawText("Displacement:", padding, y, SKTextAlign.Left, labelFont, paint);
        paint.Color = SKColor.Parse("#c56cf0");
        canvas.DrawText($"{projection.TotalDisplacement:F4}", padding + 80, y, SKTextAlign.Left, valueFont, paint);
    }
}
