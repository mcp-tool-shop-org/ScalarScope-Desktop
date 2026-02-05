using System.Text.Json;
using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Validates geometry run files before loading.
/// Provides human-readable error messages with actionable suggestions.
/// </summary>
public static class FileValidationService
{
    private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100 MB
    private const long LargeFileWarningBytes = 20 * 1024 * 1024; // 20 MB
    private const int MaxTimesteps = 50_000;

    public static async Task<FileValidationResult> ValidateAndLoadAsync(string path)
    {
        // Step 1: File existence
        if (!File.Exists(path))
        {
            return FileValidationResult.Error(
                "File Not Found",
                $"The file '{Path.GetFileName(path)}' doesn't exist or was moved.",
                ["Check that the file path is correct", "Try selecting the file again using the file picker"]);
        }

        // Step 2: File size check
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length == 0)
        {
            return FileValidationResult.Error(
                "Empty File",
                "The selected file is empty (0 bytes).",
                ["Make sure the export completed successfully", "Try re-exporting from your training pipeline"]);
        }

        if (fileInfo.Length > MaxFileSizeBytes)
        {
            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);
            return FileValidationResult.Error(
                "File Too Large",
                $"This file is {sizeMB:F1} MB, which exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit.",
                ["Try exporting with fewer timesteps", "Split the training run into smaller segments", "Contact support if you need to visualize larger runs"]);
        }

        // Step 3: File extension check
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".json")
        {
            return FileValidationResult.Error(
                "Wrong File Type",
                $"Expected a .json file, but got '{extension}'.",
                ["Select a JSON file exported from your training pipeline", "ScalarScope only supports .json geometry exports"]);
        }

        // Step 4: Read file content
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (IOException ex)
        {
            return FileValidationResult.Error(
                "Cannot Read File",
                "The file is locked or inaccessible.",
                ["Close any other programs that might have the file open", "Check file permissions", $"Technical: {ex.Message}"]);
        }
        catch (UnauthorizedAccessException)
        {
            return FileValidationResult.Error(
                "Access Denied",
                "ScalarScope doesn't have permission to read this file.",
                ["Try moving the file to your Documents folder", "Run ScalarScope as administrator", "Check file permissions in File Explorer"]);
        }

        // Step 5: Basic JSON validation
        if (string.IsNullOrWhiteSpace(json))
        {
            return FileValidationResult.Error(
                "Empty Content",
                "The file exists but contains no data.",
                ["The export may have been interrupted", "Try re-exporting from your training pipeline"]);
        }

        // Check for truncated JSON (common issue)
        var trimmed = json.TrimEnd();
        if (!trimmed.EndsWith('}') && !trimmed.EndsWith(']'))
        {
            return FileValidationResult.Error(
                "Truncated File",
                "The file appears to be incomplete - it doesn't end properly.",
                ["The export may have been interrupted", "Check available disk space during export", "Try re-exporting from your training pipeline"]);
        }

        // Step 6: JSON parsing
        GeometryRun? run;
        try
        {
            run = JsonSerializer.Deserialize<GeometryRun>(json);
        }
        catch (JsonException ex)
        {
            var (line, position) = ExtractJsonErrorPosition(ex.Message);
            var context = GetErrorContext(json, ex);

            return FileValidationResult.Error(
                "Invalid JSON Format",
                $"The file contains a syntax error{(line > 0 ? $" near line {line}" : "")}.",
                [$"Error: {SimplifyJsonError(ex.Message)}", context, "Try re-exporting from your training pipeline", "Validate the JSON at jsonlint.com if you edited it manually"]);
        }

        if (run == null)
        {
            return FileValidationResult.Error(
                "Parse Failed",
                "The JSON parsed but produced no data.",
                ["The file may be empty or contain only null", "Try re-exporting from your training pipeline"]);
        }

        // Step 7: Schema validation
        var schemaErrors = ValidateSchema(run);
        if (schemaErrors.Count > 0)
        {
            return FileValidationResult.Error(
                "Schema Mismatch",
                "The file structure doesn't match what ScalarScope expects.",
                schemaErrors.Concat(["Make sure you're loading a ScalarScope geometry export", "Check the export version compatibility"]).ToList());
        }

        // Step 8: Data validation
        var dataWarnings = ValidateData(run);

        // Step 9: Performance warnings
        if (fileInfo.Length > LargeFileWarningBytes)
        {
            dataWarnings.Add($"Large file ({fileInfo.Length / (1024.0 * 1024.0):F1} MB) - playback may be slower");
        }

        if (run.Trajectory.Timesteps.Count > MaxTimesteps)
        {
            dataWarnings.Add($"Many timesteps ({run.Trajectory.Timesteps.Count:N0}) - consider using frame skipping");
        }

        return FileValidationResult.Success(run, dataWarnings);
    }

    private static List<string> ValidateSchema(GeometryRun run)
    {
        var errors = new List<string>();

        // Check required sections exist with data
        if (run.Trajectory.Timesteps.Count == 0)
        {
            errors.Add("Missing trajectory data - no timesteps found");
        }

        if (run.Trajectory.Timesteps.Count > 0 && run.Trajectory.Timesteps[0].State2D.Count < 2)
        {
            errors.Add("Trajectory needs at least 2D coordinates (State2D)");
        }

        if (string.IsNullOrEmpty(run.Metadata.RunId) && string.IsNullOrEmpty(run.Metadata.Condition))
        {
            errors.Add("Missing run metadata (run_id or condition)");
        }

        // Check schema version compatibility
        if (!string.IsNullOrEmpty(run.SchemaVersion))
        {
            var version = run.SchemaVersion.Split('.').FirstOrDefault();
            if (version != "1")
            {
                errors.Add($"Schema version {run.SchemaVersion} may not be fully compatible (expected 1.x)");
            }
        }

        return errors;
    }

    private static List<string> ValidateData(GeometryRun run)
    {
        var warnings = new List<string>();

        // Check for NaN/Infinity values in trajectory
        var hasInvalidValues = run.Trajectory.Timesteps.Any(t =>
            t.State2D.Any(v => double.IsNaN(v) || double.IsInfinity(v)));

        if (hasInvalidValues)
        {
            warnings.Add("Some trajectory values are NaN or Infinity - visualization may be incomplete");
        }

        // Check for empty eigenvalues
        if (run.Geometry.Eigenvalues.Count == 0)
        {
            warnings.Add("No eigenvalue data - Eigen-Spectrum view will be empty");
        }

        // Check for missing scalars
        if (run.Scalars.Values.Count == 0)
        {
            warnings.Add("No scalar metrics - some visualizations may be limited");
        }

        // Check evaluator data
        if (run.Evaluators.Professors.Count == 0)
        {
            warnings.Add("No evaluator vectors - professor arrows won't be shown");
        }

        return warnings;
    }

    private static (int line, int position) ExtractJsonErrorPosition(string errorMessage)
    {
        // JsonException messages often contain "line X, position Y"
        var match = System.Text.RegularExpressions.Regex.Match(
            errorMessage, @"line (\d+).*position (\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int line) &&
            int.TryParse(match.Groups[2].Value, out int position))
        {
            return (line, position);
        }

        return (0, 0);
    }

    private static string GetErrorContext(string json, JsonException ex)
    {
        try
        {
            if (ex.BytePositionInLine.HasValue && ex.LineNumber.HasValue)
            {
                var lines = json.Split('\n');
                var lineIndex = (int)ex.LineNumber.Value - 1;
                if (lineIndex >= 0 && lineIndex < lines.Length)
                {
                    var line = lines[lineIndex];
                    if (line.Length > 60)
                    {
                        var start = Math.Max(0, (int)ex.BytePositionInLine.Value - 30);
                        line = "..." + line.Substring(start, Math.Min(60, line.Length - start)) + "...";
                    }
                    return $"Near: {line.Trim()}";
                }
            }
        }
        catch
        {
            // Ignore context extraction errors
        }

        return "";
    }

    private static string SimplifyJsonError(string errorMessage)
    {
        // Make common JSON errors more readable
        if (errorMessage.Contains("'}'"))
            return "Unexpected closing brace - check for missing commas or quotes";
        if (errorMessage.Contains("']'"))
            return "Unexpected closing bracket - check array formatting";
        if (errorMessage.Contains("expected"))
            return errorMessage.Split("Path:")[0].Trim();
        if (errorMessage.Contains("could not be converted"))
            return "Wrong data type - expected a number or string";

        // Return first sentence only
        var firstPeriod = errorMessage.IndexOf('.');
        return firstPeriod > 0 ? errorMessage.Substring(0, firstPeriod + 1) : errorMessage;
    }
}

/// <summary>
/// Result of file validation with detailed error information.
/// </summary>
public class FileValidationResult
{
    public bool IsSuccess { get; private init; }
    public GeometryRun? Run { get; private init; }
    public string? ErrorTitle { get; private init; }
    public string? ErrorMessage { get; private init; }
    public List<string> Suggestions { get; private init; } = [];
    public List<string> Warnings { get; private init; } = [];

    private FileValidationResult() { }

    public static FileValidationResult Success(GeometryRun run, List<string>? warnings = null) => new()
    {
        IsSuccess = true,
        Run = run,
        Warnings = warnings ?? []
    };

    public static FileValidationResult Error(string title, string message, List<string>? suggestions = null) => new()
    {
        IsSuccess = false,
        ErrorTitle = title,
        ErrorMessage = message,
        Suggestions = suggestions ?? []
    };

    /// <summary>
    /// Formats error for display in UI.
    /// </summary>
    public string GetFormattedError()
    {
        if (IsSuccess) return "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ErrorMessage);

        if (Suggestions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("What you can try:");
            foreach (var suggestion in Suggestions.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                sb.AppendLine($"• {suggestion}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
