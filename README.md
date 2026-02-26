<p align="center">
  <a href="README.md">English</a> | <a href="README.ja.md">日本語</a> | <a href="README.zh.md">中文</a> | <a href="README.es.md">Español</a> | <a href="README.fr.md">Français</a> | <a href="README.hi.md">हिन्दी</a> | <a href="README.it.md">Italiano</a> | <a href="README.pt-BR.md">Português (BR)</a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/mcp-tool-shop-org/brand/main/logos/ScalarScope-Desktop/readme.png" alt="ScalarScope" width="400">
</p>

# ScalarScope-Desktop

> Part of [MCP Tool Shop](https://mcptoolshop.com)

<p align="center">
  <a href="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml"><img src="https://github.com/mcp-tool-shop-org/ScalarScope-Desktop/actions/workflows/build.yml/badge.svg" alt="CI"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT"></a>
  <a href="https://mcp-tool-shop-org.github.io/ScalarScope-Desktop/"><img src="https://img.shields.io/badge/Landing_Page-live-blue?style=flat-square" alt="Landing Page"></a>
  <a href="https://apps.microsoft.com/detail/9P3HT1PHBKQK"><img src="https://img.shields.io/badge/Microsoft%20Store-9P3HT1PHBKQK-0078D4?style=flat-square&logo=microsoft" alt="Microsoft Store"></a>
  <a href="https://www.nuget.org/packages/VortexKit"><img src="https://img.shields.io/nuget/v/VortexKit?label=VortexKit&logo=nuget&style=flat-square" alt="NuGet: VortexKit"></a>
</p>

**ASPIRE Scalar Vortex Visualizer — a .NET MAUI desktop app for comparing ML inference runs with scientific rigor.**

---

## Why ScalarScope?

Most ML teams eyeball logs. ScalarScope replaces that with structured, reproducible comparison.

- **Apples-to-apples comparison** — Load two inference traces side by side and see exactly what changed
- **Canonical delta analysis** — Five delta types (ΔTc, ΔO, ΔF, ΔĀ, ΔTd) fire only when differences are statistically meaningful
- **Runtime presets** — The TFRT preset auto-suppresses irrelevant metrics so you focus on what matters for TensorFlow-TRT workloads
- **Reproducible bundles** — Export `.scbundle` archives with SHA-256 integrity, frozen deltas, and full provenance metadata
- **Review mode** — Open a bundle without recomputing; results are cryptographically verified, not re-derived
- **Privacy first** — Zero telemetry, zero analytics, all data stays local unless you explicitly export

---

## NuGet Packages

| Package | Version | Description |
|---------|---------|-------------|
| [VortexKit](https://www.nuget.org/packages/VortexKit) | [![NuGet](https://img.shields.io/nuget/v/VortexKit)](https://www.nuget.org/packages/VortexKit) | Reusable visualization framework for training dynamics — time-synced playback, animated SkiaSharp canvases, comparison views, annotation overlays, SVG/PNG export, and a semantic color system. Built on SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

---

## Quick Start

### From the Microsoft Store

1. Install **ScalarScope** from the [Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK) (Store ID: `9P3HT1PHBKQK`)
2. Click **Compare Two Runs**
3. Load baseline TFRT trace (before optimization)
4. Load optimized TFRT trace (after optimization)
5. Review deltas in the **Compare** tab
6. Export a `.scbundle` for reproducible sharing

### Using VortexKit in Your Own App

```csharp
using VortexKit.Core;

// 1. Create a shared playback controller (0.0 -> 1.0 timeline)
var player = new PlaybackController { Duration = 10.0, Loop = true };

// 2. Bind multiple animated canvases to the same controller
player.TimeChanged += () =>
{
    trajectoryCanvas.CurrentTime = player.Time;
    eigenCanvas.CurrentTime      = player.Time;
    scalarsCanvas.CurrentTime    = player.Time;
};

// 3. Subclass AnimatedCanvas for custom rendering
public class MyTrajectoryCanvas : AnimatedCanvas
{
    protected override void OnRender(SKCanvas canvas, SKImageInfo info, double time)
    {
        // Your SkiaSharp rendering at the current time position
    }
}

// 4. Export a side-by-side comparison as PNG
var exporter = new ExportService();
await exporter.ExportComparisonAsync(
    leftRender, rightRender, time: 0.5,
    outputPath: "comparison.png",
    new ComparisonExportOptions
    {
        Width = 1920, Height = 1080,
        LeftLabel = "Baseline", RightLabel = "Optimized",
        ShowLabels = true
    });

// 5. Export as layered SVG (Inkscape-compatible)
var svgExporter = new SvgExportService();
await svgExporter.ExportSvgAsync(svgData, "trajectory.svg",
    new SvgExportOptions
    {
        Palette = SvgColorPalette.Publication,
        UseCatmullRomSplines = true,
        EnableGlow = false
    });
```

---

## Features

### Delta Analysis — Five Canonical Delta Types

Every comparison produces a set of canonical deltas. Each delta fires only when the difference is statistically meaningful; irrelevant deltas are suppressed automatically.

| Delta | Full Name | What It Measures | Fires When |
|-------|-----------|------------------|------------|
| **ΔTc** | Convergence Time | Steps to reach stable latency | Steady-state reached at different steps (≥3-step separation) |
| **ΔO** | Output Variability | Oscillation / runtime instability | Area-above-threshold score differs beyond noise floor |
| **ΔF** | Failure Rate | Anomaly frequency | Failure frequency or kind differs between runs |
| **ΔĀ** | Average Latency | Mean metric value | Mean differs meaningfully (suppressed in TFRT preset) |
| **ΔTd** | Total Duration | Wall-clock time / structural emergence | Duration or dominance onset differs (suppressed in TFRT preset) |

### Runtime Presets — TFRT

The built-in **TensorFlow-TRT** preset (`tensorflowrt-runtime-v1`) maps inference-specific signals (latency, throughput, memory, CPU/GPU load) and suppresses training-only deltas (ΔĀ, ΔTd) that have no meaning for inference comparison. Guardrails warn when warmup exceeds 50% of the run or when only aggregated stats are available.

### Reproducible Bundles

Export results as `.scbundle` archives (ComparisonBundle v1.0.0):

- **`manifest.json`** — bundle metadata, app version, comparison labels, alignment mode
- **`repro/repro.json`** — input fingerprints, preset hash, determinism seed, environment info
- **`findings/deltas.json`** — canonical deltas with confidence scores, anchors, and trigger types
- **`findings/why.json`** — human-readable explanations, guardrails, parameter chips
- **`findings/summary.md`** — auto-generated Markdown summary
- **Integrity** — every file hashed with SHA-256; bundle-level hash for tamper detection

### Review Mode

Open any `.scbundle` without recomputing. Review mode verifies integrity, displays frozen deltas, and shows a review-mode banner so you know results are verified, not re-derived.

### VortexKit Visualization Framework

VortexKit is the extracted visualization engine, published as a standalone NuGet package:

| Component | What It Does |
|-----------|-------------|
| `PlaybackController` | Shared 0→1 timeline with play/pause/step/loop, speed presets (0.25x—4x), ~60 fps tick |
| `AnimatedCanvas` | Abstract `SKCanvasView` base with time-synced invalidation, grid drawing, touch/drag events, coordinate helpers |
| `ITimeSeries<T>` / `TimeSeries<T>` | Generic time-series with index↔time mapping and trail enumeration |
| `ExportService` | Single-frame PNG, frame sequences (with ffmpeg hints), and side-by-side comparison export |
| `SvgExportService` | Full-vector SVG export with Inkscape layers, Catmull-Rom splines, heatmaps, vector fields, and four color palettes (Default, Light, HighContrast, Publication) |
| `IAnnotation` | Typed annotations (Phase, Warning, Insight, Failure, Custom) with theoretical basis and priority |
| `VortexColors` | Semantic color palette — background layers, accent semantics, severity coding, eigenvalue palette, lerp/gradient helpers |

---

## Installation

### Microsoft Store (recommended)

**Store ID:** `9P3HT1PHBKQK`

[Get it from the Microsoft Store](https://apps.microsoft.com/detail/9P3HT1PHBKQK)

Requires Windows 10 (build 17763) or later.

### From Source

```bash
# Prerequisites:
#   .NET 9.0 SDK (global.json pins 9.0.100)
#   Visual Studio 2022 with MAUI workload, or:
#     dotnet workload install maui-windows

git clone https://github.com/mcp-tool-shop-org/ScalarScope-Desktop.git
cd ScalarScope-Desktop
dotnet restore
dotnet build

# Run the desktop app
dotnet run --project src/ScalarScope
```

### NuGet (library only)

```bash
dotnet add package VortexKit
```

---

## Project Structure

```
ScalarScope-Desktop/
├── src/
│   ├── ScalarScope/                    # .NET MAUI desktop app
│   │   ├── Models/                     # GeometryRun, InsightEvent
│   │   ├── ViewModels/                 # Welcome, Comparison, Export, Settings, TrajectoryPlayer, VortexSession
│   │   ├── Views/                      # XAML pages + 30+ custom controls
│   │   │   ├── WelcomePage.xaml        # First-60-seconds onboarding
│   │   │   ├── ComparisonPage.xaml     # Side-by-side delta comparison
│   │   │   ├── TrajectoryPage.xaml     # Animated trajectory playback
│   │   │   ├── GeometryPage.xaml       # Eigenvalue spectrum view
│   │   │   └── Controls/              # DeltaZone, BundleExportPanel, PlaybackControl, etc.
│   │   ├── Services/
│   │   │   ├── Connectors/            # RunTraceComparer, TfrtRuntimePreset, validation
│   │   │   ├── Bundles/               # BundleBuilder, BundleExporter, integrity, schemas
│   │   │   ├── Evidence/              # Comparison evidence reports, detector diagnostics
│   │   │   ├── Plugins/               # PluginManager
│   │   │   ├── CanonicalDeltaService.cs
│   │   │   ├── DeltaTypes.cs          # 5 canonical deltas + detector configs
│   │   │   ├── DeterminismService.cs  # Reproducible seed management
│   │   │   ├── FlowFieldService.cs    # Vector field computation
│   │   │   └── ...                    # 40+ service files
│   │   └── Resources/
│   │       ├── Styles/DesignSystem.xaml # Unified visual grammar
│   │       └── Raw/Samples/            # Built-in example traces
│   │
│   └── VortexKit/                      # Standalone NuGet library
│       ├── Core/
│       │   ├── AnimatedCanvas.cs       # Time-synced SkiaSharp canvas base
│       │   ├── PlaybackController.cs   # Shared playback timeline
│       │   ├── ITimeSeries.cs          # Generic time-series interface
│       │   ├── ExportService.cs        # PNG frame/sequence export
│       │   └── SvgExportService.cs     # Layered SVG export
│       ├── Annotations/
│       │   └── IAnnotation.cs          # Typed annotation system
│       └── Theme/
│           └── VortexColors.cs         # Semantic color palette
│
├── tests/
│   ├── ScalarScope.FixtureTests/       # Golden-file fixture tests
│   ├── ScalarScope.DeterminismTests/   # Reproducibility verification
│   ├── ScalarScope.SoakTests/          # Long-running stability tests
│   └── Fixtures/                       # Shared test data
│
├── docs/                               # Design docs, results, limitations
├── .github/workflows/
│   ├── build.yml                       # CI: restore, build, format check, pack, artifacts
│   ├── publish.yml                     # NuGet publish
│   └── release.yml                     # GitHub Release + Store submission
├── global.json                         # .NET SDK 9.0.100
├── ScalarScope.sln                     # Solution file
├── CHANGELOG.md                        # Keep-a-Changelog format
├── PRIVACY.md                          # Privacy policy (no telemetry)
├── SECURITY.md                         # Security policy
└── STORE_LISTING.md                    # Microsoft Store listing copy
```

---

## Testing

```bash
# Run all tests
dotnet test

# Fixture smoke tests only
dotnet test --filter Category=FixtureSmoke

# Determinism tests (verifies reproducible deltas)
dotnet test --filter Category=Determinism

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Related

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) — Core training framework
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) — Full experimental results
- [CHANGELOG.md](CHANGELOG.md) — Release history
- [PRIVACY.md](PRIVACY.md) — Privacy policy
- [ROADMAP.md](ROADMAP.md) — Planned features

---

## License

[MIT](LICENSE) — Copyright (c) 2025-2026 ScalarScope Project (mcp-tool-shop-org)
