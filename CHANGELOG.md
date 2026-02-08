# Changelog

All notable changes to ScalarScope will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.8] - 2026-02-08

### Added
- **Settings Page**: New dedicated tab for configuring application preferences
  - Theme selection (System / Light / Dark)
  - High contrast mode toggle
  - Reduce animations toggle (accessibility)
  - Annotation density (Minimal / Standard / Full)
  - Default playback speed selection
  - Auto-play on load toggle
  - Auto-load last session toggle
  - Configurable recent files limit (5 / 10 / 20)
  - Default export folder selection
  - Default export resolution
  - Reset demo / first-run state
  - Reset all settings to defaults
- **Theme Persistence**: Selected theme is saved and restored on app startup
- **Accessibility**: `SemanticProperties` added to all Settings page controls for screen readers

### Changed
- UserPreferencesService expanded with 15+ new settings methods
- Recent files limit now user-configurable (was hardcoded to 10)

## [1.0.7] - 2026-02-08

### Changed
- **MVVM AOT Compatibility**: Migrated all `[ObservableProperty]` fields to partial property syntax for WinRT/AOT compatibility
  - ComparisonViewModel (15 properties)
  - VortexSessionViewModel (13 properties)
  - TrajectoryPlayerViewModel (10 properties)
  - ExportViewModel (14 properties)
  - VortexKit PlaybackController (6 properties)
- **Frame → Border Migration**: Replaced all deprecated `Frame` XAML elements with modern `Border` controls
  - Migrated 30+ Frame elements across 4 XAML files
  - ExportPanel, RecoveryPage, OverviewPage, HelpPage
  - `BorderColor` → `Stroke`, `CornerRadius` → `StrokeShape="RoundRectangle X"`

### Technical
- Eliminates 104+ MVVM Toolkit source generator AOT warnings
- Future-proofs XAML for .NET 9+ where Frame is deprecated
- Zero functional changes, pure technical debt reduction

## [1.0.6] - 2026-02-08

### Changed
- **SkiaSharp API Modernization**: Migrated all text rendering from deprecated `SKPaint.TextSize`, `SKPaint.TextAlign`, and `SKCanvas.DrawText(string, float, float, SKPaint)` to modern `SKFont`-based APIs
  - All controls now use `SKFont` for text size and typeface
  - `DrawText` calls updated to use 6-parameter overload with `SKTextAlign` and `SKFont`
  - `MeasureText` calls updated to use `SKFont.MeasureText(string, SKPaint)`
- Reduced SkiaSharp deprecation warnings from 170+ to 0 (text API related)

### Technical
- Future-proofs codebase for SkiaSharp 4.x compatibility
- Files updated: TrajectoryCanvas, ComparisonTrajectoryCanvas, ComparisonAnalyticsPanel, FailuresTimeline, EigenSpectrumView, ScalarRingStack, AnnotationOverlay, LoadingOverlay, ExportService

## [1.0.5] - 2026-02-08

### Added
- **Drag-and-Drop**: Drag JSON files directly onto the Overview page to load training runs
- **Hover Tooltips**: Hover over trajectory points to see real-time values (time, position, velocity)
- **Zoom & Pan**: Mouse wheel zoom and right-click drag to pan on trajectory canvas
- **Loading Shimmer**: Animated loading overlay with progress messages when opening files
- **Reset View Button**: One-click reset for zoom/pan state in trajectory view

### Changed
- TrajectoryCanvas now supports touch/pointer events for interactive exploration
- GeometryRun model enhanced with computed velocity magnitude property
- Refactored session loading to expose loading state via observable properties

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
