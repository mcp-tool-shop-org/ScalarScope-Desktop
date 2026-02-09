# ScalarScope Microsoft Store Listing

**Store ID:** 9P3HT1PHBKQK  
**Package Family Name:** mcp-tool-shop.ScalarScope_yn6b8xqrexa5j  
**Publisher:** mcp-tool-shop  
**Version:** 2.0.0

---

## Short Description (200 characters max)

Compare inference optimization runs with scientific rigor. Measure convergence, latency, and throughput deltas. Export reproducible review bundles.

---

## Full Description

ScalarScope is a precision instrument for comparing machine learning inference runs. Whether you're optimizing TensorFlow-RT models, tuning ONNX deployments, or benchmarking PyTorch inference—ScalarScope gives you the scientific rigor to prove your optimizations work.

**Key Features:**

- **Compare Two Runs**: Load before/after inference traces and see exactly what changed. Convergence time, latency distribution, throughput gains—all measured with statistical confidence.

- **Delta Analysis (ΔTc, ΔF, ΔO)**: Canonical deltas fire when differences are scientifically significant. No false positives, no overclaiming. Every finding includes confidence intervals and guardrails.

- **Runtime Presets**: TFRT Runtime Preset automatically suppresses metrics that don't apply to inference workloads, so you only see what matters.

- **Reproducible Bundles**: Export your comparison as a cryptographic bundle (SHA-256 hash). Anyone can verify the results haven't been tampered with. Perfect for audits and peer review.

- **Review Mode**: Open bundles from colleagues without recomputing. The entire analysis is frozen and verified against the original hash.

**Privacy First:**  
ScalarScope collects no telemetry, sends no analytics, and stores all data locally. Your inference traces and comparisons never leave your machine unless you explicitly export them.

---

## Feature Bullets (5)

1. Compare inference runs with scientific rigor and confidence intervals
2. Canonical delta analysis (ΔTc convergence, ΔO variability, ΔF failures)
3. TFRT Runtime Preset for TensorFlow-RT optimization workflows
4. Reproducible bundles with cryptographic integrity verification
5. 100% offline—no telemetry, no cloud, no sign-in required

---

## Screenshots Required

### Screenshot 1: Welcome Page
**Description:** Clean landing page with "Compare Two Runs" and "Open Bundle" CTAs.
**Alt Text:** ScalarScope welcome screen showing primary actions

### Screenshot 2: Side-by-Side Comparison
**Description:** Two inference runs (Before/After) with timeline alignment.
**Alt Text:** Comparing baseline vs optimized inference runs with visual timeline

### Screenshot 3: Delta Tiles
**Description:** ΔTc (convergence) and ΔO (variability) deltas with values.
**Alt Text:** Delta analysis showing significant improvements

### Screenshot 4: Why Panel
**Description:** Expanded "Why did this fire?" panel showing trigger conditions.
**Alt Text:** Detailed explanation of why a delta was flagged as significant

### Screenshot 5: Review Mode
**Description:** Banner showing bundle hash and review mode status.
**Alt Text:** Viewing a reproducible bundle in review mode

### Screenshot 6: Export Dialog
**Description:** Bundle export with hash preview and export options.
**Alt Text:** Exporting comparison as reproducible bundle

### Screenshot 7: Interpretation Guide
**Description:** Help page with delta glossary and examples.
**Alt Text:** Interpretation guide explaining delta meanings

### Screenshot 8: Settings & About
**Description:** About section showing version, privacy, and bundle hash explanation.
**Alt Text:** Settings page with privacy statement

---

## QA Runbook (Final Pre-Release)

### Install Flow
1. [ ] Download from Microsoft Store (9P3HT1PHBKQK)
2. [ ] Launch ScalarScope
3. [ ] Verify Welcome page appears with CTAs
4. [ ] Check version shows 2.0.0 in Settings > About

### Core Workflow
5. [ ] Click "Compare Two Runs"
6. [ ] Load baseline TFRT trace (JSON)
7. [ ] Load optimized TFRT trace (JSON)
8. [ ] Verify side-by-side display
9. [ ] Verify deltas appear (ΔTc should fire)
10. [ ] Click delta tile → Why panel opens
11. [ ] Click "Show Me" → scrolls to anchor

### Export Flow
12. [ ] Open Bundle Export panel
13. [ ] Click "Export Bundle"
14. [ ] Verify .scsbundle file created
15. [ ] Copy bundle hash

### Review Flow
16. [ ] Click "Open Review Bundle" on Welcome page
17. [ ] Select .scsbundle file
18. [ ] Verify Review Mode banner appears
19. [ ] Verify Load buttons are disabled
20. [ ] Verify bundle hash matches original

### Settings & About
21. [ ] Open Settings tab
22. [ ] Verify About section visible
23. [ ] Verify privacy statement present
24. [ ] Test "Report Issue" link opens GitHub

### Uninstall
25. [ ] Uninstall from Windows Settings
26. [ ] Verify clean removal (no leftover files)

---

## Categories

- **Primary:** Developer tools
- **Secondary:** Productivity

## Age Rating

- **ESRB:** Everyone
- **PEGI:** 3

## System Requirements

- **OS:** Windows 10 (19041) or later
- **Architecture:** x64
- **RAM:** 4 GB minimum
- **Disk:** 100 MB

---

## Release Notes (v2.0.0)

### What's New

**Phase H: Humanizing & UI/UX Finish**
- New Welcome page with streamlined first-60-seconds experience
- Unified design system (typography, spacing, colors)
- Comprehensive About section with privacy statement
- Bundle hash explanation for non-technical users

**Inference Optimization Focus**
- TFRT Runtime Preset with automatic delta suppression
- RunTrace comparison with milestone alignment
- Scientific rigor: confidence intervals, guardrails, reproducibility

**Quality of Life**
- Recent comparisons on Welcome page
- Designed empty states throughout
- Reduced motion mode respects system preferences
- No telemetry pledge clearly stated

---

## Contact

- **Support:** https://github.com/mcp-tool-shop/ScalarScope/issues
- **Documentation:** https://github.com/mcp-tool-shop/ScalarScope/blob/main/docs/README.md
- **Privacy Policy:** Local-only app, no data collection
