---
title: Bundles & Review
description: Reproducible exports and review mode.
sidebar:
  order: 3
---

## Reproducible bundles

Export comparison results as `.scbundle` archives (ComparisonBundle v1.0.0). Bundles are self-contained and cryptographically verified.

### Bundle contents

| File | Purpose |
|------|---------|
| `manifest.json` | Bundle metadata, app version, comparison labels, alignment mode |
| `repro/repro.json` | Input fingerprints, preset hash, determinism seed, environment info |
| `findings/deltas.json` | Canonical deltas with confidence scores, anchors, and trigger types |
| `findings/why.json` | Human-readable explanations, guardrails, parameter chips |
| `findings/summary.md` | Auto-generated Markdown summary |

### Integrity

Every file in the bundle is hashed with SHA-256. A bundle-level hash enables tamper detection. If any file has been modified since export, the integrity check fails.

## Review mode

Open any `.scbundle` without recomputing:

- Results are cryptographically verified against the embedded hashes
- Frozen deltas are displayed exactly as they were at export time
- A review-mode banner makes it clear results are verified, not re-derived
- Both parties see identical results when sharing bundles

Review mode is read-only — you cannot modify the bundle contents from within the app.

## Export workflow

1. Complete a comparison in the **Compare** tab
2. Click **Export Bundle**
3. Choose a location for the `.scbundle` file
4. Share the file with your team
5. Recipients open it in ScalarScope — Review mode activates automatically
