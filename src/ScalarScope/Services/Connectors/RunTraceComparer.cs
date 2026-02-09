// RunTrace Comparer v1.0
// Before/After optimization comparison with scientific rigor.
// Handles alignment, fingerprint validation, delta computation, and review-only export.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services.Connectors;

#region Comparison Error Codes

/// <summary>
/// Error codes for RunTrace comparison.
/// </summary>
public static class ComparisonErrorCodes
{
    // Fingerprint validation
    public const string CMP_FINGERPRINT_DATASET_MISMATCH = "CMP_FINGERPRINT_DATASET_MISMATCH";
    public const string CMP_FINGERPRINT_CODE_MISMATCH = "CMP_FINGERPRINT_CODE_MISMATCH";
    public const string CMP_FINGERPRINT_UNEXPECTED_CHANGE = "CMP_FINGERPRINT_UNEXPECTED_CHANGE";
    
    // Alignment
    public const string CMP_ALIGNMENT_NO_ANCHOR = "CMP_ALIGNMENT_NO_ANCHOR";
    public const string CMP_ALIGNMENT_MISMATCHED_LENGTHS = "CMP_ALIGNMENT_MISMATCHED_LENGTHS";
    
    // Capability
    public const string CMP_CAPABILITY_MISMATCH = "CMP_CAPABILITY_MISMATCH";
    public const string CMP_NO_COMMON_SIGNALS = "CMP_NO_COMMON_SIGNALS";
    
    // Delta computation
    public const string CMP_DELTA_INVALID_INPUT = "CMP_DELTA_INVALID_INPUT";
    public const string CMP_DELTA_SUPPRESSED = "CMP_DELTA_SUPPRESSED";
}

#endregion

#region Comparison Intent

/// <summary>
/// Available comparison modes.
/// </summary>
public enum ComparisonMode
{
    /// <summary>Compare two RunTraces (before/after).</summary>
    RunTraceCompare,
    
    /// <summary>Compare against a baseline snapshot.</summary>
    BaselineCompare,
    
    /// <summary>Compare multiple runs for variance analysis.</summary>
    VarianceAnalysis
}

/// <summary>
/// Alignment strategy for comparison.
/// </summary>
public enum AlignmentMode
{
    /// <summary>Align by step index (default, but risky for different warmup).</summary>
    StepBased,
    
    /// <summary>Align by runtime milestone (recommended for inference).</summary>
    RuntimeMilestone,
    
    /// <summary>Align by wall clock time.</summary>
    WallClock
}

/// <summary>
/// Declares the intent for a comparison operation.
/// </summary>
public sealed record ComparisonIntent
{
    /// <summary>Comparison mode.</summary>
    public required ComparisonMode Mode { get; init; }
    
    /// <summary>Label for run A (e.g., "Baseline").</summary>
    public required string LabelA { get; init; }
    
    /// <summary>Label for run B (e.g., "Optimized").</summary>
    public required string LabelB { get; init; }
    
    /// <summary>Alignment mode.</summary>
    public required AlignmentMode Alignment { get; init; }
    
    /// <summary>Primary anchor for RuntimeMilestone alignment.</summary>
    public RuntimeMilestoneType? PrimaryAnchor { get; init; }
    
    /// <summary>Fallback anchor if primary not found.</summary>
    public RuntimeMilestoneType? FallbackAnchor { get; init; }
    
    /// <summary>Preset ID to apply (e.g., "tensorflowrt-runtime-v1").</summary>
    public required string PresetId { get; init; }
    
    /// <summary>User notes about this comparison.</summary>
    public string? Notes { get; init; }
    
    /// <summary>Create default intent for TFRT inference optimization comparison.</summary>
    public static ComparisonIntent TfrtOptimization(
        string? labelA = null,
        string? labelB = null,
        string? notes = null) => new()
    {
        Mode = ComparisonMode.RunTraceCompare,
        LabelA = labelA ?? "Baseline",
        LabelB = labelB ?? "Optimized",
        Alignment = AlignmentMode.RuntimeMilestone,
        PrimaryAnchor = RuntimeMilestoneType.SteadyStateStart,
        FallbackAnchor = RuntimeMilestoneType.WarmupEnd,
        PresetId = TfrtRuntimePreset.PresetId,
        Notes = notes
    };
}

#endregion

#region Fingerprint Comparison

/// <summary>
/// Result of comparing fingerprints between two runs.
/// </summary>
public sealed record FingerprintComparison
{
    /// <summary>Whether fingerprints are valid for comparison.</summary>
    public bool IsValidForComparison => !Issues.Any(i => i.Severity == ComparisonIssueSeverity.Error);
    
    /// <summary>List of fingerprint differences.</summary>
    public required IReadOnlyList<FingerprintDifference> Differences { get; init; }
    
    /// <summary>Comparison issues (errors prevent comparison).</summary>
    public required IReadOnlyList<ComparisonIssue> Issues { get; init; }
    
    /// <summary>Summary of changes.</summary>
    public string Summary => Differences.Count == 0
        ? "Fingerprints identical."
        : $"{Differences.Count} fingerprint difference(s): {string.Join(", ", Differences.Select(d => d.Category))}";
}

/// <summary>
/// Single fingerprint difference.
/// </summary>
public sealed record FingerprintDifference
{
    /// <summary>Category (model, dataset, code, environment).</summary>
    public required string Category { get; init; }
    
    /// <summary>Fingerprint from run A.</summary>
    public required string FingerprintA { get; init; }
    
    /// <summary>Fingerprint from run B.</summary>
    public required string FingerprintB { get; init; }
    
    /// <summary>Whether this difference is expected for optimization.</summary>
    public bool IsExpectedForOptimization { get; init; }
    
    /// <summary>Explanation for the difference.</summary>
    public string? Explanation { get; init; }
}

/// <summary>
/// Severity level for comparison issues.
/// </summary>
public enum ComparisonIssueSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Single comparison issue.
/// </summary>
public sealed record ComparisonIssue
{
    /// <summary>Error code.</summary>
    public required string Code { get; init; }
    
    /// <summary>Severity level.</summary>
    public required ComparisonIssueSeverity Severity { get; init; }
    
    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
    
    /// <summary>Optional context.</summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }
}

#endregion

#region Alignment Result

/// <summary>
/// Result of aligning two RunTraces.
/// </summary>
public sealed record AlignmentResult
{
    /// <summary>Whether alignment succeeded.</summary>
    public bool IsSuccess => AnchorStepA.HasValue && AnchorStepB.HasValue;
    
    /// <summary>Alignment mode used.</summary>
    public required AlignmentMode Mode { get; init; }
    
    /// <summary>Anchor step in run A.</summary>
    public int? AnchorStepA { get; init; }
    
    /// <summary>Anchor step in run B.</summary>
    public int? AnchorStepB { get; init; }
    
    /// <summary>Milestone type used as anchor (if RuntimeMilestone mode).</summary>
    public RuntimeMilestoneType? AnchorType { get; init; }
    
    /// <summary>Number of aligned steps.</summary>
    public required int AlignedStepCount { get; init; }
    
    /// <summary>Steps skipped in A (warmup).</summary>
    public required int SkippedStepsA { get; init; }
    
    /// <summary>Steps skipped in B (warmup).</summary>
    public required int SkippedStepsB { get; init; }
    
    /// <summary>Issues encountered during alignment.</summary>
    public required IReadOnlyList<ComparisonIssue> Issues { get; init; }
    
    /// <summary>Summary of alignment.</summary>
    public string Summary => IsSuccess
        ? $"Aligned {AlignedStepCount} steps using {Mode} (anchor: {AnchorType})"
        : $"Alignment failed: {Issues.FirstOrDefault()?.Message ?? "unknown error"}";
}

/// <summary>
/// An aligned pair of steps from two runs.
/// </summary>
public sealed record AlignedStep
{
    /// <summary>Index in aligned sequence (0-based).</summary>
    public required int Index { get; init; }
    
    /// <summary>Original step index in run A.</summary>
    public required int StepA { get; init; }
    
    /// <summary>Original step index in run B.</summary>
    public required int StepB { get; init; }
    
    /// <summary>Wall time in A (if available).</summary>
    public double? WallTimeA { get; init; }
    
    /// <summary>Wall time in B (if available).</summary>
    public double? WallTimeB { get; init; }
}

#endregion

#region Delta Result

/// <summary>
/// Computed delta between two runs.
/// </summary>
public sealed record ComparisonDelta
{
    /// <summary>Delta type (ΔTc, ΔO, ΔF, etc.).</summary>
    public required string DeltaType { get; init; }
    
    /// <summary>Signal used for this delta.</summary>
    public required string Signal { get; init; }
    
    /// <summary>Value in run A.</summary>
    public required double ValueA { get; init; }
    
    /// <summary>Value in run B.</summary>
    public required double ValueB { get; init; }
    
    /// <summary>Absolute difference (B - A).</summary>
    public double AbsoluteDifference => ValueB - ValueA;
    
    /// <summary>Relative difference ((B - A) / A).</summary>
    public double? RelativeDifference => ValueA != 0 ? (ValueB - ValueA) / ValueA : null;
    
    /// <summary>Confidence score (0-1).</summary>
    public required double Confidence { get; init; }
    
    /// <summary>Whether this delta fired (is significant).</summary>
    public required bool Fired { get; init; }
    
    /// <summary>Whether this delta was suppressed by preset.</summary>
    public bool IsSuppressed { get; init; }
    
    /// <summary>Human-readable interpretation.</summary>
    public string? Interpretation { get; init; }
    
    /// <summary>Notes from the preset.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Complete comparison result.
/// </summary>
public sealed record ComparisonResult
{
    /// <summary>Unique comparison ID.</summary>
    public required string ComparisonId { get; init; }
    
    /// <summary>When this comparison was computed.</summary>
    public required DateTimeOffset ComputedUtc { get; init; }
    
    /// <summary>Intent that drove this comparison.</summary>
    public required ComparisonIntent Intent { get; init; }
    
    /// <summary>Fingerprint comparison result.</summary>
    public required FingerprintComparison Fingerprints { get; init; }
    
    /// <summary>Alignment result.</summary>
    public required AlignmentResult Alignment { get; init; }
    
    /// <summary>Computed deltas.</summary>
    public required IReadOnlyList<ComparisonDelta> Deltas { get; init; }
    
    /// <summary>Warnings generated during comparison.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
    
    /// <summary>Delta spec version used.</summary>
    public required string DeltaSpecVersion { get; init; }
    
    /// <summary>Preset applied.</summary>
    public required string PresetId { get; init; }
    
    /// <summary>Whether the comparison is valid for analysis.</summary>
    public bool IsValid => Fingerprints.IsValidForComparison && Alignment.IsSuccess && Deltas.Any();
    
    /// <summary>Get deltas that fired.</summary>
    public IEnumerable<ComparisonDelta> FiredDeltas => Deltas.Where(d => d.Fired && !d.IsSuppressed);
    
    /// <summary>Get suppressed deltas.</summary>
    public IEnumerable<ComparisonDelta> SuppressedDeltas => Deltas.Where(d => d.IsSuppressed);
}

#endregion

#region Review-Only Bundle

/// <summary>
/// Review-only export mode.
/// </summary>
public enum ReviewExportMode
{
    /// <summary>Full review with deltas and fingerprints.</summary>
    Review,
    
    /// <summary>Audit mode with full provenance chain.</summary>
    Audit
}

/// <summary>
/// Review-only bundle for comparison results.
/// Disables recompute, preserves before/after labels.
/// </summary>
public sealed record ReviewOnlyBundle
{
    /// <summary>Bundle schema version.</summary>
    public const string SchemaVersion = "1.0.0";
    
    /// <summary>Schema version.</summary>
    public required string Version { get; init; }
    
    /// <summary>Bundle ID (SHA-256).</summary>
    public required string BundleHash { get; init; }
    
    /// <summary>When this bundle was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
    
    /// <summary>Export mode.</summary>
    public required ReviewExportMode ExportMode { get; init; }
    
    /// <summary>Original comparison result.</summary>
    public required ComparisonResult Comparison { get; init; }
    
    /// <summary>Fingerprint summary (not raw inputs).</summary>
    public required ReviewFingerprintSummary FingerprintSummary { get; init; }
    
    /// <summary>Whether this was reviewed from another bundle.</summary>
    public bool IsReviewedFromBundle { get; init; }
    
    /// <summary>Parent bundle hash (if reviewed from bundle).</summary>
    public string? ParentBundleHash { get; init; }
    
    /// <summary>Marker that recompute is disabled.</summary>
    public bool RecomputeDisabled => true;
}

/// <summary>
/// Fingerprint summary for review bundle (no raw inputs).
/// </summary>
public sealed record ReviewFingerprintSummary
{
    /// <summary>Run A label.</summary>
    public required string LabelA { get; init; }
    
    /// <summary>Run B label.</summary>
    public required string LabelB { get; init; }
    
    /// <summary>Model fingerprint A.</summary>
    public required string ModelFingerprintA { get; init; }
    
    /// <summary>Model fingerprint B.</summary>
    public required string ModelFingerprintB { get; init; }
    
    /// <summary>Dataset fingerprint A.</summary>
    public required string DatasetFingerprintA { get; init; }
    
    /// <summary>Dataset fingerprint B.</summary>
    public required string DatasetFingerprintB { get; init; }
    
    /// <summary>Code fingerprint A.</summary>
    public required string CodeFingerprintA { get; init; }
    
    /// <summary>Code fingerprint B.</summary>
    public required string CodeFingerprintB { get; init; }
    
    /// <summary>Environment fingerprint A.</summary>
    public required string EnvironmentFingerprintA { get; init; }
    
    /// <summary>Environment fingerprint B.</summary>
    public required string EnvironmentFingerprintB { get; init; }
    
    /// <summary>Framework A.</summary>
    public required string FrameworkA { get; init; }
    
    /// <summary>Framework B.</summary>
    public required string FrameworkB { get; init; }
}

#endregion

#region Executive Summary

/// <summary>
/// Executive summary generator for comparison results.
/// </summary>
public static class ExecutiveSummaryGenerator
{
    /// <summary>
    /// Generate executive summary for inference optimization comparison.
    /// </summary>
    public static string GenerateInferenceOptimizationSummary(ComparisonResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Inference Optimization Result");
        sb.AppendLine();
        
        // Main finding
        var dtc = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔTc" && d.Fired);
        var dto = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔO" && d.Fired);
        var dtf = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔF" && d.Fired);
        
        if (dtc != null)
        {
            var direction = dtc.AbsoluteDifference < 0 ? "earlier" : "later";
            var steps = Math.Abs(dtc.AbsoluteDifference);
            sb.AppendLine($"After optimization, the model reaches stable inference behavior **{steps:F0} steps {direction}**");
        }
        else
        {
            sb.AppendLine("After optimization, stabilization time shows no significant change.");
        }
        
        if (dto != null)
        {
            var direction = dto.AbsoluteDifference < 0 ? "reduced" : "increased";
            sb.AppendLine($"and shows **{direction} runtime variability** during steady-state execution.");
        }
        else
        {
            sb.AppendLine("Runtime variability during steady-state is comparable.");
        }
        
        sb.AppendLine();
        
        // Runtime failures
        if (dtf != null)
        {
            sb.AppendLine($"⚠ **Runtime anomalies detected** in the optimized run ({dtf.Notes ?? "review recommended"}).");
        }
        else
        {
            sb.AppendLine("No runtime failures were detected.");
        }
        
        sb.AppendLine();
        
        // Conditions
        sb.AppendLine("Measurements reflect identical inputs and hardware conditions, ");
        sb.AppendLine("with differences attributable to runtime optimization.");
        
        // Warnings
        if (result.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Warnings");
            foreach (var w in result.Warnings)
            {
                sb.AppendLine($"- {w}");
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate short summary suitable for UI display.
    /// </summary>
    public static string GenerateShortSummary(ComparisonResult result)
    {
        var firedCount = result.FiredDeltas.Count();
        var suppressedCount = result.SuppressedDeltas.Count();
        
        if (firedCount == 0)
        {
            return "No significant differences detected.";
        }
        
        var deltas = string.Join(", ", result.FiredDeltas.Select(d => d.DeltaType));
        var suffix = suppressedCount > 0 ? $" ({suppressedCount} suppressed)" : "";
        
        return $"{firedCount} significant delta(s): {deltas}{suffix}";
    }
}

#endregion

#region RunTraceComparer Service

/// <summary>
/// Service for comparing two RunTraces with scientific rigor.
/// Implements the before/after optimization comparison workflow.
/// </summary>
public sealed class RunTraceComparer
{
    private readonly RunTraceValidator _validator;
    
    public RunTraceComparer(RuntimeValidationOptions? validationOptions = null)
    {
        _validator = new RunTraceValidator(validationOptions);
    }
    
    /// <summary>
    /// Compare two RunTraces using the specified intent.
    /// </summary>
    public ComparisonResult Compare(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        ComparisonIntent intent)
    {
        var comparisonId = GenerateComparisonId(traceA, traceB, intent);
        var warnings = new List<string>();
        
        // Step 1: Validate both traces
        var validationA = _validator.Validate(traceA);
        var validationB = _validator.Validate(traceB);
        
        if (!validationA.IsValid)
        {
            warnings.Add($"{intent.LabelA}: {validationA.Errors.Count} validation error(s)");
        }
        if (!validationB.IsValid)
        {
            warnings.Add($"{intent.LabelB}: {validationB.Errors.Count} validation error(s)");
        }
        
        // Add validation warnings
        warnings.AddRange(validationA.Warnings.Select(w => $"{intent.LabelA}: {w.Message}"));
        warnings.AddRange(validationB.Warnings.Select(w => $"{intent.LabelB}: {w.Message}"));
        
        // Step 2: Compare fingerprints
        var fingerprints = CompareFingerprints(traceA, traceB, intent);
        
        // Step 3: Align traces
        var alignment = AlignTraces(traceA, traceB, intent);
        
        // Step 4: Apply preset guardrails
        if (intent.PresetId == TfrtRuntimePreset.PresetId)
        {
            warnings.AddRange(TfrtGuardrails.ValidateForAnalysis(traceA));
            warnings.AddRange(TfrtGuardrails.ValidateForAnalysis(traceB));
        }
        
        // Step 5: Compute deltas
        var deltas = ComputeDeltas(traceA, traceB, alignment, intent);
        
        return new ComparisonResult
        {
            ComparisonId = comparisonId,
            ComputedUtc = DateTimeOffset.UtcNow,
            Intent = intent,
            Fingerprints = fingerprints,
            Alignment = alignment,
            Deltas = deltas,
            Warnings = warnings,
            DeltaSpecVersion = "1.0.0",
            PresetId = intent.PresetId
        };
    }
    
    #region Fingerprint Comparison
    
    private FingerprintComparison CompareFingerprints(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        ComparisonIntent intent)
    {
        var differences = new List<FingerprintDifference>();
        var issues = new List<ComparisonIssue>();
        var metaA = traceA.Metadata;
        var metaB = traceB.Metadata;
        
        // Model fingerprint: MAY change for optimization
        if (metaA.ModelFingerprint != metaB.ModelFingerprint)
        {
            differences.Add(new FingerprintDifference
            {
                Category = "model",
                FingerprintA = metaA.ModelFingerprint,
                FingerprintB = metaB.ModelFingerprint,
                IsExpectedForOptimization = true,
                Explanation = "Model changed (expected for optimization)"
            });
        }
        
        // Environment fingerprint: MAY change for optimization
        if (metaA.EnvironmentFingerprint != metaB.EnvironmentFingerprint)
        {
            differences.Add(new FingerprintDifference
            {
                Category = "environment",
                FingerprintA = metaA.EnvironmentFingerprint,
                FingerprintB = metaB.EnvironmentFingerprint,
                IsExpectedForOptimization = true,
                Explanation = "Environment changed (may include optimization flags)"
            });
        }
        
        // Dataset fingerprint: MUST NOT change
        if (metaA.DatasetFingerprint != metaB.DatasetFingerprint)
        {
            differences.Add(new FingerprintDifference
            {
                Category = "dataset",
                FingerprintA = metaA.DatasetFingerprint,
                FingerprintB = metaB.DatasetFingerprint,
                IsExpectedForOptimization = false,
                Explanation = "Dataset changed (comparison may be invalid)"
            });
            issues.Add(new ComparisonIssue
            {
                Code = ComparisonErrorCodes.CMP_FINGERPRINT_DATASET_MISMATCH,
                Severity = ComparisonIssueSeverity.Error,
                Message = "Dataset fingerprints differ - comparison invalid unless intentional"
            });
        }
        
        // Code fingerprint: SHOULD NOT change (warning only)
        if (metaA.CodeFingerprint != metaB.CodeFingerprint)
        {
            differences.Add(new FingerprintDifference
            {
                Category = "code",
                FingerprintA = metaA.CodeFingerprint,
                FingerprintB = metaB.CodeFingerprint,
                IsExpectedForOptimization = false,
                Explanation = "Code changed (review carefully)"
            });
            issues.Add(new ComparisonIssue
            {
                Code = ComparisonErrorCodes.CMP_FINGERPRINT_CODE_MISMATCH,
                Severity = ComparisonIssueSeverity.Warning,
                Message = "Code fingerprints differ - ensure changes are optimization-related"
            });
        }
        
        return new FingerprintComparison
        {
            Differences = differences,
            Issues = issues
        };
    }
    
    #endregion
    
    #region Trace Alignment
    
    private AlignmentResult AlignTraces(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        ComparisonIntent intent)
    {
        var issues = new List<ComparisonIssue>();
        
        return intent.Alignment switch
        {
            AlignmentMode.RuntimeMilestone => AlignByMilestone(traceA, traceB, intent, issues),
            AlignmentMode.WallClock => AlignByWallClock(traceA, traceB, issues),
            AlignmentMode.StepBased or _ => AlignByStep(traceA, traceB, issues)
        };
    }
    
    private AlignmentResult AlignByMilestone(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        ComparisonIntent intent,
        List<ComparisonIssue> issues)
    {
        // Try primary anchor
        var anchorType = intent.PrimaryAnchor ?? RuntimeMilestoneType.SteadyStateStart;
        var anchorStepA = traceA.Milestones.OfType(anchorType).FirstOrDefault()?.Step;
        var anchorStepB = traceB.Milestones.OfType(anchorType).FirstOrDefault()?.Step;
        
        // Fallback if primary not found
        if (!anchorStepA.HasValue || !anchorStepB.HasValue)
        {
            var fallbackType = intent.FallbackAnchor ?? RuntimeMilestoneType.WarmupEnd;
            anchorStepA ??= traceA.Milestones.OfType(fallbackType).FirstOrDefault()?.Step;
            anchorStepB ??= traceB.Milestones.OfType(fallbackType).FirstOrDefault()?.Step;
            
            if (anchorStepA.HasValue && anchorStepB.HasValue)
            {
                anchorType = fallbackType;
            }
        }
        
        // Last resort: first non-warmup step (step 0)
        if (!anchorStepA.HasValue || !anchorStepB.HasValue)
        {
            issues.Add(new ComparisonIssue
            {
                Code = ComparisonErrorCodes.CMP_ALIGNMENT_NO_ANCHOR,
                Severity = ComparisonIssueSeverity.Warning,
                Message = "No milestone anchor found - using first step (may include warmup)"
            });
            anchorStepA = traceA.Timeline.FirstStep;
            anchorStepB = traceB.Timeline.FirstStep;
            anchorType = RuntimeMilestoneType.Custom;
        }
        
        // Calculate aligned step count
        var stepsAfterAnchorA = traceA.Timeline.Steps.Count(s => s >= anchorStepA.Value);
        var stepsAfterAnchorB = traceB.Timeline.Steps.Count(s => s >= anchorStepB.Value);
        var alignedCount = Math.Min(stepsAfterAnchorA, stepsAfterAnchorB);
        
        return new AlignmentResult
        {
            Mode = AlignmentMode.RuntimeMilestone,
            AnchorStepA = anchorStepA,
            AnchorStepB = anchorStepB,
            AnchorType = anchorType,
            AlignedStepCount = alignedCount,
            SkippedStepsA = traceA.Timeline.Steps.Count - stepsAfterAnchorA,
            SkippedStepsB = traceB.Timeline.Steps.Count - stepsAfterAnchorB,
            Issues = issues
        };
    }
    
    private AlignmentResult AlignByStep(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        List<ComparisonIssue> issues)
    {
        var stepsA = traceA.Timeline.Steps;
        var stepsB = traceB.Timeline.Steps;
        var alignedCount = Math.Min(stepsA.Count, stepsB.Count);
        
        if (stepsA.Count != stepsB.Count)
        {
            issues.Add(new ComparisonIssue
            {
                Code = ComparisonErrorCodes.CMP_ALIGNMENT_MISMATCHED_LENGTHS,
                Severity = ComparisonIssueSeverity.Warning,
                Message = $"Step counts differ ({stepsA.Count} vs {stepsB.Count}) - using {alignedCount} steps",
                Context = new Dictionary<string, object>
                {
                    ["stepsA"] = stepsA.Count,
                    ["stepsB"] = stepsB.Count
                }
            });
        }
        
        return new AlignmentResult
        {
            Mode = AlignmentMode.StepBased,
            AnchorStepA = stepsA.FirstOrDefault(),
            AnchorStepB = stepsB.FirstOrDefault(),
            AnchorType = null,
            AlignedStepCount = alignedCount,
            SkippedStepsA = 0,
            SkippedStepsB = 0,
            Issues = issues
        };
    }
    
    private AlignmentResult AlignByWallClock(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        List<ComparisonIssue> issues)
    {
        // Wall clock alignment requires interpolation - simplified for now
        var hasWallClockA = traceA.Timeline.WallTimeSeconds != null;
        var hasWallClockB = traceB.Timeline.WallTimeSeconds != null;
        
        if (!hasWallClockA || !hasWallClockB)
        {
            issues.Add(new ComparisonIssue
            {
                Code = ComparisonErrorCodes.CMP_ALIGNMENT_NO_ANCHOR,
                Severity = ComparisonIssueSeverity.Warning,
                Message = "Wall clock data not available - falling back to step alignment"
            });
            return AlignByStep(traceA, traceB, issues);
        }
        
        // Use step alignment with wall clock info
        return new AlignmentResult
        {
            Mode = AlignmentMode.WallClock,
            AnchorStepA = 0,
            AnchorStepB = 0,
            AnchorType = null,
            AlignedStepCount = Math.Min(traceA.Timeline.Steps.Count, traceB.Timeline.Steps.Count),
            SkippedStepsA = 0,
            SkippedStepsB = 0,
            Issues = issues
        };
    }
    
    #endregion
    
    #region Delta Computation
    
    private IReadOnlyList<ComparisonDelta> ComputeDeltas(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        AlignmentResult alignment,
        ComparisonIntent intent)
    {
        var deltas = new List<ComparisonDelta>();
        
        // Get common signals
        var signalsA = traceA.Scalars.Series.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var signalsB = traceB.Scalars.Series.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var commonSignals = signalsA.Intersect(signalsB, StringComparer.OrdinalIgnoreCase).ToList();
        
        // Determine primary signal based on preset
        var primarySignal = intent.PresetId == TfrtRuntimePreset.PresetId
            ? "latency_ms"
            : commonSignals.FirstOrDefault() ?? "loss";
        
        if (!commonSignals.Contains(primarySignal, StringComparer.OrdinalIgnoreCase))
        {
            primarySignal = commonSignals.FirstOrDefault() ?? primarySignal;
        }
        
        // Compute ΔTc (stabilization time)
        deltas.Add(ComputeDeltaTc(traceA, traceB, alignment, primarySignal, intent));
        
        // Compute ΔO (oscillation/instability)
        deltas.Add(ComputeDeltaO(traceA, traceB, alignment, primarySignal, intent));
        
        // Compute ΔF (failures/outliers)
        deltas.Add(ComputeDeltaF(traceA, traceB, alignment, primarySignal, intent));
        
        // Add suppressed deltas for visibility
        deltas.Add(CreateSuppressedDelta("ΔTd", primarySignal, intent));
        deltas.Add(CreateSuppressedDelta("ΔĀ", "accuracy", intent));
        
        return deltas;
    }
    
    private ComparisonDelta ComputeDeltaTc(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        AlignmentResult alignment,
        string signal,
        ComparisonIntent intent)
    {
        // ΔTc measures time/steps to reach stable behavior
        var steadyStateA = traceA.Milestones.SteadyStateStartStep ?? traceA.Timeline.LastStep;
        var steadyStateB = traceB.Milestones.SteadyStateStartStep ?? traceB.Timeline.LastStep;
        
        var difference = steadyStateB - steadyStateA;
        var fired = Math.Abs(difference) > 0;
        var confidence = ComputeConfidence(traceA, traceB, alignment);
        
        return new ComparisonDelta
        {
            DeltaType = "ΔTc",
            Signal = signal,
            ValueA = steadyStateA,
            ValueB = steadyStateB,
            Confidence = confidence,
            Fired = fired,
            IsSuppressed = TfrtDeltaMapping.IsSuppressed("ΔTc"),
            Interpretation = difference < 0
                ? $"Stabilizes {Math.Abs(difference)} steps earlier"
                : difference > 0
                    ? $"Stabilizes {difference} steps later"
                    : "No change in stabilization time",
            Notes = TfrtDeltaMapping.GetDeltaNotes("ΔTc")
        };
    }
    
    private ComparisonDelta ComputeDeltaO(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        AlignmentResult alignment,
        string signal,
        ComparisonIntent intent)
    {
        // ΔO measures oscillation/instability in steady state
        var seriesA = traceA.Scalars.GetByName(signal);
        var seriesB = traceB.Scalars.GetByName(signal);
        
        var varianceA = ComputeSteadyStateVariance(seriesA, traceA.Milestones);
        var varianceB = ComputeSteadyStateVariance(seriesB, traceB.Milestones);
        
        var fired = Math.Abs(varianceB - varianceA) > 0.01 * Math.Max(varianceA, varianceB);
        var confidence = ComputeConfidence(traceA, traceB, alignment);
        
        return new ComparisonDelta
        {
            DeltaType = "ΔO",
            Signal = signal,
            ValueA = varianceA,
            ValueB = varianceB,
            Confidence = confidence,
            Fired = fired,
            IsSuppressed = TfrtDeltaMapping.IsSuppressed("ΔO"),
            Interpretation = varianceB < varianceA
                ? "Reduced runtime variability"
                : varianceB > varianceA
                    ? "Increased runtime variability"
                    : "No change in runtime variability",
            Notes = TfrtDeltaMapping.GetDeltaNotes("ΔO")
        };
    }
    
    private ComparisonDelta ComputeDeltaF(
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        AlignmentResult alignment,
        string signal,
        ComparisonIntent intent)
    {
        // ΔF detects outliers/failures
        var seriesA = traceA.Scalars.GetByName(signal);
        var seriesB = traceB.Scalars.GetByName(signal);
        
        var outliersA = CountOutliers(seriesA);
        var outliersB = CountOutliers(seriesB);
        
        var fired = outliersB > outliersA;
        var confidence = ComputeConfidence(traceA, traceB, alignment);
        
        return new ComparisonDelta
        {
            DeltaType = "ΔF",
            Signal = signal,
            ValueA = outliersA,
            ValueB = outliersB,
            Confidence = confidence,
            Fired = fired,
            IsSuppressed = TfrtDeltaMapping.IsSuppressed("ΔF"),
            Interpretation = outliersB > outliersA
                ? $"Introduced {outliersB - outliersA} new runtime anomalies"
                : outliersB < outliersA
                    ? $"Eliminated {outliersA - outliersB} runtime anomalies"
                    : "No change in runtime anomalies",
            Notes = TfrtDeltaMapping.GetDeltaNotes("ΔF")
        };
    }
    
    private ComparisonDelta CreateSuppressedDelta(string deltaType, string signal, ComparisonIntent intent)
    {
        var isSuppressed = intent.PresetId == TfrtRuntimePreset.PresetId &&
                          TfrtDeltaMapping.IsSuppressed(deltaType);
        
        return new ComparisonDelta
        {
            DeltaType = deltaType,
            Signal = signal,
            ValueA = 0,
            ValueB = 0,
            Confidence = 0,
            Fired = false,
            IsSuppressed = isSuppressed,
            Interpretation = isSuppressed ? "Suppressed by preset (not applicable)" : null,
            Notes = TfrtDeltaMapping.GetDeltaNotes(deltaType)
        };
    }
    
    private double ComputeSteadyStateVariance(RuntimeScalarSeries? series, RuntimeMilestones milestones)
    {
        if (series == null) return 0;
        
        var startStep = milestones.SteadyStateStartStep ?? 0;
        var values = series.Values
            .Skip(startStep)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        
        if (values.Count < 2) return 0;
        
        var mean = values.Average();
        var variance = values.Sum(v => Math.Pow(v - mean, 2)) / values.Count;
        return Math.Sqrt(variance);
    }
    
    private int CountOutliers(RuntimeScalarSeries? series)
    {
        if (series == null) return 0;
        
        var values = series.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (values.Count < 3) return 0;
        
        var mean = values.Average();
        var stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count);
        var threshold = 3 * stdDev; // 3-sigma rule
        
        return values.Count(v => Math.Abs(v - mean) > threshold);
    }
    
    private double ComputeConfidence(RuntimeRunTrace traceA, RuntimeRunTrace traceB, AlignmentResult alignment)
    {
        // Confidence based on:
        // 1. Aligned step count
        // 2. Noise level
        // 3. Whether steady state was detected
        
        var stepWeight = Math.Min(1.0, alignment.AlignedStepCount / 100.0);
        var hasSteadyStateA = traceA.Milestones.SteadyStateStartStep.HasValue;
        var hasSteadyStateB = traceB.Milestones.SteadyStateStartStep.HasValue;
        var steadyStateWeight = (hasSteadyStateA && hasSteadyStateB) ? 1.0 : 0.7;
        
        return stepWeight * steadyStateWeight;
    }
    
    #endregion
    
    #region Review Bundle Export
    
    /// <summary>
    /// Export comparison result as review-only bundle.
    /// </summary>
    public ReviewOnlyBundle ExportReviewBundle(
        ComparisonResult result,
        RuntimeRunTrace traceA,
        RuntimeRunTrace traceB,
        ReviewExportMode mode = ReviewExportMode.Review)
    {
        var fingerprintSummary = new ReviewFingerprintSummary
        {
            LabelA = result.Intent.LabelA,
            LabelB = result.Intent.LabelB,
            ModelFingerprintA = traceA.Metadata.ModelFingerprint,
            ModelFingerprintB = traceB.Metadata.ModelFingerprint,
            DatasetFingerprintA = traceA.Metadata.DatasetFingerprint,
            DatasetFingerprintB = traceB.Metadata.DatasetFingerprint,
            CodeFingerprintA = traceA.Metadata.CodeFingerprint,
            CodeFingerprintB = traceB.Metadata.CodeFingerprint,
            EnvironmentFingerprintA = traceA.Metadata.EnvironmentFingerprint,
            EnvironmentFingerprintB = traceB.Metadata.EnvironmentFingerprint,
            FrameworkA = traceA.Framework.ToString(),
            FrameworkB = traceB.Framework.ToString()
        };
        
        var bundleHash = ComputeBundleHash(result, fingerprintSummary);
        
        return new ReviewOnlyBundle
        {
            Version = ReviewOnlyBundle.SchemaVersion,
            BundleHash = bundleHash,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExportMode = mode,
            Comparison = result,
            FingerprintSummary = fingerprintSummary,
            IsReviewedFromBundle = false,
            ParentBundleHash = null
        };
    }
    
    /// <summary>
    /// Re-export from an existing review bundle (stamps parent hash).
    /// </summary>
    public ReviewOnlyBundle ReExportFromBundle(
        ReviewOnlyBundle existingBundle,
        ReviewExportMode mode = ReviewExportMode.Audit)
    {
        var newHash = ComputeBundleHash(existingBundle.Comparison, existingBundle.FingerprintSummary);
        
        return existingBundle with
        {
            Version = ReviewOnlyBundle.SchemaVersion,
            BundleHash = newHash,
            CreatedUtc = DateTimeOffset.UtcNow,
            ExportMode = mode,
            IsReviewedFromBundle = true,
            ParentBundleHash = existingBundle.BundleHash
        };
    }
    
    private string ComputeBundleHash(ComparisonResult result, ReviewFingerprintSummary summary)
    {
        var content = $"{result.ComparisonId}|{result.ComputedUtc:O}|{summary.ModelFingerprintA}|{summary.ModelFingerprintB}";
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    private string GenerateComparisonId(RuntimeRunTrace traceA, RuntimeRunTrace traceB, ComparisonIntent intent)
    {
        var content = $"{traceA.RunId}|{traceB.RunId}|{intent.PresetId}|{DateTimeOffset.UtcNow:O}";
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return $"cmp-{Convert.ToHexString(hash)[..16].ToLowerInvariant()}";
    }
    
    #endregion
}

#endregion

#region Comparison Extensions

/// <summary>
/// Extension methods for RunTrace comparison.
/// </summary>
public static class ComparisonExtensions
{
    /// <summary>
    /// Compare this trace against another using TFRT optimization intent.
    /// </summary>
    public static ComparisonResult CompareAgainst(
        this RuntimeRunTrace baseline,
        RuntimeRunTrace optimized,
        string? baselineLabel = null,
        string? optimizedLabel = null)
    {
        var comparer = new RunTraceComparer();
        var intent = ComparisonIntent.TfrtOptimization(baselineLabel, optimizedLabel);
        return comparer.Compare(baseline, optimized, intent);
    }
    
    /// <summary>
    /// Validate that this trace is suitable for comparison.
    /// </summary>
    public static bool IsValidForComparison(this RuntimeRunTrace trace, out IReadOnlyList<string> issues)
    {
        var validator = new RunTraceValidator();
        var result = validator.Validate(trace);
        issues = result.Errors.Select(e => e.Message).ToList();
        return result.IsValid;
    }
    
    /// <summary>
    /// Get executive summary for a comparison result.
    /// </summary>
    public static string GetExecutiveSummary(this ComparisonResult result)
    {
        return ExecutiveSummaryGenerator.GenerateInferenceOptimizationSummary(result);
    }
    
    /// <summary>
    /// Get short summary for UI display.
    /// </summary>
    public static string GetShortSummary(this ComparisonResult result)
    {
        return ExecutiveSummaryGenerator.GenerateShortSummary(result);
    }
}

#endregion
