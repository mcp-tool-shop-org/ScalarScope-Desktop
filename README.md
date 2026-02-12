# ScalarScope

**Compare inference optimization runs with scientific rigor.**

[![Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-9P3HT1PHBKQK-0078D4?logo=microsoft)](https://apps.microsoft.com/detail/9P3HT1PHBKQK)
[![Version](https://img.shields.io/badge/version-2.0.0-00d9ff)](https://github.com/mcp-tool-shop/ScalarScope/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010+-blue)](https://www.microsoft.com/store/apps/9P3HT1PHBKQK)

## Overview

ScalarScope is a precision instrument for comparing machine learning inference runs. Whether you're optimizing TensorFlow-RT models, tuning ONNX deployments, or benchmarking PyTorch inference—ScalarScope gives you the scientific rigor to prove your optimizations work.

### Key Features

- **Compare Two Runs**: Load before/after inference traces and see what changed
- **Delta Analysis**: Canonical deltas (ΔTc, ΔO, ΔF) fire when differences are significant
- **Runtime Presets**: TFRT preset suppresses irrelevant metrics automatically
- **Reproducible Bundles**: Export comparisons with cryptographic integrity (SHA-256)
- **Review Mode**: Open bundles without recomputing—frozen and verified

### Privacy First

ScalarScope collects **no telemetry**, sends **no analytics**, and stores all data locally. Your inference traces never leave your machine unless you explicitly export them.

## Quick Start

1. **Open ScalarScope** from the Microsoft Store
2. **Click "Compare Two Runs"**
3. **Load baseline** TFRT trace (before optimization)
4. **Load optimized** TFRT trace (after optimization)
5. **Review deltas** in the Compare tab
6. **Export bundle** for reproducible sharing

## Delta Glossary

| Delta | What it measures | When it fires |
|-------|------------------|---------------|
| **ΔTc** | Convergence Time | Steady-state reached at different steps |
| **ΔO** | Output Variability | Runtime variance differs significantly |
| **ΔF** | Failure Rate | Anomaly frequency differs by ≥5% |
| **ΔĀ** | Average Latency | Mean latency differs (suppressed in TFRT) |
| **ΔTd** | Total Duration | Wall-clock time differs (suppressed in TFRT) |

## Architecture

```
src/ScalarScope/
├── Models/
│   └── RuntimeRunTrace.cs    # Unified trace schema
├── ViewModels/
│   ├── WelcomeViewModel.cs   # Landing page state
│   ├── ComparisonViewModel.cs # Comparison logic
│   └── SettingsViewModel.cs  # Preferences
├── Views/
│   ├── WelcomePage.xaml      # First-60-seconds experience
│   ├── ComparisonPage.xaml   # Side-by-side comparison
│   ├── HelpPage.xaml         # Interpretation guide
│   └── SettingsPage.xaml     # Preferences + About
├── Services/
│   ├── Connectors/
│   │   ├── RunTraceComparer.cs # Comparison engine
│   │   ├── RunTraceValidator.cs # Validation pipeline
│   │   └── TfrtRuntimePreset.cs # TFRT suppression rules
│   └── Bundles/
│       ├── BundleBuilder.cs  # Export bundle creation
│       └── BundleImporter.cs # Bundle loading + integrity
└── Resources/Styles/
    └── DesignSystem.xaml     # Unified visual grammar
```

## Building from Source

```bash
# Prerequisites
# - .NET 9.0 SDK
# - Visual Studio 2022 or VS Code with MAUI workload
# - Windows 10 (19041) or later

# Clone and build
git clone https://github.com/mcp-tool-shop/ScalarScope.git
cd ScalarScope
dotnet restore
dotnet build

# Run
dotnet run --project src/ScalarScope
```

## Testing

```bash
# Run all tests
dotnet test

# Run fixture smoke tests only
dotnet test --filter Category=FixtureSmoke

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

The visualizations make this concrete:

| Regime | Evaluator Structure | Visual Signature | Transfer Outcome |
|--------|---------------------|------------------|------------------|
| Path A | Orthogonal professors | Chaotic trajectory, multiple eigen bars | Transfer fails |
| Path B | Correlated professors | Clean spiral, dominant eigen bar | Transfer succeeds |

## NuGet Packages

| Package | Description |
|---------|-------------|
| [VortexKit](https://www.nuget.org/packages/VortexKit) | Visualization framework for training dynamics — time-synced playback, comparison views, annotation overlays, and export. Built on SkiaSharp + MAUI. |

```bash
dotnet add package VortexKit
```

## Related

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) - Core training framework
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) - Full experimental results

## License

MIT License - See LICENSE file for details.
