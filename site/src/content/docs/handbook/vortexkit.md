---
title: VortexKit
description: The standalone visualization framework for .NET MAUI apps.
sidebar:
  order: 4
---

VortexKit is the visualization engine extracted from ScalarScope, published as a standalone NuGet package. Build time-synced animated canvases, comparison views, and export pipelines into any .NET MAUI app.

## Install

```bash
dotnet add package VortexKit
```

## Components

| Component | What It Does |
|-----------|-------------|
| `PlaybackController` | Shared 0-1 timeline with play/pause/step/loop, speed presets (0.25x-4x), ~60 fps tick |
| `AnimatedCanvas` | Abstract `SKCanvasView` base with time-synced invalidation, grid drawing, touch/drag events |
| `ITimeSeries<T>` | Generic time-series with index-to-time mapping and trail enumeration |
| `ExportService` | Single-frame PNG, frame sequences (with ffmpeg hints), side-by-side comparison export |
| `SvgExportService` | Full-vector SVG with Inkscape layers, Catmull-Rom splines, heatmaps, four color palettes |
| `IAnnotation` | Typed annotations (Phase, Warning, Insight, Failure, Custom) with theoretical basis and priority |
| `VortexColors` | Semantic color palette — background layers, accent semantics, severity coding, lerp/gradient helpers |

## Shared playback

```csharp
using VortexKit.Core;

var player = new PlaybackController { Duration = 10.0, Loop = true };

// Bind multiple canvases to the same timeline
player.TimeChanged += () =>
{
    trajectoryCanvas.CurrentTime = player.Time;
    eigenCanvas.CurrentTime      = player.Time;
    scalarsCanvas.CurrentTime    = player.Time;
};
```

## Custom canvas

```csharp
public class MyTrajectoryCanvas : AnimatedCanvas
{
    protected override void OnRender(
        SKCanvas canvas, SKImageInfo info, double time)
    {
        // Your SkiaSharp rendering at the current timeline position
    }
}
```

## Export

```csharp
// Side-by-side comparison PNG
var exporter = new ExportService();
await exporter.ExportComparisonAsync(
    leftRender, rightRender, time: 0.5,
    outputPath: "comparison.png",
    new ComparisonExportOptions
    {
        Width = 1920, Height = 1080,
        LeftLabel = "Baseline", RightLabel = "Optimized"
    });

// Layered SVG (Inkscape-compatible)
var svgExporter = new SvgExportService();
await svgExporter.ExportSvgAsync(svgData, "trajectory.svg",
    new SvgExportOptions
    {
        Palette = SvgColorPalette.Publication,
        UseCatmullRomSplines = true
    });
```
