# Changelog

All notable changes to ScalarScope will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.4] - 2026-02-08

### Added
- **Recent Files**: Quick access to up to 10 recently opened training runs from the Overview tab
- **Keyboard Shortcut**: Added `Ctrl+E` as an additional export shortcut (alongside existing `S` and `Ctrl+S`)
- **Fine Step Control**: `Shift+Left/Right` for 0.1% precision stepping (documented)
- **Tab Navigation**: `1-6` keyboard shortcuts for switching tabs (now displayed in UI)

### Fixed
- **CI Build**: Fixed GitHub Actions workflow targeting wrong .NET framework (`net10.0` → `net9.0`)
- **Session Recovery**: File path now correctly stored for crash recovery (was using object.ToString())

### Changed
- VortexKit library promoted from `1.0.0-rc.1` to `1.0.0` stable release
- Updated keyboard shortcuts documentation and UI display

## [1.0.1.1] - 2026-02-04

### Added
- **Phase 4: Instrument Readiness & Trust**
  - InvariantGuard service for runtime assertions (soft-fail in release, hard-fail in debug)
  - ConsistencyCheckService for centralized metric calculations
  - GoldenRunService for regression testing via golden snapshots
  - Cross-view consistency verification (eigenvalue interpretations match everywhere)

### Changed
- DemoService now uses multi-path fallback for bundled file loading (better MAUI deployment compatibility)
- Version bump to 1.0.1.1 for Microsoft Store resubmission (cannot reuse version numbers)

### Fixed
- Bundled demo files now load correctly across all MAUI deployment scenarios

## [1.0.0-rc.1] - 2025-02-04

### Added
- **Trajectory Visualization**: Real-time 2D training trajectory playback with GPU-accelerated SkiaSharp rendering
- **Side-by-Side Comparison**: Compare two training runs with synchronized playback
- **Annotation System**: Automatic detection and display of:
  - Phase transitions (dimensional shifts)
  - Curvature warnings (instability indicators)
  - Eigenvalue insights (λ₁ dominance analysis)
  - Failure markers (severity-coded)
- **Export Capabilities**:
  - Single frame PNG export (up to 4K)
  - Frame sequence export for video/GIF creation
  - Comparison exports with side-by-side layout
- **Failures Timeline**: Dedicated view for analyzing failure events with severity coding
- **Comparison Analytics Panel**: Statistical comparison with automated verdict
- **Playback Controls**:
  - Play/Pause, step forward/backward
  - Variable speed (0.25x to 4x)
  - Keyboard shortcuts (Space, Arrow keys, +/-)
- **VortexKit Library**: Reusable visualization framework extracted for future projects

### Documentation
- Demo script for 5-minute walkthrough
- Quick reference card for keyboard shortcuts
- Paper companion section for publication appendix

### Technical
- .NET 9.0 + .NET MAUI for cross-platform desktop
- SkiaSharp 3.x for high-performance 2D rendering
- CommunityToolkit.Mvvm for MVVM architecture
- Support for both light and dark themes

## [0.1.0] - 2025-01-15

### Added
- Initial project scaffold
- Basic MAUI shell structure
- Core data models for training runs
