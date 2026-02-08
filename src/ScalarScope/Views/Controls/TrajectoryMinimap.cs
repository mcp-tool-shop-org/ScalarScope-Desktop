using ScalarScope.Models;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace ScalarScope.Views.Controls;

/// <summary>
/// Minimap showing small overview of trajectory with current viewport position.
/// Phase 4.1 - Fluid Navigation
/// </summary>
public class TrajectoryMinimap : SKCanvasView
{
    #region Bindable Properties

    public static readonly BindableProperty RunProperty =
        BindableProperty.Create(nameof(Run), typeof(GeometryRun), typeof(TrajectoryMinimap),
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ViewportXProperty =
        BindableProperty.Create(nameof(ViewportX), typeof(float), typeof(TrajectoryMinimap), 0f,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ViewportYProperty =
        BindableProperty.Create(nameof(ViewportY), typeof(float), typeof(TrajectoryMinimap), 0f,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty ViewportZoomProperty =
        BindableProperty.Create(nameof(ViewportZoom), typeof(float), typeof(TrajectoryMinimap), 1f,
            propertyChanged: OnInvalidate);

    public static readonly BindableProperty CurrentTimeProperty =
        BindableProperty.Create(nameof(CurrentTime), typeof(double), typeof(TrajectoryMinimap), 0.0,
            propertyChanged: OnInvalidate);

    public GeometryRun? Run
    {
        get => (GeometryRun?)GetValue(RunProperty);
        set => SetValue(RunProperty, value);
    }

    public float ViewportX
    {
        get => (float)GetValue(ViewportXProperty);
        set => SetValue(ViewportXProperty, value);
    }

    public float ViewportY
    {
        get => (float)GetValue(ViewportYProperty);
        set => SetValue(ViewportYProperty, value);
    }

    public float ViewportZoom
    {
        get => (float)GetValue(ViewportZoomProperty);
        set => SetValue(ViewportZoomProperty, value);
    }

    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<MinimapClickEventArgs>? ViewportClicked;

    #endregion

    private static readonly SKColor BackgroundColor = SKColor.Parse("#0f0f1a");
    private static readonly SKColor BorderColor = SKColor.Parse("#4a5568");
    private static readonly SKColor TrajectoryColor = SKColor.Parse("#00d9ff");
    private static readonly SKColor CurrentPositionColor = SKColor.Parse("#ff6b6b");
    private static readonly SKColor ViewportRectColor = SKColor.Parse("#ffd93d");

    // Cached bounds
    private float _dataMinX, _dataMaxX, _dataMinY, _dataMaxY;
    private bool _boundsComputed;

    public TrajectoryMinimap()
    {
        PaintSurface += OnPaintSurface;
        EnableTouchEvents = true;
        Touch += OnTouch;
    }

    private static void OnInvalidate(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is TrajectoryMinimap minimap)
        {
            if (newValue is GeometryRun)
            {
                minimap._boundsComputed = false;
            }
            minimap.InvalidateSurface();
        }
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        if (e.ActionType == SKTouchAction.Pressed || e.ActionType == SKTouchAction.Moved)
        {
            // Convert touch to data coordinates
            var info = new SKImageInfo((int)Width, (int)Height);
            var dataCoords = ScreenToData(e.Location, info);
            
            if (dataCoords != null)
            {
                ViewportClicked?.Invoke(this, new MinimapClickEventArgs
                {
                    DataX = dataCoords.Value.X,
                    DataY = dataCoords.Value.Y
                });
            }

            e.Handled = true;
        }
    }

    private void ComputeBounds()
    {
        if (_boundsComputed) return;

        var trajectory = Run?.Trajectory?.Timesteps;
        if (trajectory == null || trajectory.Count == 0)
        {
            _dataMinX = _dataMaxX = _dataMinY = _dataMaxY = 0;
            _boundsComputed = true;
            return;
        }

        _dataMinX = float.MaxValue;
        _dataMaxX = float.MinValue;
        _dataMinY = float.MaxValue;
        _dataMaxY = float.MinValue;

        foreach (var ts in trajectory)
        {
            if (ts.State2D?.Count >= 2)
            {
                _dataMinX = Math.Min(_dataMinX, (float)ts.State2D[0]);
                _dataMaxX = Math.Max(_dataMaxX, (float)ts.State2D[0]);
                _dataMinY = Math.Min(_dataMinY, (float)ts.State2D[1]);
                _dataMaxY = Math.Max(_dataMaxY, (float)ts.State2D[1]);
            }
        }

        // Add margin
        var rangeX = _dataMaxX - _dataMinX;
        var rangeY = _dataMaxY - _dataMinY;
        var margin = Math.Max(rangeX, rangeY) * 0.1f;
        _dataMinX -= margin;
        _dataMaxX += margin;
        _dataMinY -= margin;
        _dataMaxY += margin;

        _boundsComputed = true;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var info = e.Info;

        canvas.Clear(BackgroundColor);

        // Draw border
        using var borderPaint = new SKPaint
        {
            Color = BorderColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        canvas.DrawRect(0, 0, info.Width - 1, info.Height - 1, borderPaint);

        if (Run?.Trajectory?.Timesteps == null || Run.Trajectory.Timesteps.Count == 0)
        {
            return;
        }

        ComputeBounds();

        // Draw trajectory path
        DrawTrajectory(canvas, info);

        // Draw current position marker
        DrawCurrentPosition(canvas, info);

        // Draw viewport rectangle
        DrawViewportRect(canvas, info);
    }

    private void DrawTrajectory(SKCanvas canvas, SKImageInfo info)
    {
        var trajectory = Run!.Trajectory.Timesteps;
        if (trajectory.Count < 2) return;

        using var paint = new SKPaint
        {
            Color = TrajectoryColor.WithAlpha(180),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        using var path = new SKPath();
        var first = true;

        foreach (var ts in trajectory)
        {
            if (ts.State2D?.Count < 2) continue;

            var screenPt = DataToScreen(ts.State2D[0], ts.State2D[1], info);

            if (first)
            {
                path.MoveTo(screenPt);
                first = false;
            }
            else
            {
                path.LineTo(screenPt);
            }
        }

        canvas.DrawPath(path, paint);

        // Draw start point marker
        if (trajectory.Count > 0 && trajectory[0].State2D?.Count >= 2)
        {
            var startPt = DataToScreen(trajectory[0].State2D[0], trajectory[0].State2D[1], info);
            using var startPaint = new SKPaint
            {
                Color = TrajectoryColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(startPt, 3, startPaint);
        }
    }

    private void DrawCurrentPosition(SKCanvas canvas, SKImageInfo info)
    {
        var trajectory = Run!.Trajectory.Timesteps;
        if (trajectory.Count == 0) return;

        var idx = (int)(CurrentTime * (trajectory.Count - 1));
        idx = Math.Clamp(idx, 0, trajectory.Count - 1);

        var current = trajectory[idx];
        if (current.State2D?.Count < 2) return;

        var screenPt = DataToScreen(current.State2D[0], current.State2D[1], info);

        // Glow effect
        using var glowPaint = new SKPaint
        {
            Color = CurrentPositionColor.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
        };
        canvas.DrawCircle(screenPt, 6, glowPaint);

        // Core dot
        using var corePaint = new SKPaint
        {
            Color = CurrentPositionColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(screenPt, 3, corePaint);
    }

    private void DrawViewportRect(SKCanvas canvas, SKImageInfo info)
    {
        // Calculate what the main view can see based on zoom and pan
        // This is an approximation - adjust based on your main canvas size

        var viewWidth = (info.Width - 4) / ViewportZoom;
        var viewHeight = (info.Height - 4) / ViewportZoom;

        // Viewport center in data space (approximation)
        var centerX = -ViewportX / 100f; // Adjust scale factor based on main canvas
        var centerY = ViewportY / 100f;

        // Convert to minimap coordinates
        var rangeX = _dataMaxX - _dataMinX;
        var rangeY = _dataMaxY - _dataMinY;
        if (rangeX < 0.001f) rangeX = 1f;
        if (rangeY < 0.001f) rangeY = 1f;

        var scale = Math.Min((info.Width - 4) / rangeX, (info.Height - 4) / rangeY);

        // Viewport rect dimensions
        var rectWidth = viewWidth * rangeX / (info.Width - 4);
        var rectHeight = viewHeight * rangeY / (info.Height - 4);

        // Center position on minimap
        var cx = 2 + (centerX - _dataMinX) / rangeX * (info.Width - 4);
        var cy = 2 + (_dataMaxY - centerY) / rangeY * (info.Height - 4);

        // Draw viewport rectangle
        using var rectPaint = new SKPaint
        {
            Color = ViewportRectColor.WithAlpha(100),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var rectStroke = new SKPaint
        {
            Color = ViewportRectColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true
        };

        var rect = new SKRect(
            cx - rectWidth / 2,
            cy - rectHeight / 2,
            cx + rectWidth / 2,
            cy + rectHeight / 2
        );

        // Clamp rect to minimap bounds
        rect = SKRect.Intersect(rect, new SKRect(2, 2, info.Width - 2, info.Height - 2));

        canvas.DrawRect(rect, rectPaint);
        canvas.DrawRect(rect, rectStroke);
    }

    private SKPoint DataToScreen(double dataX, double dataY, SKImageInfo info)
    {
        var rangeX = _dataMaxX - _dataMinX;
        var rangeY = _dataMaxY - _dataMinY;
        if (rangeX < 0.001f) rangeX = 1f;
        if (rangeY < 0.001f) rangeY = 1f;

        var padding = 2;
        var drawWidth = info.Width - 2 * padding;
        var drawHeight = info.Height - 2 * padding;

        return new SKPoint(
            padding + (float)((dataX - _dataMinX) / rangeX * drawWidth),
            padding + (float)((_dataMaxY - dataY) / rangeY * drawHeight) // Flip Y
        );
    }

    private (float X, float Y)? ScreenToData(SKPoint screenPt, SKImageInfo info)
    {
        var rangeX = _dataMaxX - _dataMinX;
        var rangeY = _dataMaxY - _dataMinY;
        if (rangeX < 0.001f || rangeY < 0.001f) return null;

        var padding = 2;
        var drawWidth = info.Width - 2 * padding;
        var drawHeight = info.Height - 2 * padding;

        var dataX = _dataMinX + (screenPt.X - padding) / drawWidth * rangeX;
        var dataY = _dataMaxY - (screenPt.Y - padding) / drawHeight * rangeY;

        return ((float)dataX, (float)dataY);
    }
}

public class MinimapClickEventArgs : EventArgs
{
    public float DataX { get; init; }
    public float DataY { get; init; }
}
