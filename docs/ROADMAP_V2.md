# ScalarScope v2.0 Roadmap

**Vision**: Transform ScalarScope from a visualization tool into the definitive instrument for understanding training dynamics - where every pixel tells a story about learning.

---

## Phase 1: Vortex Core (v1.2 - v1.3) ✅
*Make the vortex visualization world-class*

### 1.1 Organic Flow Rendering
- [x] **Catmull-Rom Spline Interpolation**: Replace line segments with smooth, organic curves
- [x] **Adaptive Stroke Width**: Thickness varies with velocity (faster = thinner, like ink brush)
- [x] **Trail Opacity Decay**: History fades naturally, recent path emphasized
- [ ] **Anti-aliased Rendering**: Subpixel accuracy for publication-quality output

### 1.2 Energy Visualization  
- [x] **Glow/Bloom Effects**: Radial luminance around high-energy regions
- [x] **Heat Map Overlay**: Optional density heatmap showing where trajectory lingers (v1.3)
- [x] **Curvature Halos**: Already implemented via existing curvature markers
- [x] **Color Mode Selection**: Time, velocity, or curvature-based coloring (v1.3)

### 1.3 Flow Field Context
- [x] **Vector Field Grid**: Optional background showing gradient directions (v1.3)
- [ ] **Streamlines**: Show surrounding flow field, not just the taken path
- [ ] **Attractor/Repeller Markers**: Identify fixed points in the dynamics

---

## Phase 2: Publication Ready (v1.4)
*Export quality that rivals hand-crafted figures*

### 2.1 SVG Export Engine ✅
- [x] **Full Vector Export**: Infinite resolution for any zoom level
- [x] **Layer Separation**: Trajectory, annotations, grid as separate SVG groups
- [x] **Inkscape Metadata**: Embed layer names and semantic grouping
- [x] **Custom Color Palettes**: Export with user-defined or journal-specific colors

### 2.2 High-Resolution Raster ✅
- [x] **8K Export**: Up to 7680×4320 for large format printing
- [x] **Transparent Background**: PNG-alpha for compositing
- [x] **Batch Export**: Generate figure sets for multiple runs automatically
- [x] **PDF/EPS Output**: Direct export for LaTeX workflows

### 2.3 Animation Export ✅
- [x] **GIF Generator**: Smooth animated GIFs with configurable FPS
- [x] **MP4/WebM Export**: High-quality video for presentations (requires FFmpeg)
- [x] **Frame-by-Frame SVG**: Animated SVG for web embedding

---

## Phase 3: Deep Analysis (v1.4)
*Turn visualization into insight*

### 3.1 Quantitative Overlays ✅
- [x] **Eigenvalue Timeline**: Synchronized λ₁/λ₂ plot below trajectory
- [x] **Lyapunov Exponent Estimation**: Real-time chaos indicator with classification
- [x] **Phase Portrait Markers**: Bifurcation detection and annotation
- [x] **Dimension Collapse Alerts**: Highlight when effective dimension drops

### 3.2 Comparative Analysis ✅
- [x] **Overlay Mode**: Multiple trajectories in same view with transparency
- [x] **Deviation Highlighting**: Show where runs diverge
- [x] **Statistical Bands**: Confidence intervals from multiple seeds
- [x] **Distance Metrics**: Quantify trajectory similarity (DTW, Fréchet)

### 3.3 Interactive Probing ✅
- [x] **Time Travel**: Click anywhere on trajectory to see full state at that moment
- [x] **Gradient Inspector**: Show what direction training wanted to go vs. where it went
- [x] **"What-If" Markers**: Annotate hypothetical alternative paths

---

## Phase 4: Immersive Experience (v1.5)
*Make exploration intuitive and delightful*

### 4.1 Fluid Navigation
- [ ] **Inertial Pan/Zoom**: Physics-based scrolling with momentum
- [ ] **Pinch-to-Zoom**: Native touch gestures
- [ ] **Minimap**: Small overview showing current viewport position
- [ ] **Bookmark System**: Save interesting positions/times for later

### 4.2 Real-Time Playback
- [ ] **Particle System**: Trail of particles following the trajectory
- [ ] **Motion Blur**: Temporal smoothing during fast playback
- [ ] **Beat Sync**: Option to sync playback speed to audio tempo (for presentations)
- [ ] **Cinematic Modes**: Pre-programmed camera paths for demos

### 4.3 Accessibility
- [ ] **Colorblind Palettes**: Perceptually uniform alternatives
- [ ] **Screen Reader Narration**: Describe trajectory shape and events
- [ ] **High Contrast Mode**: Maximum visibility themes
- [ ] **Reduced Motion**: Static analysis mode for motion sensitivity

---

## Phase 5: Collaboration & Ecosystem (v2.0)
*Share, compare, and build community*

### 5.1 Cloud Features
- [ ] **Share Links**: Generate URLs to specific views/times
- [ ] **Embedding**: Iframe-compatible viewer for websites
- [ ] **Gallery**: Public collection of interesting trajectories
- [ ] **Comments**: Annotate specific points with notes

### 5.2 Plugin Architecture
- [ ] **Custom Analyzers**: User-defined metrics and detectors
- [ ] **Theme Packs**: Downloadable color schemes and styles
- [ ] **Export Plugins**: Additional format support via plugins
- [ ] **Render Pipelines**: Swap out rendering backends (WebGPU, etc.)

### 5.3 Integration
- [ ] **Weights & Biases**: Direct pull from W&B runs
- [ ] **TensorBoard**: Import TensorBoard scalars
- [ ] **MLflow**: Read from MLflow tracking server
- [ ] **Jupyter Widget**: Inline ScalarScope in notebooks

---

## Technical Milestones

| Version | Codename | Focus | Target |
|---------|----------|-------|--------|
| v1.2 | **Fluid** | Vortex rendering quality | +2 weeks |
| v1.3 | **Publish** | Export engine overhaul | +3 weeks |
| v1.4 | **Insight** | Analysis tools | +3 weeks |
| v1.5 | **Flow** | UX and accessibility | +2 weeks |
| v2.0 | **Connected** | Cloud and collaboration | +4 weeks |

---

## Design Principles

1. **Every pixel earned**: No visual element without scientific meaning
2. **Publication-first**: Default output should be journal-ready
3. **Progressive disclosure**: Simple by default, deep on demand
4. **Performance budgets**: 60fps on integrated graphics, <100ms load times
5. **Offline-capable**: Full functionality without internet

---

## v1.2 Implementation Priority

Starting with the most impactful changes:

1. **Catmull-Rom splines** - Immediate visual upgrade, foundation for everything else
2. **Adaptive stroke width** - Adds visual energy encoding without complexity
3. **Trail opacity decay** - Better temporal context
4. **Glow effects** - Polish that makes it feel premium

These four changes alone will transform the visual quality and set the stage for the export engine work.

---

*Last updated: 2026-02-08*
