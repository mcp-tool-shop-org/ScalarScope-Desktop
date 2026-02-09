# ScalarScope Quick Reference

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `←` `→` | Step backward / forward |
| `Shift+←` `Shift+→` | Fine step (0.1%) |
| `Home` | Jump to start |
| `End` | Jump to end |
| `+` `-` | Speed up / down |
| `S` | Quick screenshot |
| `Ctrl+S` | Quick screenshot |
| `Ctrl+E` | Quick screenshot (export) |
| `1-6` | Switch tabs |

## Tabs (v2.0)

| Tab | Purpose |
|-----|--------|
| **Home** | Welcome page, recent comparisons, quick actions |
| **Compare** | Side-by-side Path A vs Path B, delta analysis |
| **Guide** | Interpretation help, delta glossary |
| **Settings** | Preferences, About, privacy info |

## Visual Vocabulary

### Trajectory View
- **Spiral inward** → Attractor formation (good)
- **Wandering/chaotic** → No convergence (bad)
- **Orange regions** → High curvature (phase transitions)
- **Arrow vectors** → Professor evaluation directions

### Scalar Rings
- **Phase-locked** → Shared latent axis
- **Drifting** → Orthogonal evaluators

### Eigenvalue Spectrum
- **λ₁ > 80%** → Strong shared axis ✓
- **λ₁ 50-80%** → Partial unification
- **λ₁ < 50%** → Orthogonal evaluators ✗

### Effective Dimension
- **Low (1-2)** → Stable conscience
- **High (3+)** → Fragmented evaluation

## Annotation Types

| Color | Type | Meaning |
|-------|------|---------|
| 🟣 Purple | Phase Labels | Dimensional transitions |
| 🟠 Orange | Curvature Warnings | Phase transitions |
| 🔵 Cyan | Eigen Insights | Latent structure status |
| 🔴 Red | Failure Markers | Detected issues |

## Failure Severity

| Marker | Severity |
|--------|----------|
| Large red | Critical |
| Medium orange | Warning |
| Small yellow | Info |

## Export Options

- **Quick Screenshot** (`S`) → 1920×1080 PNG
- **HD Export** → 1920×1080 with options
- **4K Export** → 3840×2160
- **Frame Sequence** → PNG sequence for video

Export location: `Documents/ScalarScope Exports/`

## The Core Theorem

> **Evaluative internalization is possible if and only if evaluators share a latent evaluative manifold.**

### Path A (Orthogonal)
- Professor correlation ≈ 0
- No shared axis possible
- Transfer fails

### Path B (Correlated)
- Professor correlation ≈ 0.87
- Shared latent axis emerges
- Transfer succeeds (r ≈ 0.91)

## File Format

ScalarScope reads geometry exports (`.json`) from aspire-engine:

```
{
  "schema_version": "1.0",
  "run_metadata": { ... },
  "trajectory": { "timesteps": [...] },
  "scalars": { "values": [...] },
  "geometry": { "eigenvalues": [...] },
  "evaluators": { "professors": [...] },
  "failures": [...]
}
```

Generate with: `python -m aspire.export.geometry_export`

---

*ScalarScope v1.0 | Scientific instrument for evaluative learning dynamics*
