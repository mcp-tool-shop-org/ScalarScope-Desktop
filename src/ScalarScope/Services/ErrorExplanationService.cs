// Phase 6.2.3: Error Explanation Service
// Provides detailed "What happened?" explanations for user errors.

using System.Text;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.2: Service that generates detailed, user-friendly error explanations.
/// Answers "What happened?", "Why?", and "What can I do?" for each error.
/// </summary>
public static class ErrorExplanationService
{
    /// <summary>
    /// Generate a detailed explanation for an error state.
    /// </summary>
    public static ErrorExplanation Explain(ErrorState error, Exception? originalException = null)
    {
        var builder = new ErrorExplanationBuilder(error, originalException);
        
        return error.Code switch
        {
            // File loading errors
            "FILE_NOT_FOUND" => builder
                .WithSummary("The file you selected could not be found on disk.")
                .WithRootCause("The file may have been moved, renamed, or deleted since you last accessed it.")
                .AddStep("Check if the file exists at the path shown")
                .AddStep("Verify the file wasn't moved to a different folder")
                .AddStep("If the file was on a network drive, check your connection")
                .AddStep("Try selecting the file again using the file picker")
                .Build(),
                
            "FILE_ACCESS_DENIED" => builder
                .WithSummary("ScalarScope doesn't have permission to read this file.")
                .WithRootCause("The file is protected by system permissions, or another program has it locked.")
                .AddStep("Close any other programs that might have the file open")
                .AddStep("Right-click the file and check its security properties")
                .AddStep("Try copying the file to a different location (like Documents)")
                .AddStep("Run ScalarScope as administrator if the file requires elevated access")
                .Build(),
                
            "FILE_FORMAT_INVALID" => builder
                .WithSummary("The file format is not recognized or is corrupted.")
                .WithRootCause("The file may not be a valid trajectory file, or it may have been damaged.")
                .AddStep("Verify the file is a supported format (JSON or CSV)")
                .AddStep("Open the file in a text editor to check for obvious corruption")
                .AddStep("If the file was downloaded, try downloading it again")
                .AddStep("Check TRAJECTORY_FORMAT.md for the expected file structure")
                .Build(),
                
            "FILE_TOO_LARGE" => builder
                .WithSummary("The file is too large to process with available memory.")
                .WithRootCause("ScalarScope needs to load trajectory data into memory for comparison.")
                .AddStep("Close other applications to free up memory")
                .AddStep("Split the trajectory into smaller segments")
                .AddStep("Sample every Nth timestep to reduce data size")
                .AddStep("Use a machine with more RAM for very large trajectories")
                .WithNote("Current limit: ~100MB per trajectory file")
                .Build(),
                
            // Data processing errors
            "EMPTY_TRAJECTORY" => builder
                .WithSummary("The trajectory file contains no data points.")
                .WithRootCause("The file loaded successfully but has no timesteps to compare.")
                .AddStep("Check that the file has the expected data format")
                .AddStep("Verify timestep entries exist in the file")
                .AddStep("If exporting from another tool, check export settings")
                .Build(),
                
            "MALFORMED_DATA" => builder
                .WithSummary("Some data in the trajectory couldn't be parsed correctly.")
                .WithRootCause("The file contains invalid numbers, missing fields, or unexpected format.")
                .AddStep("Check for NaN or Inf values in your scalar data")
                .AddStep("Verify all required fields (timestep, scalars) are present")
                .AddStep("Look for encoding issues (use UTF-8 encoding)")
                .AddStep("Check for incomplete lines at the end of the file")
                .WithNote(GetMalformedDataDetails(originalException))
                .Build(),
                
            "DIMENSION_MISMATCH" => builder
                .WithSummary("The trajectories have different numbers of scalars and can't be compared directly.")
                .WithRootCause("Comparison requires both trajectories to have the same scalar count.")
                .AddStep("Verify both files track the same set of scalars")
                .AddStep("Check if one file has additional or missing scalar columns")
                .AddStep("Ensure consistent field ordering between runs")
                .WithNote("Trajectory 1 and Trajectory 2 must have identical scalar schemas")
                .Build(),
                
            // Comparison errors
            "ALIGNMENT_FAILED" => builder
                .WithSummary("ScalarScope couldn't align the two trajectories for comparison.")
                .WithRootCause("The trajectories may have incompatible time ranges or indexing schemes.")
                .AddStep("Check that both trajectories cover overlapping time ranges")
                .AddStep("Verify timestep values are consistent (e.g., both use frame numbers)")
                .AddStep("Try enabling 'Force Alignment' in settings")
                .AddStep("Manually trim trajectories to matching ranges")
                .Build(),
                
            "COMPUTATION_TIMEOUT" => builder
                .WithSummary("The comparison took too long and was cancelled.")
                .WithRootCause("Very large trajectories or complex analysis can exceed time limits.")
                .AddStep("Try comparing smaller trajectory segments")
                .AddStep("Disable advanced analysis features like eigenvalue decomposition")
                .AddStep("Increase timeout in Settings > Performance")
                .AddStep("Consider using the 'Quick Compare' mode for initial analysis")
                .WithNote("Default timeout: 60 seconds")
                .Build(),
                
            "NUMERICAL_INSTABILITY" => builder
                .WithSummary("The computation encountered numerical issues (overflow/underflow).")
                .WithRootCause("Extreme scalar values can cause floating-point precision problems.")
                .AddStep("Check for very large values (>1e38) in your data")
                .AddStep("Check for very small values (<1e-38) that might underflow")
                .AddStep("Normalize your data to a reasonable range before export")
                .AddStep("Look for divide-by-zero scenarios in the source data")
                .Build(),
                
            // Export errors
            "EXPORT_PATH_INVALID" => builder
                .WithSummary("The export location is not valid or accessible.")
                .WithRootCause("The folder path doesn't exist or isn't writable.")
                .AddStep("Choose an existing folder for export")
                .AddStep("Check you have write permission to the folder")
                .AddStep("Try exporting to your Documents folder")
                .AddStep("Ensure the path doesn't contain invalid characters")
                .Build(),
                
            "EXPORT_FAILED" => builder
                .WithSummary("The export operation couldn't complete.")
                .WithRootCause("A problem occurred while writing the export files.")
                .AddStep("Check available disk space")
                .AddStep("Verify no other program is blocking the export folder")
                .AddStep("Try a different export format")
                .AddStep("Export to a local drive instead of network/cloud storage")
                .Build(),
                
            "DISK_FULL" => builder
                .WithSummary("There's not enough disk space to save the export.")
                .WithRootCause("The destination drive has insufficient free space.")
                .AddStep("Free up disk space by removing unnecessary files")
                .AddStep("Export to a different drive with more space")
                .AddStep("Use compressed export formats (PNG vs BMP)")
                .Build(),
                
            // System errors  
            "OUT_OF_MEMORY" => builder
                .WithSummary("The computer ran out of memory during operation.")
                .WithRootCause("The operation required more RAM than currently available.")
                .AddStep("Close other applications to free memory")
                .AddStep("Work with smaller trajectory segments")
                .AddStep("Restart ScalarScope to clear memory")
                .AddStep("Consider upgrading system RAM for large datasets")
                .Build(),
                
            "INTERNAL_ERROR" => builder
                .WithSummary("An unexpected error occurred inside ScalarScope.")
                .WithRootCause("This is likely a bug in ScalarScope that we'd like to fix.")
                .AddStep("Try the operation again - it may succeed on retry")
                .AddStep("Restart ScalarScope if the problem persists")
                .AddStep("Report this issue with the error details below")
                .WithNote(GetInternalErrorDetails(originalException))
                .WithBugReportPrompt()
                .Build(),
                
            "INITIALIZATION_FAILED" => builder
                .WithSummary("ScalarScope couldn't start a required component.")
                .WithRootCause("A service or resource failed to initialize properly.")
                .AddStep("Restart ScalarScope")
                .AddStep("Check that all required files are present")
                .AddStep("Reinstall ScalarScope if the problem persists")
                .AddStep("Check system event logs for more details")
                .Build(),
                
            _ => builder
                .WithSummary($"An error occurred: {error.UserExplanation}")
                .WithRootCause("The specific cause couldn't be determined.")
                .AddStep("Try the operation again")
                .AddStep("Restart ScalarScope if needed")
                .AddStep("Check the error log for details")
                .Build()
        };
    }
    
    /// <summary>
    /// Generate explanation from an exception without prior ErrorState mapping.
    /// </summary>
    public static ErrorExplanation ExplainException(Exception ex)
    {
        var errorState = ErrorStateMapping.MapException(ex);
        return Explain(errorState, ex);
    }
    
    private static string GetMalformedDataDetails(Exception? ex)
    {
        if (ex is null) return "";
        
        // Extract line/position info if available
        var message = ex.Message;
        if (message.Contains("line") || message.Contains("position"))
        {
            return $"Parser info: {message}";
        }
        return "";
    }
    
    private static string GetInternalErrorDetails(Exception? ex)
    {
        if (ex is null) return "";
        
        var sb = new StringBuilder();
        sb.AppendLine($"Error type: {ex.GetType().Name}");
        sb.AppendLine($"Message: {ex.Message}");
        
        if (ex.StackTrace is not null)
        {
            // Show just the first frame for context
            var firstFrame = ex.StackTrace.Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstFrame))
            {
                sb.AppendLine($"Location: {firstFrame}");
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// A detailed explanation of an error suitable for display to users.
/// </summary>
public record ErrorExplanation
{
    /// <summary>
    /// The original error state code.
    /// </summary>
    public required string Code { get; init; }
    
    /// <summary>
    /// User-friendly error title.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// One-sentence summary of what went wrong.
    /// </summary>
    public required string Summary { get; init; }
    
    /// <summary>
    /// Explanation of the root cause.
    /// </summary>
    public required string RootCause { get; init; }
    
    /// <summary>
    /// Ordered list of troubleshooting steps.
    /// </summary>
    public required IReadOnlyList<string> TroubleshootingSteps { get; init; }
    
    /// <summary>
    /// Additional technical note (may be empty).
    /// </summary>
    public string TechnicalNote { get; init; } = "";
    
    /// <summary>
    /// Whether to show bug report option.
    /// </summary>
    public bool ShowBugReportPrompt { get; init; }
    
    /// <summary>
    /// Original exception (for logging).
    /// </summary>
    public Exception? OriginalException { get; init; }
    
    /// <summary>
    /// Format as user-readable text.
    /// </summary>
    public string ToDetailedText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {Title}");
        sb.AppendLine();
        sb.AppendLine($"**What happened:** {Summary}");
        sb.AppendLine();
        sb.AppendLine($"**Why:** {RootCause}");
        sb.AppendLine();
        sb.AppendLine("**What you can do:**");
        for (int i = 0; i < TroubleshootingSteps.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {TroubleshootingSteps[i]}");
        }
        
        if (!string.IsNullOrEmpty(TechnicalNote))
        {
            sb.AppendLine();
            sb.AppendLine($"*Note: {TechnicalNote}*");
        }
        
        if (ShowBugReportPrompt)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("If this problem persists, please report it at:");
            sb.AppendLine("https://github.com/your-repo/ScalarScope/issues");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Format as concise text for dialog display.
    /// </summary>
    public string ToConciseText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Summary);
        sb.AppendLine();
        sb.AppendLine("Try:");
        foreach (var step in TroubleshootingSteps.Take(3))
        {
            sb.AppendLine($"• {step}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Builder for constructing ErrorExplanation instances.
/// </summary>
internal class ErrorExplanationBuilder
{
    private readonly ErrorState _error;
    private readonly Exception? _exception;
    private string _summary = "";
    private string _rootCause = "";
    private readonly List<string> _steps = new();
    private string _note = "";
    private bool _showBugReport;
    
    public ErrorExplanationBuilder(ErrorState error, Exception? exception)
    {
        _error = error;
        _exception = exception;
    }
    
    public ErrorExplanationBuilder WithSummary(string summary)
    {
        _summary = summary;
        return this;
    }
    
    public ErrorExplanationBuilder WithRootCause(string cause)
    {
        _rootCause = cause;
        return this;
    }
    
    public ErrorExplanationBuilder AddStep(string step)
    {
        _steps.Add(step);
        return this;
    }
    
    public ErrorExplanationBuilder WithNote(string note)
    {
        if (!string.IsNullOrEmpty(note))
        {
            _note = note;
        }
        return this;
    }
    
    public ErrorExplanationBuilder WithBugReportPrompt()
    {
        _showBugReport = true;
        return this;
    }
    
    public ErrorExplanation Build()
    {
        return new ErrorExplanation
        {
            Code = _error.Code,
            Title = _error.UserTitle,
            Summary = _summary,
            RootCause = _rootCause,
            TroubleshootingSteps = _steps.AsReadOnly(),
            TechnicalNote = _note,
            ShowBugReportPrompt = _showBugReport,
            OriginalException = _exception
        };
    }
}
