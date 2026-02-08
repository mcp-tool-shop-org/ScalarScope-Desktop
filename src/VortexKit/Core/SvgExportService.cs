using System.Globalization;
using System.Text;

namespace VortexKit.Core;

/// <summary>
/// Service for exporting visualizations as SVG with full vector fidelity.
/// Supports layer separation, Inkscape metadata, and custom color palettes.
/// </summary>
public class SvgExportService
{
    private const string InkscapeNamespace = "http://www.inkscape.org/namespaces/inkscape";
    private const string SodipodiNamespace = "http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd";

    /// <summary>
    /// Export trajectory data as SVG with configurable layers and styling.
    /// </summary>
    public async Task<string> ExportSvgAsync(
        SvgExportData data,
        string outputPath,
        SvgExportOptions? options = null)
    {
        options ??= new SvgExportOptions();

        var svg = BuildSvg(data, options);
        await File.WriteAllTextAsync(outputPath, svg);

        return outputPath;
    }

    /// <summary>
    /// Build SVG content as string.
    /// </summary>
    public string BuildSvg(SvgExportData data, SvgExportOptions? options = null)
    {
        options ??= new SvgExportOptions();
        var palette = options.Palette ?? SvgColorPalette.Default;

        var sb = new StringBuilder();

        // SVG header with namespaces for Inkscape compatibility
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\"");
        sb.AppendLine($"     xmlns:inkscape=\"{InkscapeNamespace}\"");
        sb.AppendLine($"     xmlns:sodipodi=\"{SodipodiNamespace}\"");
        sb.AppendLine($"     width=\"{options.Width}\" height=\"{options.Height}\"");
        sb.AppendLine($"     viewBox=\"{F(data.ViewBox.X)} {F(data.ViewBox.Y)} {F(data.ViewBox.Width)} {F(data.ViewBox.Height)}\">");

        // Inkscape metadata
        if (options.IncludeInkscapeMetadata)
        {
            sb.AppendLine("  <sodipodi:namedview");
            sb.AppendLine("     inkscape:document-units=\"px\"");
            sb.AppendLine("     inkscape:pageopacity=\"0\"");
            sb.AppendLine("     inkscape:pageshadow=\"2\"");
            sb.AppendLine($"     inkscape:window-width=\"{options.Width}\"");
            sb.AppendLine($"     inkscape:window-height=\"{options.Height}\" />");
        }

        // Title and description
        if (!string.IsNullOrEmpty(options.Title))
            sb.AppendLine($"  <title>{EscapeXml(options.Title)}</title>");
        if (!string.IsNullOrEmpty(options.Description))
            sb.AppendLine($"  <desc>{EscapeXml(options.Description)}</desc>");

        // Definitions (gradients, markers, filters)
        sb.AppendLine("  <defs>");
        BuildDefs(sb, data, options, palette);
        sb.AppendLine("  </defs>");

        // Background layer
        if (options.IncludeBackground)
        {
            sb.AppendLine($"  <g id=\"layer-background\" inkscape:groupmode=\"layer\" inkscape:label=\"Background\">");
            sb.AppendLine($"    <rect x=\"{F(data.ViewBox.X)}\" y=\"{F(data.ViewBox.Y)}\" width=\"{F(data.ViewBox.Width)}\" height=\"{F(data.ViewBox.Height)}\" fill=\"{palette.Background}\" />");
            sb.AppendLine("  </g>");
        }

        // Grid layer
        if (options.IncludeGrid && data.Grid != null)
        {
            sb.AppendLine($"  <g id=\"layer-grid\" inkscape:groupmode=\"layer\" inkscape:label=\"Grid\">");
            BuildGridLayer(sb, data.Grid, options, palette);
            sb.AppendLine("  </g>");
        }

        // Vector field layer
        if (options.IncludeVectorField && data.VectorField != null)
        {
            sb.AppendLine($"  <g id=\"layer-vectorfield\" inkscape:groupmode=\"layer\" inkscape:label=\"Vector Field\">");
            BuildVectorFieldLayer(sb, data.VectorField, options, palette);
            sb.AppendLine("  </g>");
        }

        // Trajectory layer(s)
        if (data.Trajectories.Count > 0)
        {
            for (int i = 0; i < data.Trajectories.Count; i++)
            {
                var traj = data.Trajectories[i];
                var label = traj.Label ?? $"Trajectory {i + 1}";
                sb.AppendLine($"  <g id=\"layer-trajectory-{i}\" inkscape:groupmode=\"layer\" inkscape:label=\"{EscapeXml(label)}\">");
                BuildTrajectoryLayer(sb, traj, options, palette, i);
                sb.AppendLine("  </g>");
            }
        }

        // Heat map overlay layer
        if (options.IncludeHeatMap && data.HeatMap != null)
        {
            sb.AppendLine($"  <g id=\"layer-heatmap\" inkscape:groupmode=\"layer\" inkscape:label=\"Heat Map\" opacity=\"0.5\">");
            BuildHeatMapLayer(sb, data.HeatMap, options, palette);
            sb.AppendLine("  </g>");
        }

        // Annotations layer
        if (options.IncludeAnnotations && data.Annotations.Count > 0)
        {
            sb.AppendLine($"  <g id=\"layer-annotations\" inkscape:groupmode=\"layer\" inkscape:label=\"Annotations\">");
            BuildAnnotationsLayer(sb, data.Annotations, options, palette);
            sb.AppendLine("  </g>");
        }

        // Markers layer (failure points, eigenvalue changes, etc.)
        if (options.IncludeMarkers && data.Markers.Count > 0)
        {
            sb.AppendLine($"  <g id=\"layer-markers\" inkscape:groupmode=\"layer\" inkscape:label=\"Markers\">");
            BuildMarkersLayer(sb, data.Markers, options, palette);
            sb.AppendLine("  </g>");
        }

        sb.AppendLine("</svg>");

        return sb.ToString();
    }

    private void BuildDefs(StringBuilder sb, SvgExportData data, SvgExportOptions options, SvgColorPalette palette)
    {
        // Glow filter
        if (options.EnableGlow)
        {
            sb.AppendLine("    <filter id=\"glow\" x=\"-50%\" y=\"-50%\" width=\"200%\" height=\"200%\">");
            sb.AppendLine("      <feGaussianBlur in=\"SourceGraphic\" stdDeviation=\"3\" result=\"blur\" />");
            sb.AppendLine("      <feMerge>");
            sb.AppendLine("        <feMergeNode in=\"blur\" />");
            sb.AppendLine("        <feMergeNode in=\"SourceGraphic\" />");
            sb.AppendLine("      </feMerge>");
            sb.AppendLine("    </filter>");
        }

        // Arrow marker for vector field
        sb.AppendLine($"    <marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"4\" markerHeight=\"4\" orient=\"auto-start-reverse\">");
        sb.AppendLine($"      <path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"{palette.VectorField}\" />");
        sb.AppendLine("    </marker>");

        // Failure marker
        sb.AppendLine($"    <symbol id=\"failure-marker\" viewBox=\"0 0 20 20\">");
        sb.AppendLine($"      <circle cx=\"10\" cy=\"10\" r=\"8\" fill=\"none\" stroke=\"{palette.Failure}\" stroke-width=\"2\" />");
        sb.AppendLine($"      <line x1=\"6\" y1=\"6\" x2=\"14\" y2=\"14\" stroke=\"{palette.Failure}\" stroke-width=\"2\" />");
        sb.AppendLine($"      <line x1=\"14\" y1=\"6\" x2=\"6\" y2=\"14\" stroke=\"{palette.Failure}\" stroke-width=\"2\" />");
        sb.AppendLine("    </symbol>");

        // Trajectory gradients for time/velocity coloring
        if (options.ColorMode == SvgColorMode.Time || options.ColorMode == SvgColorMode.Velocity)
        {
            for (int i = 0; i < data.Trajectories.Count; i++)
            {
                var colors = options.ColorMode == SvgColorMode.Time
                    ? new[] { palette.TrajectoryStart, palette.TrajectoryEnd }
                    : new[] { palette.LowVelocity, palette.HighVelocity };

                sb.AppendLine($"    <linearGradient id=\"traj-gradient-{i}\" gradientUnits=\"userSpaceOnUse\">");
                sb.AppendLine($"      <stop offset=\"0%\" stop-color=\"{colors[0]}\" />");
                sb.AppendLine($"      <stop offset=\"100%\" stop-color=\"{colors[1]}\" />");
                sb.AppendLine("    </linearGradient>");
            }
        }
    }

    private void BuildGridLayer(StringBuilder sb, SvgGridData grid, SvgExportOptions options, SvgColorPalette palette)
    {
        var strokeWidth = F(options.GridStrokeWidth);

        // Major grid lines
        foreach (var line in grid.MajorLines)
        {
            sb.AppendLine($"    <line x1=\"{F(line.X1)}\" y1=\"{F(line.Y1)}\" x2=\"{F(line.X2)}\" y2=\"{F(line.Y2)}\" stroke=\"{palette.GridMajor}\" stroke-width=\"{strokeWidth}\" />");
        }

        // Minor grid lines
        foreach (var line in grid.MinorLines)
        {
            sb.AppendLine($"    <line x1=\"{F(line.X1)}\" y1=\"{F(line.Y1)}\" x2=\"{F(line.X2)}\" y2=\"{F(line.Y2)}\" stroke=\"{palette.GridMinor}\" stroke-width=\"{F(options.GridStrokeWidth * 0.5)}\" stroke-dasharray=\"2,2\" />");
        }

        // Axis labels
        if (options.IncludeAxisLabels)
        {
            foreach (var label in grid.Labels)
            {
                sb.AppendLine($"    <text x=\"{F(label.X)}\" y=\"{F(label.Y)}\" fill=\"{palette.Text}\" font-size=\"{options.LabelFontSize}\" font-family=\"{options.FontFamily}\" text-anchor=\"middle\">{EscapeXml(label.Text)}</text>");
            }
        }
    }

    private void BuildVectorFieldLayer(StringBuilder sb, SvgVectorFieldData vectorField, SvgExportOptions options, SvgColorPalette palette)
    {
        foreach (var arrow in vectorField.Arrows)
        {
            var opacity = F(Math.Min(1.0, arrow.Magnitude * options.VectorFieldOpacityScale));
            sb.AppendLine($"    <line x1=\"{F(arrow.X)}\" y1=\"{F(arrow.Y)}\" x2=\"{F(arrow.X + arrow.Dx)}\" y2=\"{F(arrow.Y + arrow.Dy)}\" stroke=\"{palette.VectorField}\" stroke-width=\"{F(options.VectorFieldStrokeWidth)}\" marker-end=\"url(#arrow)\" opacity=\"{opacity}\" />");
        }
    }

    private void BuildTrajectoryLayer(StringBuilder sb, SvgTrajectoryData traj, SvgExportOptions options, SvgColorPalette palette, int index)
    {
        if (traj.Points.Count < 2) return;

        var pathData = new StringBuilder();

        // Choose path type based on options
        if (options.UseCatmullRomSplines && traj.Points.Count >= 4)
        {
            pathData.Append(BuildCatmullRomPath(traj.Points));
        }
        else
        {
            // Simple polyline
            pathData.Append($"M {F(traj.Points[0].X)} {F(traj.Points[0].Y)}");
            for (int i = 1; i < traj.Points.Count; i++)
            {
                pathData.Append($" L {F(traj.Points[i].X)} {F(traj.Points[i].Y)}");
            }
        }

        // Determine stroke style
        string stroke;
        if (options.ColorMode == SvgColorMode.Solid)
        {
            stroke = traj.Color ?? palette.Trajectory;
        }
        else
        {
            stroke = $"url(#traj-gradient-{index})";
        }

        var strokeWidth = F(options.TrajectoryStrokeWidth);
        var filter = options.EnableGlow ? " filter=\"url(#glow)\"" : "";

        sb.AppendLine($"    <path d=\"{pathData}\" fill=\"none\" stroke=\"{stroke}\" stroke-width=\"{strokeWidth}\" stroke-linecap=\"round\" stroke-linejoin=\"round\"{filter} />");

        // Trail markers (optional dots along path)
        if (options.IncludeTrailMarkers)
        {
            var interval = Math.Max(1, traj.Points.Count / options.TrailMarkerCount);
            for (int i = 0; i < traj.Points.Count; i += interval)
            {
                var pt = traj.Points[i];
                var radius = F(options.TrailMarkerRadius);
                sb.AppendLine($"    <circle cx=\"{F(pt.X)}\" cy=\"{F(pt.Y)}\" r=\"{radius}\" fill=\"{stroke}\" opacity=\"0.6\" />");
            }
        }

        // Start/end markers
        if (options.IncludeStartEndMarkers)
        {
            var start = traj.Points[0];
            var end = traj.Points[^1];
            sb.AppendLine($"    <circle cx=\"{F(start.X)}\" cy=\"{F(start.Y)}\" r=\"{F(options.TrajectoryStrokeWidth * 2)}\" fill=\"{palette.TrajectoryStart}\" />");
            sb.AppendLine($"    <circle cx=\"{F(end.X)}\" cy=\"{F(end.Y)}\" r=\"{F(options.TrajectoryStrokeWidth * 2)}\" fill=\"{palette.TrajectoryEnd}\" />");
        }
    }

    private string BuildCatmullRomPath(List<SvgPoint> points)
    {
        // Catmull-Rom to cubic Bezier conversion
        var sb = new StringBuilder();
        sb.Append($"M {F(points[0].X)} {F(points[0].Y)}");

        for (int i = 0; i < points.Count - 1; i++)
        {
            var p0 = i > 0 ? points[i - 1] : points[i];
            var p1 = points[i];
            var p2 = points[i + 1];
            var p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];

            // Catmull-Rom to Bezier control points
            var cp1x = p1.X + (p2.X - p0.X) / 6.0;
            var cp1y = p1.Y + (p2.Y - p0.Y) / 6.0;
            var cp2x = p2.X - (p3.X - p1.X) / 6.0;
            var cp2y = p2.Y - (p3.Y - p1.Y) / 6.0;

            sb.Append($" C {F(cp1x)} {F(cp1y)}, {F(cp2x)} {F(cp2y)}, {F(p2.X)} {F(p2.Y)}");
        }

        return sb.ToString();
    }

    private void BuildHeatMapLayer(StringBuilder sb, SvgHeatMapData heatMap, SvgExportOptions options, SvgColorPalette palette)
    {
        foreach (var cell in heatMap.Cells)
        {
            var color = InterpolateHeatColor(cell.Intensity, palette);
            sb.AppendLine($"    <rect x=\"{F(cell.X)}\" y=\"{F(cell.Y)}\" width=\"{F(cell.Width)}\" height=\"{F(cell.Height)}\" fill=\"{color}\" />");
        }
    }

    private void BuildAnnotationsLayer(StringBuilder sb, List<SvgAnnotation> annotations, SvgExportOptions options, SvgColorPalette palette)
    {
        foreach (var ann in annotations)
        {
            // Background rect for readability
            sb.AppendLine($"    <rect x=\"{F(ann.X - 2)}\" y=\"{F(ann.Y - options.AnnotationFontSize)}\" width=\"{F(ann.Text.Length * options.AnnotationFontSize * 0.6)}\" height=\"{F(options.AnnotationFontSize * 1.2)}\" fill=\"{palette.AnnotationBackground}\" rx=\"2\" />");
            sb.AppendLine($"    <text x=\"{F(ann.X)}\" y=\"{F(ann.Y)}\" fill=\"{palette.Annotation}\" font-size=\"{options.AnnotationFontSize}\" font-family=\"{options.FontFamily}\">{EscapeXml(ann.Text)}</text>");
        }
    }

    private void BuildMarkersLayer(StringBuilder sb, List<SvgMarker> markers, SvgExportOptions options, SvgColorPalette palette)
    {
        foreach (var marker in markers)
        {
            var color = marker.Type switch
            {
                SvgMarkerType.Failure => palette.Failure,
                SvgMarkerType.Eigenvalue => palette.Eigenvalue,
                SvgMarkerType.Curvature => palette.Curvature,
                _ => palette.Marker
            };

            if (marker.Type == SvgMarkerType.Failure)
            {
                sb.AppendLine($"    <use href=\"#failure-marker\" x=\"{F(marker.X - 10)}\" y=\"{F(marker.Y - 10)}\" width=\"20\" height=\"20\" />");
            }
            else
            {
                sb.AppendLine($"    <circle cx=\"{F(marker.X)}\" cy=\"{F(marker.Y)}\" r=\"{F(marker.Radius)}\" fill=\"{color}\" opacity=\"0.8\" />");
            }

            if (!string.IsNullOrEmpty(marker.Label))
            {
                sb.AppendLine($"    <text x=\"{F(marker.X + marker.Radius + 4)}\" y=\"{F(marker.Y + 4)}\" fill=\"{palette.Text}\" font-size=\"{options.MarkerLabelFontSize}\" font-family=\"{options.FontFamily}\">{EscapeXml(marker.Label)}</text>");
            }
        }
    }

    private string InterpolateHeatColor(double intensity, SvgColorPalette palette)
    {
        // Simple heat map: low=blue, mid=yellow, high=red
        intensity = Math.Clamp(intensity, 0, 1);

        if (intensity < 0.5)
        {
            var t = intensity * 2;
            return InterpolateColor(palette.HeatLow, palette.HeatMid, t);
        }
        else
        {
            var t = (intensity - 0.5) * 2;
            return InterpolateColor(palette.HeatMid, palette.HeatHigh, t);
        }
    }

    private static string InterpolateColor(string c1, string c2, double t)
    {
        var (r1, g1, b1) = ParseHexColor(c1);
        var (r2, g2, b2) = ParseHexColor(c2);

        var r = (int)(r1 + (r2 - r1) * t);
        var g = (int)(g1 + (g2 - g1) * t);
        var b = (int)(b1 + (b2 - b1) * t);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static (int r, int g, int b) ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        return (
            int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber),
            int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber),
            int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber)
        );
    }

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapeXml(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}

#region Data Models

/// <summary>
/// Complete data for SVG export.
/// </summary>
public record SvgExportData
{
    public SvgViewBox ViewBox { get; init; } = new(-1, -1, 2, 2);
    public List<SvgTrajectoryData> Trajectories { get; init; } = [];
    public SvgGridData? Grid { get; init; }
    public SvgVectorFieldData? VectorField { get; init; }
    public SvgHeatMapData? HeatMap { get; init; }
    public List<SvgAnnotation> Annotations { get; init; } = [];
    public List<SvgMarker> Markers { get; init; } = [];
}

public record SvgViewBox(double X, double Y, double Width, double Height);

public record SvgTrajectoryData
{
    public List<SvgPoint> Points { get; init; } = [];
    public string? Label { get; init; }
    public string? Color { get; init; }
    public List<double>? Velocities { get; init; }
    public List<double>? Curvatures { get; init; }
}

public record SvgPoint(double X, double Y);

public record SvgGridData
{
    public List<SvgLine> MajorLines { get; init; } = [];
    public List<SvgLine> MinorLines { get; init; } = [];
    public List<SvgLabel> Labels { get; init; } = [];
}

public record SvgLine(double X1, double Y1, double X2, double Y2);

public record SvgLabel(double X, double Y, string Text);

public record SvgVectorFieldData
{
    public List<SvgArrow> Arrows { get; init; } = [];
}

public record SvgArrow(double X, double Y, double Dx, double Dy, double Magnitude);

public record SvgHeatMapData
{
    public List<SvgHeatCell> Cells { get; init; } = [];
}

public record SvgHeatCell(double X, double Y, double Width, double Height, double Intensity);

public record SvgAnnotation(double X, double Y, string Text);

public record SvgMarker(double X, double Y, double Radius, SvgMarkerType Type, string? Label = null);

public enum SvgMarkerType
{
    Generic,
    Failure,
    Eigenvalue,
    Curvature
}

#endregion

#region Options

/// <summary>
/// Options for SVG export.
/// </summary>
public record SvgExportOptions
{
    // Dimensions
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;

    // Metadata
    public string? Title { get; init; }
    public string? Description { get; init; }
    public bool IncludeInkscapeMetadata { get; init; } = true;

    // Layers
    public bool IncludeBackground { get; init; } = true;
    public bool IncludeGrid { get; init; } = true;
    public bool IncludeVectorField { get; init; } = true;
    public bool IncludeHeatMap { get; init; } = true;
    public bool IncludeAnnotations { get; init; } = true;
    public bool IncludeMarkers { get; init; } = true;
    public bool IncludeAxisLabels { get; init; } = true;

    // Trajectory styling
    public double TrajectoryStrokeWidth { get; init; } = 2.0;
    public bool UseCatmullRomSplines { get; init; } = true;
    public bool IncludeTrailMarkers { get; init; } = false;
    public int TrailMarkerCount { get; init; } = 20;
    public double TrailMarkerRadius { get; init; } = 3.0;
    public bool IncludeStartEndMarkers { get; init; } = true;
    public SvgColorMode ColorMode { get; init; } = SvgColorMode.Solid;
    public bool EnableGlow { get; init; } = true;

    // Grid styling
    public double GridStrokeWidth { get; init; } = 0.5;

    // Vector field styling
    public double VectorFieldStrokeWidth { get; init; } = 1.0;
    public double VectorFieldOpacityScale { get; init; } = 2.0;

    // Text styling
    public string FontFamily { get; init; } = "Arial, sans-serif";
    public double LabelFontSize { get; init; } = 12;
    public double AnnotationFontSize { get; init; } = 10;
    public double MarkerLabelFontSize { get; init; } = 9;

    // Colors
    public SvgColorPalette? Palette { get; init; }
}

public enum SvgColorMode
{
    Solid,
    Time,
    Velocity,
    Curvature
}

/// <summary>
/// Color palette for SVG export.
/// </summary>
public record SvgColorPalette
{
    public string Background { get; init; } = "#1E1E2E";
    public string Text { get; init; } = "#CDD6F4";
    public string GridMajor { get; init; } = "#45475A";
    public string GridMinor { get; init; } = "#313244";

    public string Trajectory { get; init; } = "#89B4FA";
    public string TrajectoryStart { get; init; } = "#94E2D5";
    public string TrajectoryEnd { get; init; } = "#F38BA8";

    public string LowVelocity { get; init; } = "#89B4FA";
    public string HighVelocity { get; init; } = "#F9E2AF";

    public string VectorField { get; init; } = "#6C7086";

    public string HeatLow { get; init; } = "#1E1E2E";
    public string HeatMid { get; init; } = "#F9E2AF";
    public string HeatHigh { get; init; } = "#F38BA8";

    public string Failure { get; init; } = "#F38BA8";
    public string Eigenvalue { get; init; } = "#CBA6F7";
    public string Curvature { get; init; } = "#F9E2AF";
    public string Marker { get; init; } = "#89B4FA";

    public string Annotation { get; init; } = "#CDD6F4";
    public string AnnotationBackground { get; init; } = "#31324480";

    // Preset palettes
    public static SvgColorPalette Default => new();

    public static SvgColorPalette Light => new()
    {
        Background = "#FFFFFF",
        Text = "#1E1E2E",
        GridMajor = "#CDD6F4",
        GridMinor = "#E6E9EF",
        Trajectory = "#1E66F5",
        TrajectoryStart = "#40A02B",
        TrajectoryEnd = "#D20F39",
        LowVelocity = "#1E66F5",
        HighVelocity = "#DF8E1D",
        VectorField = "#9CA0B0",
        HeatLow = "#FFFFFF",
        HeatMid = "#DF8E1D",
        HeatHigh = "#D20F39",
        Failure = "#D20F39",
        Eigenvalue = "#8839EF",
        Curvature = "#DF8E1D",
        Marker = "#1E66F5",
        Annotation = "#1E1E2E",
        AnnotationBackground = "#E6E9EF80"
    };

    public static SvgColorPalette HighContrast => new()
    {
        Background = "#000000",
        Text = "#FFFFFF",
        GridMajor = "#666666",
        GridMinor = "#333333",
        Trajectory = "#00FFFF",
        TrajectoryStart = "#00FF00",
        TrajectoryEnd = "#FF0000",
        LowVelocity = "#0000FF",
        HighVelocity = "#FFFF00",
        VectorField = "#888888",
        HeatLow = "#000000",
        HeatMid = "#FFFF00",
        HeatHigh = "#FF0000",
        Failure = "#FF0000",
        Eigenvalue = "#FF00FF",
        Curvature = "#FFFF00",
        Marker = "#00FFFF",
        Annotation = "#FFFFFF",
        AnnotationBackground = "#00000080"
    };

    public static SvgColorPalette Publication => new()
    {
        Background = "#FFFFFF",
        Text = "#000000",
        GridMajor = "#CCCCCC",
        GridMinor = "#EEEEEE",
        Trajectory = "#0066CC",
        TrajectoryStart = "#228B22",
        TrajectoryEnd = "#CC0000",
        LowVelocity = "#0066CC",
        HighVelocity = "#CC6600",
        VectorField = "#999999",
        HeatLow = "#FFFFFF",
        HeatMid = "#FFCC00",
        HeatHigh = "#CC0000",
        Failure = "#CC0000",
        Eigenvalue = "#6600CC",
        Curvature = "#CC6600",
        Marker = "#0066CC",
        Annotation = "#000000",
        AnnotationBackground = "#FFFFFF80"
    };
}

#endregion
