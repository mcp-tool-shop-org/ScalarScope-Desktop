// ConnectorValidationService - Error Handling and Validation
// Uses Phase 6 error explanation system for connector errors.

namespace ScalarScope.Services.Connectors;

/// <summary>
/// Well-known connector error codes.
/// </summary>
public static class ConnectorErrorCodes
{
    /// <summary>Source path/URI doesn't exist.</summary>
    public const string SourceNotFound = "CONNECTOR_SOURCE_NOT_FOUND";
    
    /// <summary>File format not recognized.</summary>
    public const string FormatInvalid = "CONNECTOR_FORMAT_INVALID";
    
    /// <summary>No scalar metrics found.</summary>
    public const string NoScalars = "CONNECTOR_NO_SCALARS";
    
    /// <summary>Preset couldn't map required signals.</summary>
    public const string SignalMappingFailed = "CONNECTOR_SIGNAL_MAPPING_FAILED";
    
    /// <summary>Trace missing required fields.</summary>
    public const string IncompleteTrace = "CONNECTOR_INCOMPLETE_TRACE";
    
    /// <summary>Live stream disconnected.</summary>
    public const string StreamingInterrupted = "CONNECTOR_STREAMING_INTERRUPTED";
    
    /// <summary>No suitable connector found.</summary>
    public const string NoConnectorFound = "CONNECTOR_NOT_FOUND";
    
    /// <summary>Connector version mismatch.</summary>
    public const string VersionMismatch = "CONNECTOR_VERSION_MISMATCH";
    
    /// <summary>Data validation failed.</summary>
    public const string ValidationFailed = "CONNECTOR_VALIDATION_FAILED";
    
    /// <summary>Permission denied.</summary>
    public const string PermissionDenied = "CONNECTOR_PERMISSION_DENIED";
    
    /// <summary>Artifact not found.</summary>
    public const string ArtifactNotFound = "CONNECTOR_ARTIFACT_NOT_FOUND";
    
    /// <summary>Artifact hash mismatch.</summary>
    public const string ArtifactHashMismatch = "CONNECTOR_ARTIFACT_HASH_MISMATCH";
}

/// <summary>
/// Exception thrown by connectors with structured error information.
/// </summary>
public class ConnectorException : Exception
{
    /// <summary>Error code.</summary>
    public string Code { get; }
    
    /// <summary>Source path that caused the error.</summary>
    public string? SourcePath { get; }
    
    /// <summary>Additional context.</summary>
    public IReadOnlyDictionary<string, object>? Context { get; }
    
    public ConnectorException(string code, string message, string? sourcePath = null)
        : base(message)
    {
        Code = code;
        SourcePath = sourcePath;
    }
    
    public ConnectorException(string code, string message, Exception inner, string? sourcePath = null)
        : base(message, inner)
    {
        Code = code;
        SourcePath = sourcePath;
    }
    
    public ConnectorException(
        string code, 
        string message, 
        string? sourcePath, 
        IReadOnlyDictionary<string, object>? context)
        : base(message)
    {
        Code = code;
        SourcePath = sourcePath;
        Context = context;
    }
}

/// <summary>
/// Result of RunTrace validation.
/// </summary>
public sealed record TraceValidationResult
{
    /// <summary>Whether the trace is valid.</summary>
    public required bool IsValid { get; init; }
    
    /// <summary>Validation issues found.</summary>
    public required IReadOnlyList<TraceValidationIssue> Issues { get; init; }
    
    /// <summary>Effective capabilities after validation.</summary>
    public ConnectorCapabilities? EffectiveCapabilities { get; init; }
    
    /// <summary>Create a valid result.</summary>
    public static TraceValidationResult Valid(ConnectorCapabilities capabilities) => new()
    {
        IsValid = true,
        Issues = Array.Empty<TraceValidationIssue>(),
        EffectiveCapabilities = capabilities
    };
    
    /// <summary>Create an invalid result.</summary>
    public static TraceValidationResult Invalid(params TraceValidationIssue[] issues) => new()
    {
        IsValid = false,
        Issues = issues
    };
}

/// <summary>
/// Single validation issue.
/// </summary>
public sealed record TraceValidationIssue
{
    /// <summary>Error code.</summary>
    public required string Code { get; init; }
    
    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
    
    /// <summary>Severity level.</summary>
    public required ValidationSeverity Severity { get; init; }
    
    /// <summary>Path to the problematic field.</summary>
    public string? FieldPath { get; init; }
    
    /// <summary>Suggested fix.</summary>
    public string? Suggestion { get; init; }
}

/// <summary>
/// Validation severity levels.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Informational note.</summary>
    Info,
    
    /// <summary>Warning - trace is usable but may have issues.</summary>
    Warning,
    
    /// <summary>Error - trace is invalid or unusable.</summary>
    Error
}

/// <summary>
/// Service for validating RunTrace data.
/// </summary>
public sealed class ConnectorValidationService
{
    /// <summary>Singleton instance.</summary>
    public static readonly ConnectorValidationService Instance = new();
    
    private ConnectorValidationService() { }
    
    /// <summary>
    /// Validate a RunTrace.
    /// </summary>
    public TraceValidationResult Validate(RunTrace trace)
    {
        var issues = new List<TraceValidationIssue>();
        
        // Version check
        if (trace.TraceVersion != RunTrace.CurrentVersion)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.VersionMismatch,
                Message = $"Trace version {trace.TraceVersion} may not be fully compatible (expected {RunTrace.CurrentVersion})",
                Severity = ValidationSeverity.Warning,
                FieldPath = "traceVersion"
            });
        }
        
        // Metadata validation
        ValidateMetadata(trace.Metadata, issues);
        
        // Timeline validation
        ValidateTimeline(trace.Timeline, issues);
        
        // Scalars validation (minimum requirement)
        ValidateScalars(trace.Scalars, trace.Capabilities, issues);
        
        // Milestones validation (if claimed)
        if (trace.Capabilities.HasFlag(ConnectorCapabilities.Milestones))
        {
            ValidateMilestones(trace.Milestones, trace.Timeline.StepCount, issues);
        }
        
        // Artifacts validation (if claimed)
        if (trace.Capabilities.HasFlag(ConnectorCapabilities.Artifacts))
        {
            ValidateArtifacts(trace.Artifacts, issues);
        }
        
        // Signals validation (if claimed)
        if (trace.Signals is not null)
        {
            ValidateSignals(trace.Signals, trace.Capabilities, issues);
        }
        
        // Determine effective capabilities
        var effectiveCaps = DetermineEffectiveCapabilities(trace, issues);
        
        var hasErrors = issues.Any(i => i.Severity == ValidationSeverity.Error);
        return new TraceValidationResult
        {
            IsValid = !hasErrors,
            Issues = issues,
            EffectiveCapabilities = effectiveCaps
        };
    }
    
    /// <summary>
    /// Quick validation for minimum requirements only.
    /// </summary>
    public bool IsMinimallyValid(RunTrace trace)
    {
        if (string.IsNullOrEmpty(trace.Metadata.RunId))
            return false;
        if (trace.Scalars.Count == 0)
            return false;
        if (trace.Timeline.StepCount == 0)
            return false;
        return true;
    }
    
    private void ValidateMetadata(RunTraceMetadata metadata, List<TraceValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(metadata.RunId))
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Run ID is required",
                Severity = ValidationSeverity.Error,
                FieldPath = "metadata.runId"
            });
        }
        
        if (string.IsNullOrWhiteSpace(metadata.Label))
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Run label is recommended",
                Severity = ValidationSeverity.Warning,
                FieldPath = "metadata.label",
                Suggestion = "Provide a descriptive label for the run"
            });
        }
    }
    
    private void ValidateTimeline(RunTraceTimeline timeline, List<TraceValidationIssue> issues)
    {
        if (timeline.StepCount <= 0)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Step count must be positive",
                Severity = ValidationSeverity.Error,
                FieldPath = "timeline.stepCount"
            });
        }
        
        if (timeline.WallClockPerStep is not null && timeline.WallClockPerStep.Count != timeline.StepCount)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.ValidationFailed,
                Message = $"Wall clock array length ({timeline.WallClockPerStep.Count}) doesn't match step count ({timeline.StepCount})",
                Severity = ValidationSeverity.Warning,
                FieldPath = "timeline.wallClockPerStep"
            });
        }
    }
    
    private void ValidateScalars(
        IReadOnlyDictionary<string, ScalarSeries> scalars,
        ConnectorCapabilities capabilities,
        List<TraceValidationIssue> issues)
    {
        if (scalars.Count == 0)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.NoScalars,
                Message = "No scalar metrics found",
                Severity = ValidationSeverity.Error,
                FieldPath = "scalars"
            });
            return;
        }
        
        foreach (var (name, series) in scalars)
        {
            if (series.Steps.Count != series.Values.Count)
            {
                issues.Add(new TraceValidationIssue
                {
                    Code = ConnectorErrorCodes.ValidationFailed,
                    Message = $"Scalar '{name}' has mismatched steps ({series.Steps.Count}) and values ({series.Values.Count})",
                    Severity = ValidationSeverity.Error,
                    FieldPath = $"scalars.{name}"
                });
            }
            
            if (series.Steps.Count == 0)
            {
                issues.Add(new TraceValidationIssue
                {
                    Code = ConnectorErrorCodes.ValidationFailed,
                    Message = $"Scalar '{name}' has no data points",
                    Severity = ValidationSeverity.Warning,
                    FieldPath = $"scalars.{name}"
                });
            }
            
            // Check for NaN/Inf
            var invalidCount = series.Values.Count(v => double.IsNaN(v) || double.IsInfinity(v));
            if (invalidCount > 0)
            {
                issues.Add(new TraceValidationIssue
                {
                    Code = ConnectorErrorCodes.ValidationFailed,
                    Message = $"Scalar '{name}' has {invalidCount} NaN/Infinity values",
                    Severity = ValidationSeverity.Warning,
                    FieldPath = $"scalars.{name}",
                    Suggestion = "Consider enabling RemoveInvalidValues in normalization settings"
                });
            }
        }
    }
    
    private void ValidateMilestones(
        IReadOnlyList<Milestone>? milestones,
        int stepCount,
        List<TraceValidationIssue> issues)
    {
        if (milestones is null || milestones.Count == 0)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Milestones capability claimed but no milestones provided",
                Severity = ValidationSeverity.Warning,
                FieldPath = "milestones"
            });
            return;
        }
        
        foreach (var milestone in milestones)
        {
            if (milestone.Step < 0 || milestone.Step > stepCount)
            {
                issues.Add(new TraceValidationIssue
                {
                    Code = ConnectorErrorCodes.ValidationFailed,
                    Message = $"Milestone at step {milestone.Step} is outside valid range [0, {stepCount}]",
                    Severity = ValidationSeverity.Warning,
                    FieldPath = "milestones"
                });
            }
        }
    }
    
    private void ValidateArtifacts(IReadOnlyList<ArtifactReference>? artifacts, List<TraceValidationIssue> issues)
    {
        if (artifacts is null || artifacts.Count == 0)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Artifacts capability claimed but no artifacts provided",
                Severity = ValidationSeverity.Warning,
                FieldPath = "artifacts"
            });
            return;
        }
        
        foreach (var artifact in artifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Path))
            {
                issues.Add(new TraceValidationIssue
                {
                    Code = ConnectorErrorCodes.ValidationFailed,
                    Message = "Artifact has empty path",
                    Severity = ValidationSeverity.Warning,
                    FieldPath = "artifacts"
                });
            }
        }
    }
    
    private void ValidateSignals(SignalData signals, ConnectorCapabilities capabilities, List<TraceValidationIssue> issues)
    {
        if (capabilities.HasFlag(ConnectorCapabilities.Curvature) && signals.Curvature is null)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Curvature capability claimed but no curvature data provided",
                Severity = ValidationSeverity.Warning,
                FieldPath = "signals.curvature"
            });
        }
        
        if (capabilities.HasFlag(ConnectorCapabilities.Spectrum) && signals.EigenSpectrum is null)
        {
            issues.Add(new TraceValidationIssue
            {
                Code = ConnectorErrorCodes.IncompleteTrace,
                Message = "Spectrum capability claimed but no spectrum data provided",
                Severity = ValidationSeverity.Warning,
                FieldPath = "signals.eigenSpectrum"
            });
        }
    }
    
    private ConnectorCapabilities DetermineEffectiveCapabilities(RunTrace trace, List<TraceValidationIssue> issues)
    {
        var caps = ConnectorCapabilities.None;
        
        // Always have scalars if we got here
        if (trace.Scalars.Count > 0)
            caps |= ConnectorCapabilities.Scalars;
        
        // Check milestones
        if (trace.Milestones is not null && trace.Milestones.Count > 0)
            caps |= ConnectorCapabilities.Milestones;
        
        // Check artifacts
        if (trace.Artifacts is not null && trace.Artifacts.Count > 0)
            caps |= ConnectorCapabilities.Artifacts;
        
        // Check wall clock
        if (trace.Timeline.WallClockPerStep is not null)
            caps |= ConnectorCapabilities.WallClock;
        
        // Check fingerprints
        if (trace.Metadata.Fingerprints is not null)
            caps |= ConnectorCapabilities.Fingerprints;
        
        // Check signals
        if (trace.Signals?.Curvature is not null)
            caps |= ConnectorCapabilities.Curvature;
        if (trace.Signals?.EigenSpectrum is not null)
            caps |= ConnectorCapabilities.Spectrum;
        if (trace.Signals?.EvaluatorVectors is not null)
            caps |= ConnectorCapabilities.Evaluators;
        
        return caps;
    }
}

/// <summary>
/// Extension methods for connector error handling.
/// </summary>
public static class ConnectorErrorExtensions
{
    /// <summary>
    /// Get a user-friendly error explanation.
    /// </summary>
    public static string GetExplanation(this ConnectorException ex)
    {
        return ex.Code switch
        {
            ConnectorErrorCodes.SourceNotFound => 
                $"The source '{ex.SourcePath}' could not be found. Check the path exists and is accessible.",
            
            ConnectorErrorCodes.FormatInvalid => 
                "The file format is not recognized or is corrupted. Try a different connector or check the file.",
            
            ConnectorErrorCodes.NoScalars => 
                "No scalar metrics were found in the source. Ensure the training logs include metrics like loss or accuracy.",
            
            ConnectorErrorCodes.SignalMappingFailed => 
                "Could not map source metrics to ScalarScope signals. Try a different preset or configure custom mappings.",
            
            ConnectorErrorCodes.IncompleteTrace => 
                "The training data is incomplete. Some required fields are missing.",
            
            ConnectorErrorCodes.StreamingInterrupted => 
                "The live connection was interrupted. Check the training process is still running.",
            
            ConnectorErrorCodes.NoConnectorFound => 
                "No connector could handle this source format. Supported formats: TensorBoard, MLflow, CSV/JSON logs.",
            
            ConnectorErrorCodes.PermissionDenied => 
                $"Permission denied when accessing '{ex.SourcePath}'. Check file permissions.",
            
            _ => ex.Message
        };
    }
    
    /// <summary>
    /// Get suggested recovery actions.
    /// </summary>
    public static IReadOnlyList<string> GetRecoveryActions(this ConnectorException ex)
    {
        return ex.Code switch
        {
            ConnectorErrorCodes.SourceNotFound => new[]
            {
                "Verify the file or directory path is correct",
                "Check that the file hasn't been moved or deleted",
                "Ensure you have read permissions"
            },
            
            ConnectorErrorCodes.FormatInvalid => new[]
            {
                "Try selecting a specific connector type",
                "Check the file isn't corrupted",
                "Ensure the training framework was configured to export logs"
            },
            
            ConnectorErrorCodes.NoScalars => new[]
            {
                "Verify your training script logs metrics",
                "Check the metric names match the preset",
                "Try the 'Generic Logs' preset with custom mapping"
            },
            
            ConnectorErrorCodes.SignalMappingFailed => new[]
            {
                "Select a different preset for your framework",
                "Configure custom metric-to-signal mappings",
                "Check available metrics match preset expectations"
            },
            
            _ => Array.Empty<string>()
        };
    }
}
