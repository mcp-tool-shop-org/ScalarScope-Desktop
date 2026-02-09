// RunTrace Validator v1.0
// Guarantees scientific validity, UI safety, and delta correctness
// Runs AFTER schema validation, BEFORE presets/delta computation/replay/bundling.

using System.Globalization;
using System.Text.RegularExpressions;

namespace ScalarScope.Services.Connectors;

#region Validation Error Codes

/// <summary>
/// RunTrace validation error codes.
/// </summary>
public static class RunTraceErrorCodes
{
    // V1 — Timeline Integrity
    public const string RT_TIMELINE_EMPTY = "RT_TIMELINE_EMPTY";
    public const string RT_TIMELINE_NON_MONOTONIC = "RT_TIMELINE_NON_MONOTONIC";
    public const string RT_TIMELINE_LENGTH_MISMATCH = "RT_TIMELINE_LENGTH_MISMATCH";
    public const string RT_TIMELINE_NEGATIVE_STEP = "RT_TIMELINE_NEGATIVE_STEP";
    
    // V2 — Scalar Series Integrity
    public const string RT_SCALAR_LENGTH_MISMATCH = "RT_SCALAR_LENGTH_MISMATCH";
    public const string RT_SCALAR_ALL_NULL = "RT_SCALAR_ALL_NULL";
    public const string RT_SCALAR_INVALID_VALUE = "RT_SCALAR_INVALID_VALUE";
    public const string RT_SCALAR_AGG_NO_MILESTONE = "RT_SCALAR_AGG_NO_MILESTONE";
    
    // V3 — Null Handling
    public const string RT_SCALAR_HIGH_NULL_DENSITY = "RT_SCALAR_HIGH_NULL_DENSITY";
    public const string RT_SCALAR_LEADING_NULLS = "RT_SCALAR_LEADING_NULLS";
    public const string RT_SCALAR_TRAILING_NULLS = "RT_SCALAR_TRAILING_NULLS";
    
    // V4 — Milestone Consistency
    public const string RT_MILESTONE_MISSING_STEADY_STATE = "RT_MILESTONE_MISSING_STEADY_STATE";
    public const string RT_MILESTONE_OUT_OF_RANGE = "RT_MILESTONE_OUT_OF_RANGE";
    
    // V5 — Metadata & Fingerprints
    public const string RT_FINGERPRINT_INVALID = "RT_FINGERPRINT_INVALID";
    public const string RT_ENVIRONMENT_MISSING = "RT_ENVIRONMENT_MISSING";
    
    // V6 — Capability Consistency
    public const string RT_CAPABILITY_MISMATCH = "RT_CAPABILITY_MISMATCH";
}

#endregion

#region Validation Result Types

/// <summary>
/// Single validation issue for RuntimeRunTrace.
/// </summary>
public sealed record RuntimeValidationIssue
{
    /// <summary>Error code (e.g., RT_TIMELINE_EMPTY).</summary>
    public required string Code { get; init; }
    
    /// <summary>Severity level.</summary>
    public required ValidationSeverity Severity { get; init; }
    
    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
    
    /// <summary>Optional path to problematic element (e.g., "scalars.latency_ms").</summary>
    public string? Path { get; init; }
    
    /// <summary>Optional additional context.</summary>
    public IReadOnlyDictionary<string, object>? Context { get; init; }
}

/// <summary>
/// Complete validation result for RuntimeRunTrace.
/// </summary>
public sealed record RuntimeValidationResult
{
    /// <summary>Whether the trace is valid (no errors).</summary>
    public bool IsValid => !Errors.Any();
    
    /// <summary>Blocking errors.</summary>
    public required IReadOnlyList<RuntimeValidationIssue> Errors { get; init; }
    
    /// <summary>Non-blocking warnings.</summary>
    public required IReadOnlyList<RuntimeValidationIssue> Warnings { get; init; }
    
    /// <summary>Diagnostic information.</summary>
    public required IReadOnlyList<RuntimeValidationIssue> Infos { get; init; }
    
    /// <summary>All issues combined.</summary>
    public IEnumerable<RuntimeValidationIssue> AllIssues => Errors.Concat(Warnings).Concat(Infos);
    
    /// <summary>Create a valid result with no issues.</summary>
    public static RuntimeValidationResult Valid() => new()
    {
        Errors = [],
        Warnings = [],
        Infos = []
    };
}

/// <summary>
/// Validation options for RuntimeRunTrace.
/// </summary>
public sealed record RuntimeValidationOptions
{
    /// <summary>Maximum allowed null density (0-1). Default: 0.30 (30%).</summary>
    public double MaxNullDensity { get; init; } = 0.30;
    
    /// <summary>Whether to allow gaps in step sequence (sparse logging).</summary>
    public bool AllowSparseSteps { get; init; } = true;
    
    /// <summary>Whether to validate fingerprints strictly.</summary>
    public bool StrictFingerprints { get; init; } = true;
    
    /// <summary>Default options.</summary>
    public static RuntimeValidationOptions Default => new();
}

#endregion

#region RunTraceValidator

/// <summary>
/// Validates RuntimeRunTrace for scientific validity, UI safety, and delta correctness.
/// Runs after schema validation, before presets/delta computation/replay/bundling.
/// </summary>
public sealed class RunTraceValidator
{
    private readonly RuntimeValidationOptions _options;
    
    public RunTraceValidator(RuntimeValidationOptions? options = null)
    {
        _options = options ?? RuntimeValidationOptions.Default;
    }
    
    /// <summary>
    /// Validate a RuntimeRunTrace through all stages.
    /// </summary>
    public RuntimeValidationResult Validate(RuntimeRunTrace trace)
    {
        var errors = new List<RuntimeValidationIssue>();
        var warnings = new List<RuntimeValidationIssue>();
        var infos = new List<RuntimeValidationIssue>();
        
        // V1 — Timeline Integrity
        ValidateTimeline(trace, errors, warnings, infos);
        
        // Short-circuit if timeline is invalid (other stages depend on it)
        if (errors.Any(e => e.Code.StartsWith("RT_TIMELINE")))
        {
            return new RuntimeValidationResult { Errors = errors, Warnings = warnings, Infos = infos };
        }
        
        // V2 — Scalar Series Integrity
        ValidateScalars(trace, errors, warnings, infos);
        
        // V3 — Null Handling Rules
        ValidateNullHandling(trace, errors, warnings, infos);
        
        // V4 — Milestone Consistency
        ValidateMilestones(trace, errors, warnings, infos);
        
        // V5 — Metadata & Fingerprints
        ValidateMetadata(trace, errors, warnings, infos);
        
        // V6 — Capability Consistency
        ValidateCapabilities(trace, errors, warnings, infos);
        
        return new RuntimeValidationResult
        {
            Errors = errors,
            Warnings = warnings,
            Infos = infos
        };
    }
    
    #region V1 — Timeline Integrity
    
    private void ValidateTimeline(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        var steps = trace.Timeline.Steps;
        
        // Rule: length ≥ 1
        if (steps.Count == 0)
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_TIMELINE_EMPTY,
                Severity = ValidationSeverity.Error,
                Message = "No steps provided in timeline",
                Path = "timeline.steps"
            });
            return;
        }
        
        // Rule: integers ≥ 0
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] < 0)
            {
                errors.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_TIMELINE_NEGATIVE_STEP,
                    Severity = ValidationSeverity.Error,
                    Message = $"Step at index {i} is negative: {steps[i]}",
                    Path = $"timeline.steps[{i}]",
                    Context = new Dictionary<string, object> { ["index"] = i, ["value"] = steps[i] }
                });
            }
        }
        
        // Rule: strictly increasing (no repeats)
        for (int i = 1; i < steps.Count; i++)
        {
            if (steps[i] <= steps[i - 1])
            {
                errors.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_TIMELINE_NON_MONOTONIC,
                    Severity = ValidationSeverity.Error,
                    Message = $"Steps not strictly increasing at index {i}: {steps[i - 1]} → {steps[i]}",
                    Path = $"timeline.steps[{i}]",
                    Context = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["previous"] = steps[i - 1],
                        ["current"] = steps[i]
                    }
                });
                break; // One error is enough
            }
        }
        
        // Rule: optional arrays length must match
        if (trace.Timeline.WallTimeSeconds != null && 
            trace.Timeline.WallTimeSeconds.Count != steps.Count)
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_TIMELINE_LENGTH_MISMATCH,
                Severity = ValidationSeverity.Error,
                Message = $"wallTimeSeconds length ({trace.Timeline.WallTimeSeconds.Count}) != steps length ({steps.Count})",
                Path = "timeline.wallTimeSeconds"
            });
        }
        
        if (trace.Timeline.Epoch != null && 
            trace.Timeline.Epoch.Count != steps.Count)
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_TIMELINE_LENGTH_MISMATCH,
                Severity = ValidationSeverity.Error,
                Message = $"epoch length ({trace.Timeline.Epoch.Count}) != steps length ({steps.Count})",
                Path = "timeline.epoch"
            });
        }
    }
    
    #endregion
    
    #region V2 — Scalar Series Integrity
    
    private void ValidateScalars(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        var stepCount = trace.Timeline.Steps.Count;
        
        foreach (var series in trace.Scalars.Series)
        {
            var path = $"scalars.{series.Name}";
            
            // Rule: values.length == timeline.steps.length
            if (series.Values.Count != stepCount)
            {
                errors.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_SCALAR_LENGTH_MISMATCH,
                    Severity = ValidationSeverity.Error,
                    Message = $"Scalar '{series.Name}' length ({series.Values.Count}) != steps length ({stepCount})",
                    Path = path,
                    Context = new Dictionary<string, object>
                    {
                        ["seriesLength"] = series.Values.Count,
                        ["stepsLength"] = stepCount
                    }
                });
                continue;
            }
            
            // Rule: all-null series are invalid
            var nonNullCount = series.Values.Count(v => v.HasValue);
            if (nonNullCount == 0)
            {
                errors.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_SCALAR_ALL_NULL,
                    Severity = ValidationSeverity.Error,
                    Message = $"Scalar '{series.Name}' contains only null values",
                    Path = path
                });
                continue;
            }
            
            // Rule: check for NaN / ±Inf
            for (int i = 0; i < series.Values.Count; i++)
            {
                var v = series.Values[i];
                if (v.HasValue && (double.IsNaN(v.Value) || double.IsInfinity(v.Value)))
                {
                    errors.Add(new RuntimeValidationIssue
                    {
                        Code = RunTraceErrorCodes.RT_SCALAR_INVALID_VALUE,
                        Severity = ValidationSeverity.Error,
                        Message = $"Scalar '{series.Name}' has invalid value at index {i}: {v.Value}",
                        Path = $"{path}.values[{i}]",
                        Context = new Dictionary<string, object> { ["index"] = i, ["value"] = v.Value.ToString(CultureInfo.InvariantCulture) }
                    });
                    break; // One error per series is enough
                }
            }
            
            // Rule: aggregated series must have milestone context
            if (series.Aggregation.HasValue && series.Aggregation.Value != ScalarAggregation.None)
            {
                // Check for any milestone
                if (trace.Milestones.List.Count == 0)
                {
                    errors.Add(new RuntimeValidationIssue
                    {
                        Code = RunTraceErrorCodes.RT_SCALAR_AGG_NO_MILESTONE,
                        Severity = ValidationSeverity.Error,
                        Message = $"Aggregated scalar '{series.Name}' requires at least one milestone for context",
                        Path = path
                    });
                }
            }
        }
    }
    
    #endregion
    
    #region V3 — Null Handling Rules
    
    private void ValidateNullHandling(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        foreach (var series in trace.Scalars.Series)
        {
            var path = $"scalars.{series.Name}";
            var values = series.Values;
            
            if (values.Count == 0) continue;
            
            // Calculate null density
            var nullCount = values.Count(v => !v.HasValue);
            var nullDensity = (double)nullCount / values.Count;
            
            // Warning: high null density
            if (nullDensity > _options.MaxNullDensity)
            {
                warnings.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_SCALAR_HIGH_NULL_DENSITY,
                    Severity = ValidationSeverity.Warning,
                    Message = $"Scalar '{series.Name}' has {nullDensity:P0} null values (threshold: {_options.MaxNullDensity:P0})",
                    Path = path,
                    Context = new Dictionary<string, object>
                    {
                        ["nullDensity"] = nullDensity,
                        ["threshold"] = _options.MaxNullDensity
                    }
                });
            }
            
            // Info: leading nulls (warmup assumed)
            var leadingNulls = 0;
            for (int i = 0; i < values.Count && !values[i].HasValue; i++)
            {
                leadingNulls++;
            }
            
            if (leadingNulls > 0 && leadingNulls < values.Count)
            {
                infos.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_SCALAR_LEADING_NULLS,
                    Severity = ValidationSeverity.Info,
                    Message = $"Scalar '{series.Name}' has {leadingNulls} leading nulls (warmup assumed)",
                    Path = path,
                    Context = new Dictionary<string, object> { ["leadingNulls"] = leadingNulls }
                });
            }
            
            // Error: trailing nulls (signal never stabilizes)
            var trailingNulls = 0;
            for (int i = values.Count - 1; i >= 0 && !values[i].HasValue; i--)
            {
                trailingNulls++;
            }
            
            // Check if there's any terminal signal
            if (trailingNulls > 0)
            {
                // Allow some trailing nulls but not all
                var trailingRatio = (double)trailingNulls / values.Count;
                if (trailingRatio > 0.5) // More than 50% trailing nulls is an error
                {
                    errors.Add(new RuntimeValidationIssue
                    {
                        Code = RunTraceErrorCodes.RT_SCALAR_TRAILING_NULLS,
                        Severity = ValidationSeverity.Error,
                        Message = $"Scalar '{series.Name}' has {trailingNulls} trailing nulls ({trailingRatio:P0}) - signal never stabilizes",
                        Path = path,
                        Context = new Dictionary<string, object>
                        {
                            ["trailingNulls"] = trailingNulls,
                            ["trailingRatio"] = trailingRatio
                        }
                    });
                }
            }
        }
    }
    
    #endregion
    
    #region V4 — Milestone Consistency
    
    private void ValidateMilestones(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        var steps = trace.Timeline.Steps;
        var stepSet = new HashSet<int>(steps);
        var minStep = steps.Min();
        var maxStep = steps.Max();
        
        foreach (var milestone in trace.Milestones.List)
        {
            // Rule: milestones must reference valid steps
            // Allow milestone step to be within range even if not exactly on a logged step
            if (milestone.Step < minStep || milestone.Step > maxStep)
            {
                errors.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_MILESTONE_OUT_OF_RANGE,
                    Severity = ValidationSeverity.Error,
                    Message = $"Milestone '{milestone.Type}' at step {milestone.Step} is outside timeline range [{minStep}, {maxStep}]",
                    Path = $"milestones[step={milestone.Step}]",
                    Context = new Dictionary<string, object>
                    {
                        ["milestoneStep"] = milestone.Step,
                        ["minStep"] = minStep,
                        ["maxStep"] = maxStep
                    }
                });
            }
        }
        
        // Rule: inference runs should have steady_state marker
        if (trace.RunType == RunType.Inference)
        {
            var hasSteadyState = trace.Milestones.List.Any(m => 
                m.Type == RuntimeMilestoneType.SteadyStateStart || 
                m.Type == RuntimeMilestoneType.WarmupEnd);
            
            if (!hasSteadyState)
            {
                warnings.Add(new RuntimeValidationIssue
                {
                    Code = RunTraceErrorCodes.RT_MILESTONE_MISSING_STEADY_STATE,
                    Severity = ValidationSeverity.Warning,
                    Message = "Inference run lacks steady-state or warmup-end marker",
                    Path = "milestones"
                });
            }
        }
        
        // Rule: training runs should have epoch marker
        if (trace.RunType == RunType.Training)
        {
            var hasEpoch = trace.Milestones.List.Any(m => 
                m.Type == RuntimeMilestoneType.EpochStart || 
                m.Type == RuntimeMilestoneType.EpochEnd);
            
            if (!hasEpoch)
            {
                infos.Add(new RuntimeValidationIssue
                {
                    Code = "RT_MILESTONE_MISSING_EPOCH",
                    Severity = ValidationSeverity.Info,
                    Message = "Training run has no epoch markers",
                    Path = "milestones"
                });
            }
        }
    }
    
    #endregion
    
    #region V5 — Metadata & Fingerprints
    
    private static readonly Regex Sha256Regex = new(@"^[a-f0-9]{64}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private void ValidateMetadata(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        var meta = trace.Metadata;
        
        if (!_options.StrictFingerprints) return;
        
        // Rule: environment fingerprint must exist
        if (string.IsNullOrWhiteSpace(meta.EnvironmentFingerprint))
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_ENVIRONMENT_MISSING,
                Severity = ValidationSeverity.Error,
                Message = "Environment fingerprint is required",
                Path = "metadata.environmentFingerprint"
            });
        }
        else if (!IsValidSha256(meta.EnvironmentFingerprint))
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_FINGERPRINT_INVALID,
                Severity = ValidationSeverity.Error,
                Message = $"Environment fingerprint is not valid SHA-256: {meta.EnvironmentFingerprint[..Math.Min(16, meta.EnvironmentFingerprint.Length)]}...",
                Path = "metadata.environmentFingerprint"
            });
        }
        
        // Validate other fingerprints
        ValidateFingerprint(meta.ModelFingerprint, "metadata.modelFingerprint", "Model", errors);
        ValidateFingerprint(meta.DatasetFingerprint, "metadata.datasetFingerprint", "Dataset", errors);
        ValidateFingerprint(meta.CodeFingerprint, "metadata.codeFingerprint", "Code", errors);
    }
    
    private void ValidateFingerprint(string? fingerprint, string path, string name, List<RuntimeValidationIssue> errors)
    {
        if (!string.IsNullOrWhiteSpace(fingerprint) && !IsValidSha256(fingerprint))
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_FINGERPRINT_INVALID,
                Severity = ValidationSeverity.Error,
                Message = $"{name} fingerprint is not valid SHA-256: {fingerprint[..Math.Min(16, fingerprint.Length)]}...",
                Path = path
            });
        }
    }
    
    private static bool IsValidSha256(string value)
    {
        return Sha256Regex.IsMatch(value);
    }
    
    #endregion
    
    #region V6 — Capability Consistency
    
    private void ValidateCapabilities(
        RuntimeRunTrace trace,
        List<RuntimeValidationIssue> errors,
        List<RuntimeValidationIssue> warnings,
        List<RuntimeValidationIssue> infos)
    {
        var caps = trace.Capabilities;
        var scalars = trace.Scalars;
        
        // hasLatency
        ValidateCapabilityFlag(
            caps.HasLatency,
            scalars.Has("latency_ms") || scalars.Has("latency"),
            "hasLatency",
            "latency",
            errors);
        
        // hasThroughput
        ValidateCapabilityFlag(
            caps.HasThroughput,
            scalars.Has("throughput_items_per_sec") || scalars.Has("throughput"),
            "hasThroughput",
            "throughput",
            errors);
        
        // hasMemory
        ValidateCapabilityFlag(
            caps.HasMemory,
            scalars.Has("memory_bytes") || scalars.Has("memory_mb") || scalars.Has("memory"),
            "hasMemory",
            "memory",
            errors);
        
        // hasLoss
        ValidateCapabilityFlag(
            caps.HasLoss,
            scalars.Has("loss") || scalars.Has("train_loss"),
            "hasLoss",
            "loss",
            errors);
        
        // hasAccuracy
        ValidateCapabilityFlag(
            caps.HasAccuracy,
            scalars.Has("accuracy") || scalars.Has("train_accuracy"),
            "hasAccuracy",
            "accuracy",
            errors);
    }
    
    private void ValidateCapabilityFlag(
        bool flagValue,
        bool dataExists,
        string flagName,
        string dataName,
        List<RuntimeValidationIssue> errors)
    {
        if (flagValue != dataExists)
        {
            errors.Add(new RuntimeValidationIssue
            {
                Code = RunTraceErrorCodes.RT_CAPABILITY_MISMATCH,
                Severity = ValidationSeverity.Error,
                Message = flagValue
                    ? $"Capability '{flagName}' is true but no '{dataName}' series found"
                    : $"Capability '{flagName}' is false but '{dataName}' series exists",
                Path = $"capabilities.{flagName}",
                Context = new Dictionary<string, object>
                {
                    ["flagValue"] = flagValue,
                    ["dataExists"] = dataExists
                }
            });
        }
    }
    
    #endregion
}

#endregion

#region Validator Extensions

/// <summary>
/// Extension methods for validation integration.
/// </summary>
public static class RunTraceValidatorExtensions
{
    /// <summary>
    /// Validate and throw if invalid.
    /// </summary>
    public static RuntimeRunTrace ValidateOrThrow(this RuntimeRunTrace trace, RuntimeValidationOptions? options = null)
    {
        var validator = new RunTraceValidator(options);
        var result = validator.Validate(trace);
        
        if (!result.IsValid)
        {
            var errorMessages = string.Join("; ", result.Errors.Select(e => $"[{e.Code}] {e.Message}"));
            throw new InvalidOperationException($"RunTrace validation failed: {errorMessages}");
        }
        
        return trace;
    }
    
    /// <summary>
    /// Try to validate, returning success status.
    /// </summary>
    public static bool TryValidate(
        this RuntimeRunTrace trace, 
        out RuntimeValidationResult result,
        RuntimeValidationOptions? options = null)
    {
        var validator = new RunTraceValidator(options);
        result = validator.Validate(trace);
        return result.IsValid;
    }
}

#endregion
