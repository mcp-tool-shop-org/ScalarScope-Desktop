---
title: Delta Analysis
description: The five canonical delta types and runtime presets.
sidebar:
  order: 2
---

Every comparison produces a set of canonical deltas. Each delta fires only when the difference is statistically meaningful — no noise, no false signals, no manual threshold tuning.

## Five canonical delta types

| Delta | Full Name | What It Measures | Fires When |
|-------|-----------|------------------|------------|
| **ΔTc** | Convergence Time | Steps to reach stable latency | Steady-state reached at different steps (3+ step separation) |
| **ΔO** | Output Variability | Oscillation / runtime instability | Area-above-threshold score differs beyond noise floor |
| **ΔF** | Failure Rate | Anomaly frequency | Failure frequency or kind differs between runs |
| **ΔĀ** | Average Latency | Mean metric value | Mean differs meaningfully |
| **ΔTd** | Total Duration | Wall-clock time / structural emergence | Duration or dominance onset differs |

## Runtime presets

### TFRT (TensorFlow-TRT)

The built-in TensorFlow-TRT preset (`tensorflowrt-runtime-v1`) is designed for inference comparison:

- Maps inference-specific signals: latency, throughput, memory, CPU/GPU load
- **Suppresses** training-only deltas (ΔĀ, ΔTd) that have no meaning for inference
- Warns when warmup exceeds 50% of the run
- Warns when only aggregated stats are available (not per-step traces)

This ensures your analysis stays focused on what actually changed between inference runs, not metrics that are irrelevant to the TFRT workload.

## How deltas are computed

Each delta type has its own detector with configurable thresholds. Deltas include:

- **Confidence score** — how certain the difference is meaningful
- **Anchors** — specific data points that triggered the delta
- **Trigger type** — what kind of signal caused the detection
- **Human-readable explanation** — auto-generated text describing the finding
