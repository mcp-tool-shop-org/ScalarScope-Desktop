# ScalarScope

**Scalar Vortex Visualizer** - Interactive visualization of ScalarScope training dynamics.

## Overview

ScalarScope is a .NET MAUI application that visualizes the geometry of evaluative internalization during ScalarScope training runs. It renders the "scalar vortex" - the flow of learning through multi-dimensional state space - making abstract training dynamics tangible and interpretable.

## Key Visualizations

### 1. Trajectory View (Core Vortex)
- 2D/3D projection of the state vector trajectory
- Velocity vectors showing learning direction
- Curvature markers highlighting phase transitions
- Professor vectors showing evaluator alignment

**What to look for:**
- Clean spiral convergence = shared latent structure (Path B success)
- Chaotic multi-basin = orthogonal evaluators (Path A)

### 2. Scalar Ring Stack
- Concentric rings for each token dimension (correctness, coherence, calibration, tradeoffs, clarity)
- Radius = scalar value, angular position = time
- Phase-locked rings = evaluator coherence
- Phase drift = evaluator independence

### 3. Eigen Spectrum
- Top N eigenvalues as animated bars
- One dominant bar = shared latent axis
- Multiple stable bars = plural evaluative dimensions

**Key insight:** First factor variance >50% indicates transfer-viable structure.

### 4. Evaluator Geometry
- Professor positions as vectors in reduced space
- Clustered vectors = correlated professors (Path B)
- Orthogonal vectors = independent professors (Path A)

## Architecture

```
ScalarScope/
├── Models/
│   └── GeometryRun.cs        # JSON schema for geometry exports
├── ViewModels/
│   ├── VortexSessionViewModel.cs   # Global session state
│   └── TrajectoryPlayerViewModel.cs # Playback controller
├── Views/
│   ├── OverviewPage.xaml     # Summary and load
│   ├── TrajectoryPage.xaml   # Flow field canvas
│   ├── ScalarsPage.xaml      # Ring stack
│   └── GeometryPage.xaml     # Eigen spectrum
└── Views/Controls/
    ├── TrajectoryCanvas.cs   # SkiaSharp trajectory renderer
    ├── ScalarRingStack.cs    # SkiaSharp ring renderer
    ├── EigenSpectrumView.cs  # SkiaSharp eigen bars
    └── PlaybackControl.xaml  # Time scrubber
```

## Data Format

The app loads geometry exports from ScalarScope (`.json` files). See [scalarscope/export/geometry_export.py](https://github.com/mcp-tool-shop-org/ScalarScope/blob/main/src/scalarscope/export/geometry_export.py) for the schema.

Key sections:
- `run_metadata` - Condition, seed, conscience tier
- `trajectory` - State vectors, velocities, curvature
- `scalars` - Token dimension values over time
- `geometry` - Eigenvalues and anisotropy
- `evaluators` - Professor vectors for alignment visualization
- `failures` - Detected issues during training

## Building

```bash
# Prerequisites
# - .NET 9.0 SDK
# - Visual Studio 2022 or VS Code with MAUI workload

# Restore and build
cd src/ScalarScope
dotnet restore
dotnet build

# Run (Windows)
dotnet run -f net9.0-windows10.0.19041.0
```

## Usage

1. Launch the app
2. Click "Load Geometry Run" on the Overview tab
3. Select an ScalarScope geometry export (`.json`)
4. Use the tabs to explore different visualizations
5. Use the playback controls to scrub through training time

## Scientific Background

This visualizer is designed around the key finding from ScalarScope research:

> **Evaluative internalization is possible if and only if evaluators share a latent evaluative manifold.**

The visualizations make this concrete:

| Regime | Evaluator Structure | Visual Signature | Transfer Outcome |
|--------|---------------------|------------------|------------------|
| Path A | Orthogonal professors | Chaotic trajectory, multiple eigen bars | Transfer fails |
| Path B | Correlated professors | Clean spiral, dominant eigen bar | Transfer succeeds |

## Related

- [ScalarScope (Python)](https://github.com/mcp-tool-shop-org/ScalarScope) - Core training framework
- [RESULTS_AND_LIMITATIONS.md](docs/RESULTS_AND_LIMITATIONS.md) - Full experimental results

## License

MIT License - See LICENSE file for details.
