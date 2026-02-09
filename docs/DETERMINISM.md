# ScalarScope Determinism Guarantees

This document describes ScalarScope's reproducibility guarantees and how to verify them.

## Overview

ScalarScope v1.4+ provides **deterministic delta computation**: identical inputs will always produce identical outputs, enabling:

- Independent verification of results
- Reproducible research workflows
- Audit-compatible analysis

## Determinism Scope

### Guaranteed Deterministic

| Component | Behavior |
|-----------|----------|
| Delta Detection | Same runs + alignment + time → same deltas |
| Delta Ordering | Canonical order by causal salience |
| Confidence Values | Computed from input data only |
| Explanations | Template-based, data-driven |
| Export Content | Fully reproducible given same inputs |

### Not Guaranteed Deterministic

| Component | Reason |
|-----------|--------|
| UI Layout | Platform/window size dependent |
| Animation Timing | Frame-rate dependent |
| Timestamps | Based on wall clock |
| File Paths | User/system dependent |

## How It Works

### Input Fingerprinting

Every comparison generates an **input fingerprint** based on:

```
SHA256(leftRunId | rightRunId | alignmentMode | leftSteps | rightSteps)
```

This fingerprint is:
- Truncated to 16 characters for readability
- Stored in exports for verification
- Used to derive deterministic seeds when needed

### Delta Hashing

Each computation generates a **delta hash** based on:

```
SHA256(JSON([{id, status, confidence, explanation}, ...]))
```

This hash uniquely identifies the output and can be compared across runs.

## Verification Process

### Manual Verification

1. Run the same comparison on two machines
2. Compare the `DeltaHash` values from exports
3. Hashes should match exactly

### Programmatic Verification

```csharp
// Get the expected hash from a previous run
var expectedHash = previousResult.DeltaHash;

// Compute new result
var newResult = CanonicalDeltaService.ComputeDeltasWithAlignment(...);

// Verify
var verification = DeterminismService.VerifyDeterminism(
    expectedHash, 
    newResult.Deltas);

Console.WriteLine(verification.IsMatch 
    ? "✓ Results are identical" 
    : "✗ Results differ");
```

## Reproducibility Metadata

Every export includes reproducibility metadata:

```json
{
  "reproducibility": {
    "isDeterministic": true,
    "seed": null,
    "inputFingerprint": "A1B2C3D4E5F67890",
    "version": "1.4.0",
    "timestamp": "2026-02-08T12:00:00Z"
  },
  "deltaHash": "F0E1D2C3B4A59687"
}
```

## Best Practices

### For Researchers

1. **Record fingerprints**: Save the `InputFingerprint` with your notes
2. **Archive exports**: Keep exports for verification later
3. **Version tracking**: Note the ScalarScope version used

### For Auditors

1. **Request exports**: Ask for the original export files
2. **Verify hashes**: Compare `DeltaHash` values
3. **Check versions**: Ensure same ScalarScope version

### For CI/CD

1. **Pin versions**: Use specific ScalarScope versions
2. **Hash assertions**: Compare delta hashes in tests
3. **Snapshot testing**: Store expected outputs for regression

## Troubleshooting

### Hash Mismatch

If hashes don't match, check:

1. **Input files**: Are they byte-identical?
2. **Alignment mode**: Is it the same?
3. **ScalarScope version**: Different versions may have different algorithms
4. **Time parameter**: Was the same time value used?

### Floating Point Variance

ScalarScope handles floating-point determinism by:

- Using consistent rounding in comparisons
- Avoiding platform-specific math functions where possible
- Documenting any known variance sources

## Version History

| Version | Determinism Changes |
|---------|---------------------|
| 1.4.0 | Initial determinism guarantees |
| 1.3.x | Not guaranteed deterministic |

## Contact

For reproducibility issues, include:
- Export files from both runs
- ScalarScope version
- Operating system and .NET version
- Steps to reproduce
