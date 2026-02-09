namespace ScalarScope.Services;

/// <summary>
/// Phase 6.2: Comprehensive error state mapping.
/// Maps internal errors to user-understandable states.
/// </summary>
public static class ErrorStateMapping
{
    /// <summary>
    /// All known error states in ScalarScope.
    /// </summary>
    public static IReadOnlyDictionary<ErrorCategory, IReadOnlyList<ErrorState>> AllStates => _errorStates;
    
    private static readonly Dictionary<ErrorCategory, IReadOnlyList<ErrorState>> _errorStates = new()
    {
        [ErrorCategory.FileLoad] = new List<ErrorState>
        {
            new()
            {
                Code = "FILE_NOT_FOUND",
                Category = ErrorCategory.FileLoad,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "File not found at specified path",
                UserTitle = "File Not Found",
                UserExplanation = "We couldn't find the file you selected. It may have been moved, renamed, or deleted.",
                SuggestedActions = ["Check if the file exists", "Try selecting the file again", "Check if you have permission to access this location"],
                IsRecoverable = true
            },
            new()
            {
                Code = "FILE_FORMAT_INVALID",
                Category = ErrorCategory.FileLoad,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "File format does not match expected schema",
                UserTitle = "Unrecognized File Format",
                UserExplanation = "This file doesn't appear to be a valid training run. Make sure you're opening a file exported from your training system.",
                SuggestedActions = ["Verify the file is a valid training export", "Check if the file was corrupted", "Try a different file"],
                IsRecoverable = true
            },
            new()
            {
                Code = "FILE_CORRUPTED",
                Category = ErrorCategory.FileLoad,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "File appears corrupted or truncated",
                UserTitle = "File Appears Damaged",
                UserExplanation = "This file seems to be incomplete or damaged. It may have been interrupted during save.",
                SuggestedActions = ["Try the original file if you have a backup", "Re-export from your training system", "Check disk for errors"],
                IsRecoverable = true
            },
            new()
            {
                Code = "FILE_TOO_LARGE",
                Category = ErrorCategory.FileLoad,
                Severity = ErrorSeverity.Warning,
                TechnicalMessage = "File exceeds recommended size limit",
                UserTitle = "Large File Warning",
                UserExplanation = "This file is very large and may take longer to load. Performance might be affected.",
                SuggestedActions = ["Continue anyway", "Use a sampled version if available", "Export a smaller time range"],
                IsRecoverable = true
            }
        },
        
        [ErrorCategory.DataProcessing] = new List<ErrorState>
        {
            new()
            {
                Code = "EMPTY_TRAJECTORY",
                Category = ErrorCategory.DataProcessing,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "Trajectory contains no timesteps",
                UserTitle = "Empty Training Run",
                UserExplanation = "This file doesn't contain any training data. It might be from a run that didn't start properly.",
                SuggestedActions = ["Verify training actually ran", "Check export configuration", "Try a different run"],
                IsRecoverable = true
            },
            new()
            {
                Code = "INVALID_TIMESTEPS",
                Category = ErrorCategory.DataProcessing,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "Timestep data contains invalid values",
                UserTitle = "Invalid Data Points",
                UserExplanation = "Some data points in this file are invalid (infinite or undefined values). This usually means something went wrong during training.",
                SuggestedActions = ["Check training logs for errors", "Try a run from before the issue", "Contact support with details"],
                IsRecoverable = false
            },
            new()
            {
                Code = "DIMENSION_MISMATCH",
                Category = ErrorCategory.DataProcessing,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "State dimensions inconsistent across timesteps",
                UserTitle = "Inconsistent Data Structure",
                UserExplanation = "The data structure changes partway through this file, which shouldn't happen in normal training.",
                SuggestedActions = ["This may indicate a bug in your training code", "Try exporting again", "Use a different run"],
                IsRecoverable = false
            }
        },
        
        [ErrorCategory.Comparison] = new List<ErrorState>
        {
            new()
            {
                Code = "ALIGNMENT_FAILED",
                Category = ErrorCategory.Comparison,
                Severity = ErrorSeverity.Warning,
                TechnicalMessage = "Could not align runs using selected method",
                UserTitle = "Alignment Issue",
                UserExplanation = "We had trouble matching up these two runs. The comparison will still work, but may be less accurate.",
                SuggestedActions = ["Try a different alignment mode", "Check that runs are comparable", "Proceed with caution"],
                IsRecoverable = true
            },
            new()
            {
                Code = "RUNS_TOO_DIFFERENT",
                Category = ErrorCategory.Comparison,
                Severity = ErrorSeverity.Warning,
                TechnicalMessage = "Run characteristics differ significantly",
                UserTitle = "Very Different Runs",
                UserExplanation = "These runs are quite different in length or structure. The comparison may not be meaningful.",
                SuggestedActions = ["Verify you're comparing the right runs", "Consider if comparison makes sense", "Check run configurations"],
                IsRecoverable = true
            },
            new()
            {
                Code = "DELTA_COMPUTATION_TIMEOUT",
                Category = ErrorCategory.Comparison,
                Severity = ErrorSeverity.Warning,
                TechnicalMessage = "Delta computation exceeded time limit",
                UserTitle = "Analysis Taking Too Long",
                UserExplanation = "The comparison is taking longer than expected. This might happen with very large files.",
                SuggestedActions = ["Wait a bit longer", "Try with smaller files", "Restart and try again"],
                IsRecoverable = true
            }
        },
        
        [ErrorCategory.Export] = new List<ErrorState>
        {
            new()
            {
                Code = "EXPORT_WRITE_FAILED",
                Category = ErrorCategory.Export,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "Failed to write export file",
                UserTitle = "Couldn't Save File",
                UserExplanation = "We couldn't save your export. The destination might be full or you might not have permission to write there.",
                SuggestedActions = ["Check available disk space", "Try a different location", "Check folder permissions"],
                IsRecoverable = true
            },
            new()
            {
                Code = "EXPORT_RENDER_FAILED",
                Category = ErrorCategory.Export,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "Image rendering failed during export",
                UserTitle = "Export Failed",
                UserExplanation = "Something went wrong while creating your export image. This is usually a temporary issue.",
                SuggestedActions = ["Try exporting again", "Try a smaller resolution", "Restart the application"],
                IsRecoverable = true
            }
        },
        
        [ErrorCategory.System] = new List<ErrorState>
        {
            new()
            {
                Code = "OUT_OF_MEMORY",
                Category = ErrorCategory.System,
                Severity = ErrorSeverity.Critical,
                TechnicalMessage = "System ran out of available memory",
                UserTitle = "Not Enough Memory",
                UserExplanation = "Your computer doesn't have enough memory for this operation. Try closing other applications.",
                SuggestedActions = ["Close other applications", "Try with smaller files", "Restart and try again"],
                IsRecoverable = true
            },
            new()
            {
                Code = "UNEXPECTED_ERROR",
                Category = ErrorCategory.System,
                Severity = ErrorSeverity.Error,
                TechnicalMessage = "An unexpected error occurred",
                UserTitle = "Something Went Wrong",
                UserExplanation = "An unexpected error occurred. This information has been recorded for debugging.",
                SuggestedActions = ["Try the operation again", "Restart the application", "Contact support if this persists"],
                IsRecoverable = true
            }
        }
    };
    
    /// <summary>
    /// Get error state by code.
    /// </summary>
    public static ErrorState? GetByCode(string code)
    {
        foreach (var category in _errorStates.Values)
        {
            var state = category.FirstOrDefault(s => s.Code == code);
            if (state != null) return state;
        }
        return null;
    }
    
    /// <summary>
    /// Map an exception to an error state.
    /// </summary>
    public static ErrorState MapException(Exception ex)
    {
        return ex switch
        {
            FileNotFoundException => GetByCode("FILE_NOT_FOUND")!,
            DirectoryNotFoundException => GetByCode("FILE_NOT_FOUND")!,
            UnauthorizedAccessException => GetByCode("EXPORT_WRITE_FAILED")!,
            OutOfMemoryException => GetByCode("OUT_OF_MEMORY")!,
            IOException io when io.Message.Contains("disk") => GetByCode("EXPORT_WRITE_FAILED")!,
            TimeoutException => GetByCode("DELTA_COMPUTATION_TIMEOUT")!,
            _ => GetByCode("UNEXPECTED_ERROR")! with 
            { 
                TechnicalMessage = ex.Message,
                UserExplanation = $"An unexpected error occurred: {ex.Message}"
            }
        };
    }
}

/// <summary>
/// Error category for grouping.
/// </summary>
public enum ErrorCategory
{
    FileLoad,
    DataProcessing,
    Comparison,
    Export,
    System
}

/// <summary>
/// Error severity level.
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Complete error state definition.
/// </summary>
public record ErrorState
{
    public required string Code { get; init; }
    public required ErrorCategory Category { get; init; }
    public required ErrorSeverity Severity { get; init; }
    public required string TechnicalMessage { get; init; }
    public required string UserTitle { get; init; }
    public required string UserExplanation { get; init; }
    public required string[] SuggestedActions { get; init; }
    public bool IsRecoverable { get; init; }
    
    /// <summary>
    /// Generate user-facing error message.
    /// </summary>
    public string ToUserMessage()
    {
        return $"{UserTitle}\n\n{UserExplanation}\n\nYou can try:\n• {string.Join("\n• ", SuggestedActions)}";
    }
}
