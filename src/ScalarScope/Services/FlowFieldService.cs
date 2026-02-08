using SkiaSharp;
using System.Numerics;

namespace ScalarScope.Services;

/// <summary>
/// Flow field analysis - streamlines, attractors, and repellers.
/// Phase 1.3 - Flow Field Context
/// </summary>
public class FlowFieldService
{
    /// <summary>
    /// Compute streamlines from seed points following the gradient field.
    /// </summary>
    public List<Streamline> ComputeStreamlines(
        Func<Vector2, Vector2> gradientField,
        IEnumerable<Vector2> seedPoints,
        StreamlineSettings settings)
    {
        var streamlines = new List<Streamline>();

        foreach (var seed in seedPoints)
        {
            var forward = IntegrateStreamline(gradientField, seed, settings, forward: true);
            var backward = IntegrateStreamline(gradientField, seed, settings, forward: false);

            // Combine backward (reversed) + forward
            backward.Points.Reverse();
            backward.Points.AddRange(forward.Points.Skip(1)); // Skip duplicate seed point

            streamlines.Add(new Streamline
            {
                Seed = seed,
                Points = backward.Points,
                Length = backward.Length + forward.Length
            });
        }

        return streamlines;
    }

    private static Streamline IntegrateStreamline(
        Func<Vector2, Vector2> field,
        Vector2 start,
        StreamlineSettings settings,
        bool forward)
    {
        var points = new List<Vector2> { start };
        var current = start;
        var totalLength = 0f;
        var direction = forward ? 1f : -1f;

        for (int i = 0; i < settings.MaxSteps; i++)
        {
            var gradient = field(current);
            if (gradient.LengthSquared() < settings.MinGradientMagnitude * settings.MinGradientMagnitude)
                break; // Near singular point

            // Normalize and scale
            var step = Vector2.Normalize(gradient) * settings.StepSize * direction;
            var next = current + step;

            // Check bounds
            if (!settings.Bounds.Contains(next.X, next.Y))
                break;

            points.Add(next);
            totalLength += step.Length();
            current = next;

            if (totalLength > settings.MaxLength)
                break;
        }

        return new Streamline { Points = points, Length = totalLength };
    }

    /// <summary>
    /// Generate evenly-spaced seed points for streamlines.
    /// </summary>
    public List<Vector2> GenerateSeedGrid(RectF bounds, int rows, int cols)
    {
        var seeds = new List<Vector2>();
        var dx = bounds.Width / (cols - 1);
        var dy = bounds.Height / (rows - 1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                seeds.Add(new Vector2(bounds.X + c * dx, bounds.Y + r * dy));
            }
        }

        return seeds;
    }

    /// <summary>
    /// Detect fixed points (attractors, repellers, saddle points) in the gradient field.
    /// </summary>
    public List<FixedPoint> DetectFixedPoints(
        Func<Vector2, Vector2> gradientField,
        RectF searchBounds,
        FixedPointSettings settings)
    {
        var fixedPoints = new List<FixedPoint>();
        var gridSize = settings.SearchGridSize;
        var dx = searchBounds.Width / gridSize;
        var dy = searchBounds.Height / gridSize;

        // Search grid for sign changes (potential fixed points)
        for (int i = 0; i < gridSize - 1; i++)
        {
            for (int j = 0; j < gridSize - 1; j++)
            {
                var cell = new Vector2[]
                {
                    new(searchBounds.X + i * dx, searchBounds.Y + j * dy),
                    new(searchBounds.X + (i + 1) * dx, searchBounds.Y + j * dy),
                    new(searchBounds.X + i * dx, searchBounds.Y + (j + 1) * dy),
                    new(searchBounds.X + (i + 1) * dx, searchBounds.Y + (j + 1) * dy)
                };

                var gradients = cell.Select(p => gradientField(p)).ToArray();

                // Check for sign change in x and y components
                var hasSignChangeX = HasSignChange(gradients.Select(g => g.X));
                var hasSignChangeY = HasSignChange(gradients.Select(g => g.Y));

                if (hasSignChangeX && hasSignChangeY)
                {
                    // Refine using Newton's method
                    var center = new Vector2(
                        searchBounds.X + (i + 0.5f) * dx,
                        searchBounds.Y + (j + 0.5f) * dy);

                    var refined = RefineFixedPoint(gradientField, center, settings.MaxNewtonIterations);
                    if (refined.HasValue)
                    {
                        var fp = ClassifyFixedPoint(gradientField, refined.Value, settings.JacobianStep);
                        if (fp != null && InBounds(fp.Position, searchBounds))
                        {
                            fixedPoints.Add(fp);
                        }
                    }
                }
            }
        }

        return MergeNearbyPoints(fixedPoints, settings.MergeRadius);
    }

    private static bool HasSignChange(IEnumerable<float> values)
    {
        var arr = values.ToArray();
        return arr.Min() < 0 && arr.Max() > 0;
    }

    private static Vector2? RefineFixedPoint(
        Func<Vector2, Vector2> field,
        Vector2 initial,
        int maxIterations)
    {
        var current = initial;
        var h = 0.001f;

        for (int i = 0; i < maxIterations; i++)
        {
            var f = field(current);
            if (f.LengthSquared() < 1e-10f)
                return current; // Converged

            // Compute Jacobian numerically
            var fx = field(current + new Vector2(h, 0));
            var fy = field(current + new Vector2(0, h));

            var j00 = (fx.X - f.X) / h;
            var j01 = (fy.X - f.X) / h;
            var j10 = (fx.Y - f.Y) / h;
            var j11 = (fy.Y - f.Y) / h;

            // Newton step: x_new = x - J^(-1) * f(x)
            var det = j00 * j11 - j01 * j10;
            if (MathF.Abs(det) < 1e-10f)
                return null; // Singular Jacobian

            var dx = (j11 * f.X - j01 * f.Y) / det;
            var dy = (-j10 * f.X + j00 * f.Y) / det;

            current -= new Vector2(dx, dy);
        }

        return null; // Did not converge
    }

    private static FixedPoint? ClassifyFixedPoint(
        Func<Vector2, Vector2> field,
        Vector2 position,
        float h)
    {
        // Compute Jacobian at fixed point
        var f = field(position);
        var fx = field(position + new Vector2(h, 0));
        var fy = field(position + new Vector2(0, h));

        var j00 = (fx.X - f.X) / h;
        var j01 = (fy.X - f.X) / h;
        var j10 = (fx.Y - f.Y) / h;
        var j11 = (fy.Y - f.Y) / h;

        // Eigenvalue analysis
        var trace = j00 + j11;
        var det = j00 * j11 - j01 * j10;
        var discriminant = trace * trace - 4 * det;

        FixedPointType type;
        float stability;

        if (det < 0)
        {
            type = FixedPointType.Saddle;
            stability = 0.5f;
        }
        else if (discriminant >= 0)
        {
            var lambda1 = (trace + MathF.Sqrt(discriminant)) / 2;
            var lambda2 = (trace - MathF.Sqrt(discriminant)) / 2;

            if (lambda1 < 0 && lambda2 < 0)
            {
                type = FixedPointType.StableNode; // Attractor
                stability = 1f;
            }
            else if (lambda1 > 0 && lambda2 > 0)
            {
                type = FixedPointType.UnstableNode; // Repeller
                stability = 0f;
            }
            else
            {
                type = FixedPointType.Saddle;
                stability = 0.5f;
            }
        }
        else
        {
            // Complex eigenvalues (spiral)
            if (trace < 0)
            {
                type = FixedPointType.StableSpiral; // Spiral attractor
                stability = 1f;
            }
            else if (trace > 0)
            {
                type = FixedPointType.UnstableSpiral; // Spiral repeller
                stability = 0f;
            }
            else
            {
                type = FixedPointType.Center;
                stability = 0.5f;
            }
        }

        return new FixedPoint
        {
            Position = position,
            Type = type,
            Stability = stability,
            TraceValue = trace,
            DeterminantValue = det
        };
    }

    private static bool InBounds(Vector2 p, RectF bounds) =>
        p.X >= bounds.X && p.X <= bounds.X + bounds.Width &&
        p.Y >= bounds.Y && p.Y <= bounds.Y + bounds.Height;

    private static List<FixedPoint> MergeNearbyPoints(List<FixedPoint> points, float radius)
    {
        var merged = new List<FixedPoint>();
        var used = new bool[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            if (used[i]) continue;

            var cluster = new List<FixedPoint> { points[i] };
            used[i] = true;

            for (int j = i + 1; j < points.Count; j++)
            {
                if (used[j]) continue;

                if (Vector2.Distance(points[i].Position, points[j].Position) < radius)
                {
                    cluster.Add(points[j]);
                    used[j] = true;
                }
            }

            // Take the most stable representative
            merged.Add(cluster.OrderByDescending(p => p.Stability).First());
        }

        return merged;
    }

    /// <summary>
    /// Draw streamlines on a canvas.
    /// </summary>
    public void RenderStreamlines(
        SKCanvas canvas,
        IReadOnlyList<Streamline> streamlines,
        Func<Vector2, SKPoint> worldToScreen,
        StreamlineRenderSettings settings)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = settings.StrokeWidth,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true
        };

        foreach (var sl in streamlines)
        {
            if (sl.Points.Count < 2) continue;

            using var path = new SKPath();
            var first = worldToScreen(sl.Points[0]);
            path.MoveTo(first);

            for (int i = 1; i < sl.Points.Count; i++)
            {
                var p = worldToScreen(sl.Points[i]);
                path.LineTo(p);
            }

            // Fade based on distance from trajectory or fixed alpha
            paint.Color = settings.Color.WithAlpha(settings.Alpha);
            canvas.DrawPath(path, paint);

            // Draw arrow at midpoint
            if (settings.ShowArrows && sl.Points.Count > 2)
            {
                var midIdx = sl.Points.Count / 2;
                var mid = worldToScreen(sl.Points[midIdx]);
                var next = worldToScreen(sl.Points[midIdx + 1]);
                DrawArrowHead(canvas, mid, next, settings.ArrowSize, paint);
            }
        }
    }

    private static void DrawArrowHead(SKCanvas canvas, SKPoint from, SKPoint to, float size, SKPaint paint)
    {
        var dir = new SKPoint(to.X - from.X, to.Y - from.Y);
        var len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        if (len < 0.1f) return;

        dir = new SKPoint(dir.X / len, dir.Y / len);
        var perp = new SKPoint(-dir.Y, dir.X);

        var tip = new SKPoint(from.X + dir.X * size, from.Y + dir.Y * size);
        var left = new SKPoint(from.X - dir.X * size * 0.5f + perp.X * size * 0.5f,
                               from.Y - dir.Y * size * 0.5f + perp.Y * size * 0.5f);
        var right = new SKPoint(from.X - dir.X * size * 0.5f - perp.X * size * 0.5f,
                                from.Y - dir.Y * size * 0.5f - perp.Y * size * 0.5f);

        using var arrowPath = new SKPath();
        arrowPath.MoveTo(tip);
        arrowPath.LineTo(left);
        arrowPath.LineTo(right);
        arrowPath.Close();

        using var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = paint.Color,
            IsAntialias = true
        };
        canvas.DrawPath(arrowPath, fillPaint);
    }

    /// <summary>
    /// Draw fixed point markers on a canvas.
    /// </summary>
    public void RenderFixedPoints(
        SKCanvas canvas,
        IReadOnlyList<FixedPoint> fixedPoints,
        Func<Vector2, SKPoint> worldToScreen,
        FixedPointRenderSettings settings)
    {
        foreach (var fp in fixedPoints)
        {
            var screenPos = worldToScreen(fp.Position);
            var color = GetFixedPointColor(fp.Type, settings);

            switch (fp.Type)
            {
                case FixedPointType.StableNode:
                case FixedPointType.StableSpiral:
                    // Filled circle (attractor)
                    using (var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Fill,
                        Color = color,
                        IsAntialias = true
                    })
                    {
                        canvas.DrawCircle(screenPos, settings.Size, paint);
                    }
                    break;

                case FixedPointType.UnstableNode:
                case FixedPointType.UnstableSpiral:
                    // Hollow circle (repeller)
                    using (var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        Color = color,
                        StrokeWidth = 2,
                        IsAntialias = true
                    })
                    {
                        canvas.DrawCircle(screenPos, settings.Size, paint);
                    }
                    break;

                case FixedPointType.Saddle:
                    // X mark
                    using (var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        Color = color,
                        StrokeWidth = 2,
                        StrokeCap = SKStrokeCap.Round,
                        IsAntialias = true
                    })
                    {
                        var s = settings.Size;
                        canvas.DrawLine(screenPos.X - s, screenPos.Y - s, screenPos.X + s, screenPos.Y + s, paint);
                        canvas.DrawLine(screenPos.X + s, screenPos.Y - s, screenPos.X - s, screenPos.Y + s, paint);
                    }
                    break;

                case FixedPointType.Center:
                    // Diamond
                    using (var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Stroke,
                        Color = color,
                        StrokeWidth = 2,
                        IsAntialias = true
                    })
                    {
                        var s = settings.Size;
                        using var path = new SKPath();
                        path.MoveTo(screenPos.X, screenPos.Y - s);
                        path.LineTo(screenPos.X + s, screenPos.Y);
                        path.LineTo(screenPos.X, screenPos.Y + s);
                        path.LineTo(screenPos.X - s, screenPos.Y);
                        path.Close();
                        canvas.DrawPath(path, paint);
                    }
                    break;
            }

            // Label
            if (settings.ShowLabels)
            {
                using var textPaint = new SKPaint
                {
                    IsAntialias = true
                };
                using var font = new SKFont { Size = settings.LabelSize };
                var label = fp.Type.ToString().Replace("Stable", "S-").Replace("Unstable", "U-");
                canvas.DrawText(label, screenPos.X + settings.Size + 4, screenPos.Y + 4, font, textPaint);
            }
        }
    }

    private static SKColor GetFixedPointColor(FixedPointType type, FixedPointRenderSettings settings)
    {
        return type switch
        {
            FixedPointType.StableNode or FixedPointType.StableSpiral => settings.AttractorColor,
            FixedPointType.UnstableNode or FixedPointType.UnstableSpiral => settings.RepellerColor,
            FixedPointType.Saddle => settings.SaddleColor,
            FixedPointType.Center => settings.CenterColor,
            _ => SKColors.Gray
        };
    }
}

#region Data Models

public class Streamline
{
    public Vector2 Seed { get; set; }
    public List<Vector2> Points { get; set; } = [];
    public float Length { get; set; }
}

public class StreamlineSettings
{
    public float StepSize { get; set; } = 0.5f;
    public int MaxSteps { get; set; } = 1000;
    public float MaxLength { get; set; } = 500f;
    public float MinGradientMagnitude { get; set; } = 1e-6f;
    public RectF Bounds { get; set; } = new(-1000, -1000, 2000, 2000);
}

public class StreamlineRenderSettings
{
    public SKColor Color { get; set; } = SKColor.Parse("#2a6a9e");
    public byte Alpha { get; set; } = 100;
    public float StrokeWidth { get; set; } = 1f;
    public bool ShowArrows { get; set; } = true;
    public float ArrowSize { get; set; } = 6f;
}

public class FixedPoint
{
    public Vector2 Position { get; set; }
    public FixedPointType Type { get; set; }
    public float Stability { get; set; }
    public float TraceValue { get; set; }
    public float DeterminantValue { get; set; }
}

public enum FixedPointType
{
    StableNode,      // Attractor (all eigenvalues negative real)
    UnstableNode,    // Repeller (all eigenvalues positive real)
    StableSpiral,    // Spiral attractor (complex eigenvalues, negative real part)
    UnstableSpiral,  // Spiral repeller (complex eigenvalues, positive real part)
    Saddle,          // Saddle point (eigenvalues with opposite signs)
    Center           // Center (purely imaginary eigenvalues)
}

public class FixedPointSettings
{
    public int SearchGridSize { get; set; } = 50;
    public int MaxNewtonIterations { get; set; } = 20;
    public float JacobianStep { get; set; } = 0.001f;
    public float MergeRadius { get; set; } = 5f;
}

public class FixedPointRenderSettings
{
    public float Size { get; set; } = 8f;
    public SKColor AttractorColor { get; set; } = SKColor.Parse("#4ecdc4");
    public SKColor RepellerColor { get; set; } = SKColor.Parse("#ff6b6b");
    public SKColor SaddleColor { get; set; } = SKColor.Parse("#ffd93d");
    public SKColor CenterColor { get; set; } = SKColor.Parse("#a29bfe");
    public bool ShowLabels { get; set; }
    public float LabelSize { get; set; } = 10f;
}

public struct RectF(float x, float y, float width, float height)
{
    public float X = x;
    public float Y = y;
    public float Width = width;
    public float Height = height;

    public readonly bool Contains(float px, float py) =>
        px >= X && px <= X + Width && py >= Y && py <= Y + Height;
}

#endregion
