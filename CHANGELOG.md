# Changelog

All notable changes to ScalarScope will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-02-09

### Added - Phase H: Humanizing & UI/UX Finish

#### Gate H1: Welcome Experience
- **WelcomePage**: New landing page with first-60-seconds experience
- **Hero Section**: App icon, title, one-liner promise ("Compare inference runs with scientific rigor")
- **Trust Badge**: "Reproducible • Auditable • No telemetry"
- **Primary CTAs**: "Compare Two Runs" and "Open Review Bundle" buttons
- **Try Example Section**: Onboarding card for new users
- **Recent Comparisons**: Quick resume list with timestamp and metric summary

#### Gate H2: Design System
- **DesignSystem.xaml**: Unified visual grammar resource dictionary
- **Typography Scale**: Hero (32pt), Section (15pt), Body (14pt), Caption (11pt)
- **Spacing Rhythm**: 4/8/12/16/24/32/40px consistent spacing tokens
- **Status Chips**: Semantic colors (success/warning/error/neutral) with consistent styling
- **Card Patterns**: Subtle borders, corner radius, and hover states

#### Gate H3: Navigation Simplification
- **Reduced Tab Count**: 5 tabs → 4 (Home, Compare, Guide, Settings)
- **Merged Views**: Consolidated redundant pages for cleaner flow
- **Tab Bar Styling**: Unified colors and selection states

#### Gate H4: Settings & About Enhancement
- **Version Display**: Show version with commit hash (2.0.0+7887384)
- **Privacy Statement**: Added inline privacy explanation
- **Bundle Hash Explanation**: Help users understand cryptographic integrity
- **Quick Links**: Documentation, GitHub, Report Issue commands

#### Gate H5: Marketplace Presentation
- **STORE_LISTING.md**: Complete Microsoft Store presentation pack
- **Screenshots Guide**: 5 required screenshots with descriptions and alt text
- **Feature Bullets**: Optimized for Store discovery
- **Pre-Flight Checklist**: Submission readiness validation

### Added - Prep Phase: Publishing

#### Gate P1: Package Identity Lock
- **Version 2.0.0.0**: Synchronized across manifest and csproj
- **InformationalVersion**: Includes commit hash for traceability
- **Updated Description**: Reflects inference optimization focus

#### Gate P2: Dependencies Included
- **SelfContained=true**: Bundles full .NET 9 runtime
- **WindowsAppSDKSelfContained=true**: Bundles Windows App SDK
- **78MB Package**: Works on clean Windows 10 without prerequisites

#### Gate P3: Signing & MSIXUpload
- **Unsigned Package**: For Store upload (Store signs during ingestion)
- **SHA256 Hash**: 0292A6189C522DCEE7EA69F59A0E114ECC0747F23E3B2C5506E1DD11094FB5FE

#### Gate P4: Store Compliance
- **PRIVACY.md**: Privacy policy (local-only, no data collection)
- **Accessibility**: AutomationProperties on all interactive controls
- **Minimal Capabilities**: runFullTrust only (justified for MAUI desktop)

#### Gate P5: Dry Run
- **44/44 Tests Passing**: Fixture and determinism tests green
- **Final Artifact**: ScalarScope_v2.0.0.msixupload (78.42 MB)

### Technical
- `WelcomePage.xaml/.cs`: New landing experience with recent comparisons
- `WelcomeViewModel.cs`: Recent comparison tracking and navigation
- `DesignSystem.xaml`: Centralized styles, colors, typography
- `AppShell.xaml`: Simplified 4-tab navigation
- `SettingsPage.xaml`: Expanded About section with privacy and links
- `SettingsViewModel.cs`: OpenDocumentation, OpenGitHub, OpenIssue commands

## [1.4.0] - 2026-02-09

### Added - Phase 5: UI Polish & Release Hardening

#### Gate 5.1: Idle Calm
- **Demo State Service**: Orchestrates idle animations across components
- **Pulse Breathing**: Subtle glow animations reduced during active comparison
- **Respiration Hook**: Animation intensity responds to user activity

#### Gate 5.2: State Continuity & Transitions
- **Focus Preservation**: Single→Compare mode switch preserves scroll/focus state
- **Choreographed Navigation**: "Show me" commands animate smoothly to anchors
- **Loading Skeletons**: Unified PlotScaffold and DeltaList loading states
- **No-Flicker Policy**: LayoutDebouncer prevents rapid layout thrashing

#### Gate 5.3: Confidence ↔ Intensity
- **Confidence Tokens**: Tiered visual weight (High/Medium/Low/Negligible)
- **Glow Modulation**: Highlight intensity correlates with confidence level
- **Confidence Badges**: Color-coded badges (HIGH/MED/LOW/???) on deltas
- **Tooltip Prefixes**: "High confidence:", "Medium confidence:" in explanations
- **Accessibility Labels**: SemanticProperties and AutomationProperties

#### Gate 5.4: Shareability Polish
- **Screenshot Ready Options**: IncludeWatermark, IncludeLegend, IncludeConfidenceBadges
- **Copy Cards**: Plain-text, Markdown, and summary card formats for deltas
- **Export Branding**: Legend and watermark drawing in exports
- **Plain-Language Export**: Non-technical executive summaries for stakeholders
- **Social Card Templates**: Twitter (1200x628), LinkedIn (1200x627), Slide (1920x1080)

#### Gate 5.5: Release Hardening
- **Version Watermark**: VersionInfo service embeds version in all exports
- **CI Validation**: Enhanced pipeline with version tracking and build summaries
- **Error Boundary**: Graceful error handling with user-friendly messages
- **Performance Profiler**: Gate thresholds for frame time, delta calc, exports

### Technical
- `DemoStateService`: Singleton for coordinating idle animations
- `ConfidenceTokens`: Maps confidence values to visual weight
- `LayoutDebouncer`/`AxisRangeInterpolator`: Smooth layout transitions
- `DeltaCopyService`: Multi-format copy card generation
- `SocialCardService`: Social media-optimized image export
- `VersionInfo`: Assembly version metadata access
- `ErrorBoundary`: Safe execution wrappers with retry/timeout
- `PerformanceProfiler`: Timing metrics with percentile support

## [1.3.0] - 2026-02-08

### Added
- **Heat Map Overlay**: Optional density visualization showing where the trajectory lingers longest
  - Logarithmic scaling for better visualization of density variations
  - 7-color gradient from deep blue (sparse) to red (dense)
  - Toggle via `ShowHeatMap` property

- **Vector Field Grid**: Background arrows showing local flow directions
  - 12x12 grid with contextual velocity indicators
  - Muted styling to avoid distracting from main trajectory
  - Toggle via `ShowVectorField` property

- **Color Mode Selection**: Choose how trajectory color is determined
  - **Time mode** (default): Gradient from start (cyan) to end (red)
  - **Velocity mode**: Slow (blue) to fast (red) coloring
  - **Curvature mode**: Smooth (green) to sharp turns (orange)
  - Set via `ColorMode` property with `TrajectoryColorMode` enum

### Technical
- Added `TrajectoryColorMode` enum for trajectory coloring options
- Implemented `DrawHeatMap()` with 64x64 density grid and Gaussian-like visualization
- Implemented `DrawVectorFieldGrid()` with directional arrows
- Added color mode-aware trajectory rendering throughout glow and spline drawing

## [1.2.0] - 2026-02-08

### Added
- **Catmull-Rom Spline Smoothing**: Trajectory paths now render with smooth, organic curves instead of jagged line segments
- **Adaptive Stroke Width**: Line thickness varies inversely with velocity (faster motion = thinner lines, like calligraphy)
- **Trail Opacity Decay**: Older trajectory points fade naturally, with recent path emphasized
- **Glow Effects**: Multi-layered ambient glow around trajectory for enhanced visual depth and energy visualization
- **v2.0 Roadmap**: Added comprehensive roadmap document outlining the path to ScalarScope 2.0

### Technical
- Implemented Catmull-Rom spline interpolation with 8 subdivisions per segment for smooth curves
- Added velocity-normalized stroke width calculation (1.5px - 5px range)
- Added progressive opacity fade starting at 40% for trajectory history
- Multi-pass glow rendering with blur masks for realistic luminance

## [1.1.0] - 2026-02-08

### Added
- **Session Recovery**: Full crash recovery now restores playback position, current page, and playback state
  - Automatically reloads the file that was open when the app crashed
  - Resumes playback from exact position
  - Navigates to the page you were viewing

### Fixed
- **Zero-Warning Build**: Eliminated all 11 compiler warnings
  - CS0108: Added `new` keyword to intentionally hidden members (BackgroundColor, ScaleProperty)
  - CS0618: Migrated deprecated `Frame` to modern `Border` in DemoAnnotationBanner
  - CS1998: Removed unnecessary `async` modifier from synchronous methods
  - CS8602: Fixed potential null reference in TrajectoryCanvas

### Changed
- DemoAnnotationBanner now uses `Border` with `StrokeShape` instead of deprecated `Frame`

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
