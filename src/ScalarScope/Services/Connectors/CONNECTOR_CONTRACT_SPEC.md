# RunConnectorContract v1.0 Specification

## Overview

The RunConnectorContract defines a universal interface for importing training run data into ScalarScope from any external source. This enables "implement contract + preset = integration" for any ML platform.

## Design Principles

1. **Stream-friendly**: All data structures support incremental/streaming ingestion
2. **Capability-based**: Connectors declare what they can provide; UI adapts
3. **Preset-driven**: Signal mapping is configurable, not hard-coded
4. **Determinism-preserving**: Fingerprints flow through for reproducibility
5. **Error-first**: Validation uses Phase 6 error explanations

---

## 1. RunTrace Schema (Canonical Format)

Every connector produces a `RunTrace` - the universal intermediate representation.

```json
{
  "traceVersion": "1.0.0",
  "metadata": {
    "runId": "uuid",
    "label": "experiment-42-lr-sweep",
    "source": "tensorboard",
    "sourceVersion": "2.15.0",
    "createdUtc": "2026-02-09T12:00:00Z",
    "fingerprints": {
      "code": "sha256:abc123...",
      "dataset": "sha256:def456...",
      "model": "sha256:...",
      "seed": 42
    },
    "tags": { "team": "ml-core", "experiment": "lr-sweep" }
  },
  "capabilities": ["scalars", "milestones", "artifacts"],
  "timeline": {
    "stepCount": 10000,
    "stepUnit": "iteration",
    "wallClockStart": "2026-02-09T10:00:00Z",
    "wallClockEnd": "2026-02-09T11:30:00Z",
    "wallClockPerStep": [0.0, 0.54, 1.08, ...]  // optional, seconds from start
  },
  "scalars": {
    "train/loss": { "steps": [0,1,2,...], "values": [2.3, 2.1, 1.9, ...] },
    "train/accuracy": { "steps": [0,1,2,...], "values": [0.1, 0.15, 0.2, ...] },
    "eval/loss": { "steps": [100,200,...], "values": [2.0, 1.8, ...] },
    "learning_rate": { "steps": [0,1,2,...], "values": [0.001, 0.001, ...] }
  },
  "milestones": [
    { "step": 0, "type": "epoch", "index": 0 },
    { "step": 1000, "type": "epoch", "index": 1 },
    { "step": 1000, "type": "checkpoint", "path": "checkpoints/step-1000.pt", "hash": "sha256:..." },
    { "step": 500, "type": "eval", "index": 0 }
  ],
  "signals": {
    "curvature": { "steps": [...], "values": [...] },
    "eigenSpectrum": { "steps": [...], "values": [[λ1,λ2,λ3], ...] },
    "evaluatorVectors": { "steps": [...], "values": [...] }
  },
  "artifacts": [
    { "step": 1000, "type": "checkpoint", "path": "...", "hash": "sha256:...", "bytes": 123456 },
    { "step": 5000, "type": "model", "path": "...", "hash": "sha256:..." }
  ]
}
```

---

## 2. Connector Capabilities

Connectors declare their capabilities. ScalarScope UI adapts based on what's available.

| Capability | Description | Required Sections |
|------------|-------------|-------------------|
| `scalars` | Basic scalar metrics | `scalars` |
| `milestones` | Epoch/eval/checkpoint markers | `milestones` |
| `artifacts` | Checkpoint/model file references | `artifacts` |
| `wallClock` | Wall-clock timing per step | `timeline.wallClockPerStep` |
| `curvature` | Curvature proxy signals | `signals.curvature` |
| `spectrum` | Eigenvalue spectrum | `signals.eigenSpectrum` |
| `evaluators` | Evaluator alignment vectors | `signals.evaluatorVectors` |
| `streaming` | Supports live append-only ingestion | (runtime flag) |

**Minimum viable**: `scalars` only. Everything else is progressive enhancement.

---

## 3. ConnectorPreset (Signal Mapping)

Maps external metric names → ScalarScope signal roles.

```json
{
  "presetId": "tensorboard-pytorch-default",
  "presetVersion": "1.0.0",
  "source": "tensorboard",
  "mappings": {
    "learningSignal": {
      "primary": "train/loss",
      "fallbacks": ["loss", "training_loss", "train_loss"]
    },
    "evaluationSignal": {
      "primary": "eval/loss",
      "fallbacks": ["val/loss", "validation_loss", "eval_loss"]
    },
    "curvatureSignal": {
      "primary": "curvature_proxy",
      "fallbacks": ["grad_norm", "gradient_norm"]
    },
    "spectrumSignal": {
      "primary": "eigen/lambda",
      "fallbacks": ["hessian_eigenvalues"]
    }
  },
  "milestoneMapping": {
    "epoch": ["epoch", "epochs"],
    "eval": ["eval", "evaluation", "validation"],
    "checkpoint": ["checkpoint", "ckpt", "save"]
  },
  "normalization": {
    "lossScale": "auto",
    "stepOffset": 0
  }
}
```

**Resolution order**: primary → fallbacks → null (signal unavailable)

---

## 4. IRunConnector Interface

```csharp
public interface IRunConnector
{
    /// <summary>Connector identifier (e.g., "tensorboard", "mlflow").</summary>
    string ConnectorId { get; }
    
    /// <summary>Human-readable name.</summary>
    string DisplayName { get; }
    
    /// <summary>Capabilities this connector can provide.</summary>
    ConnectorCapabilities Capabilities { get; }
    
    /// <summary>Check if a source path/URI is valid for this connector.</summary>
    Task<ConnectorProbeResult> ProbeAsync(string source, CancellationToken ct = default);
    
    /// <summary>Import a complete run trace from source.</summary>
    Task<RunTrace> ImportAsync(string source, ConnectorOptions options, CancellationToken ct = default);
    
    /// <summary>Begin streaming import (for live connectors).</summary>
    IAsyncEnumerable<RunTraceUpdate> StreamAsync(string source, ConnectorOptions options, CancellationToken ct = default);
}
```

---

## 5. Alignment Modes for Training Runs

| Mode | When to Use | Description |
|------|-------------|-------------|
| `Step` | Same-length runs | Align by training step/iteration |
| `ConvergenceOnset` | Different schedules | Align when signal stabilizes |
| `EvalMilestone` | Different step counts, same eval cadence | Align by eval index |
| `Epoch` | Different batch sizes | Align by epoch boundary |

Default selection heuristic:
1. If `stepCount` matches ±5%: use `Step`
2. If `milestones` have matching eval count: use `EvalMilestone`
3. Else: use `ConvergenceOnset`

---

## 6. Validation & Error Handling

Connectors use the Phase 6 error explanation system:

| Error Code | Description |
|------------|-------------|
| `CONNECTOR_SOURCE_NOT_FOUND` | Source path/URI doesn't exist |
| `CONNECTOR_FORMAT_INVALID` | File format not recognized |
| `CONNECTOR_NO_SCALARS` | No scalar metrics found |
| `CONNECTOR_SIGNAL_MAPPING_FAILED` | Preset couldn't map required signals |
| `CONNECTOR_INCOMPLETE_TRACE` | Trace missing required fields |
| `CONNECTOR_STREAMING_INTERRUPTED` | Live stream disconnected |

---

## 7. Phase C1 Deliverables

- [x] `RunTrace` record model with all sections
- [x] `ConnectorCapabilities` flags enum  
- [x] `ConnectorPreset` model and validation
- [x] `IRunConnector` interface
- [x] `ConnectorRegistry` for discovering connectors
- [x] `ConnectorValidationService` with error explanations
- [x] JSON Schema for RunTrace v1.0.0

---

## 8. Roadmap

| Phase | Name | Scope |
|-------|------|-------|
| C1 | Universal Connector Contract | This spec + SDK infrastructure |
| C2 | Offline Connectors | TensorBoard, MLflow, W&B, CSV/JSON |
| C3 | Live Streaming | WebSocket/pipe ingestion, live badge |
| C4 | Framework Hooks | PyTorch, Lightning, HF, Keras, JAX |
| C5 | Enterprise Governance | Auth, redaction, provenance, audit |
