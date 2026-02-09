// Phase 7.1.1: Canonical Import Formats
// Defines supported import schemas with versioning and validation.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.1: Service for validating and managing trajectory import schemas.
/// Supports JSON and CSV formats with explicit versioning.
/// </summary>
public static class ImportSchemaService
{
    /// <summary>
    /// Current schema version for JSON format.
    /// </summary>
    public const string JsonSchemaVersion = "1.0.0";
    
    /// <summary>
    /// Current schema version for CSV format.
    /// </summary>
    public const string CsvSchemaVersion = "1.0.0";
    
    /// <summary>
    /// Supported import formats.
    /// </summary>
    public static IReadOnlyList<ImportFormat> SupportedFormats { get; } = new[]
    {
        new ImportFormat
        {
            Id = "json-v1",
            Name = "ScalarScope JSON",
            Extensions = new[] { ".json" },
            SchemaVersion = JsonSchemaVersion,
            Description = "Native ScalarScope trajectory format with full metadata support",
            MimeType = "application/json"
        },
        new ImportFormat
        {
            Id = "csv-v1",
            Name = "CSV Trajectory",
            Extensions = new[] { ".csv" },
            SchemaVersion = CsvSchemaVersion,
            Description = "Comma-separated values with header row defining scalar names",
            MimeType = "text/csv"
        }
    };
    
    /// <summary>
    /// Validate a file against its detected schema.
    /// </summary>
    public static ImportValidationResult Validate(string filePath)
    {
        var result = new ImportValidationResult
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };
        
        try
        {
            if (!File.Exists(filePath))
            {
                result.AddError("FILE_NOT_FOUND", "File does not exist", 
                    ErrorExplanationService.Explain(
                        ErrorStateMapping.GetByCode("FILE_NOT_FOUND")));
                return result;
            }
            
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var format = SupportedFormats.FirstOrDefault(f => 
                f.Extensions.Contains(extension));
            
            if (format is null)
            {
                result.AddError("UNSUPPORTED_FORMAT", 
                    $"File extension '{extension}' is not supported. Use .json or .csv",
                    CreateUnsupportedFormatExplanation(extension));
                return result;
            }
            
            result.DetectedFormat = format;
            
            // Format-specific validation
            if (extension == ".json")
            {
                ValidateJsonSchema(filePath, result);
            }
            else if (extension == ".csv")
            {
                ValidateCsvSchema(filePath, result);
            }
        }
        catch (Exception ex)
        {
            var errorState = ErrorStateMapping.MapException(ex);
            result.AddError(errorState.Code, ex.Message, 
                ErrorExplanationService.Explain(errorState, ex));
        }
        
        return result;
    }
    
    /// <summary>
    /// Validate JSON trajectory schema.
    /// </summary>
    private static void ValidateJsonSchema(string filePath, ImportValidationResult result)
    {
        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            var errorState = ErrorStateMapping.MapException(ex);
            result.AddError(errorState.Code, $"Cannot read file: {ex.Message}",
                ErrorExplanationService.Explain(errorState, ex));
            return;
        }
        
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            result.AddError("JSON_PARSE_ERROR", $"Invalid JSON: {ex.Message}",
                CreateJsonParseErrorExplanation(ex));
            return;
        }
        
        using (doc)
        {
            var root = doc.RootElement;
            
            // Check for schema version
            if (root.TryGetProperty("schemaVersion", out var versionProp))
            {
                result.SchemaVersion = versionProp.GetString();
                
                // Validate version compatibility
                if (!IsVersionCompatible(result.SchemaVersion, JsonSchemaVersion))
                {
                    result.AddWarning("SCHEMA_VERSION_MISMATCH",
                        $"File schema version {result.SchemaVersion} differs from current {JsonSchemaVersion}");
                }
            }
            else
            {
                result.AddWarning("NO_SCHEMA_VERSION",
                    "File does not specify schema version, assuming v1.0.0");
                result.SchemaVersion = "1.0.0";
            }
            
            // Validate required fields
            if (!root.TryGetProperty("timesteps", out var timesteps))
            {
                if (!root.TryGetProperty("data", out timesteps))
                {
                    result.AddError("MISSING_TIMESTEPS",
                        "JSON must contain 'timesteps' or 'data' array",
                        CreateMissingFieldExplanation("timesteps"));
                    return;
                }
            }
            
            if (timesteps.ValueKind != JsonValueKind.Array)
            {
                result.AddError("INVALID_TIMESTEPS_TYPE",
                    "Timesteps must be an array",
                    CreateInvalidTypeExplanation("timesteps", "array"));
                return;
            }
            
            var timestepCount = timesteps.GetArrayLength();
            result.TimestepCount = timestepCount;
            
            if (timestepCount == 0)
            {
                result.AddError("EMPTY_TRAJECTORY",
                    "Trajectory contains no timesteps",
                    ErrorExplanationService.Explain(
                        ErrorStateMapping.GetByCode("EMPTY_TRAJECTORY")));
                return;
            }
            
            // Validate first timestep structure
            var firstTimestep = timesteps[0];
            ValidateTimestepStructure(firstTimestep, result);
            
            // Count scalars
            if (firstTimestep.TryGetProperty("scalars", out var scalars))
            {
                if (scalars.ValueKind == JsonValueKind.Object)
                {
                    result.ScalarCount = scalars.EnumerateObject().Count();
                    result.ScalarNames = scalars.EnumerateObject()
                        .Select(p => p.Name)
                        .ToList();
                }
                else if (scalars.ValueKind == JsonValueKind.Array)
                {
                    result.ScalarCount = scalars.GetArrayLength();
                }
            }
            
            // Extract metadata if present
            if (root.TryGetProperty("metadata", out var metadata))
            {
                result.HasMetadata = true;
                if (metadata.TryGetProperty("name", out var name))
                {
                    result.TrajectoryName = name.GetString();
                }
            }
        }
        
        result.IsValid = !result.Errors.Any();
    }
    
    /// <summary>
    /// Validate CSV trajectory schema.
    /// </summary>
    private static void ValidateCsvSchema(string filePath, ImportValidationResult result)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(filePath);
        }
        catch (Exception ex)
        {
            var errorState = ErrorStateMapping.MapException(ex);
            result.AddError(errorState.Code, $"Cannot read file: {ex.Message}",
                ErrorExplanationService.Explain(errorState, ex));
            return;
        }
        
        if (lines.Length == 0)
        {
            result.AddError("EMPTY_FILE", "CSV file is empty",
                CreateEmptyFileExplanation());
            return;
        }
        
        // Parse header
        var header = lines[0];
        var columns = ParseCsvLine(header);
        
        if (columns.Length == 0)
        {
            result.AddError("EMPTY_HEADER", "CSV header row is empty",
                CreateEmptyHeaderExplanation());
            return;
        }
        
        // First column should be timestep/index
        var firstCol = columns[0].ToLowerInvariant();
        if (firstCol != "timestep" && firstCol != "t" && firstCol != "index" && firstCol != "step")
        {
            result.AddWarning("AMBIGUOUS_TIMESTEP_COLUMN",
                $"First column '{columns[0]}' may not be a timestep identifier");
        }
        
        // Remaining columns are scalars
        result.ScalarCount = columns.Length - 1;
        result.ScalarNames = columns.Skip(1).ToList();
        result.TimestepCount = lines.Length - 1;
        result.SchemaVersion = CsvSchemaVersion;
        
        if (result.TimestepCount == 0)
        {
            result.AddError("EMPTY_TRAJECTORY",
                "CSV contains header but no data rows",
                ErrorExplanationService.Explain(
                    ErrorStateMapping.GetByCode("EMPTY_TRAJECTORY")));
            return;
        }
        
        // Validate a sample of data rows
        var sampleSize = Math.Min(10, lines.Length - 1);
        for (int i = 1; i <= sampleSize; i++)
        {
            var rowColumns = ParseCsvLine(lines[i]);
            
            if (rowColumns.Length != columns.Length)
            {
                result.AddError("COLUMN_COUNT_MISMATCH",
                    $"Row {i} has {rowColumns.Length} columns, expected {columns.Length}",
                    CreateColumnMismatchExplanation(i, rowColumns.Length, columns.Length));
                return;
            }
            
            // Validate numeric values
            for (int j = 0; j < rowColumns.Length; j++)
            {
                if (!double.TryParse(rowColumns[j], out _) && 
                    !string.IsNullOrEmpty(rowColumns[j]) &&
                    rowColumns[j].ToLowerInvariant() != "nan" &&
                    rowColumns[j].ToLowerInvariant() != "inf" &&
                    rowColumns[j].ToLowerInvariant() != "-inf")
                {
                    result.AddWarning("NON_NUMERIC_VALUE",
                        $"Row {i}, column '{columns[j]}' contains non-numeric value: {rowColumns[j]}");
                }
            }
        }
        
        result.IsValid = !result.Errors.Any();
    }
    
    private static void ValidateTimestepStructure(JsonElement timestep, ImportValidationResult result)
    {
        // Check for timestep identifier
        var hasTimestepId = timestep.TryGetProperty("t", out _) ||
                           timestep.TryGetProperty("timestep", out _) ||
                           timestep.TryGetProperty("index", out _) ||
                           timestep.TryGetProperty("step", out _);
        
        if (!hasTimestepId)
        {
            result.AddWarning("NO_TIMESTEP_ID",
                "Timesteps don't have explicit identifiers (t, timestep, index, step)");
        }
        
        // Check for scalars
        if (!timestep.TryGetProperty("scalars", out _) &&
            !timestep.TryGetProperty("values", out _) &&
            !timestep.TryGetProperty("data", out _))
        {
            // Maybe scalars are at root level of timestep
            var hasNumericProps = timestep.EnumerateObject()
                .Any(p => p.Value.ValueKind == JsonValueKind.Number);
            
            if (!hasNumericProps)
            {
                result.AddError("NO_SCALAR_DATA",
                    "Timesteps must contain scalar data",
                    CreateNoScalarDataExplanation());
            }
        }
    }
    
    private static string[] ParseCsvLine(string line)
    {
        // Simple CSV parsing (doesn't handle quoted fields with commas)
        return line.Split(',').Select(s => s.Trim()).ToArray();
    }
    
    private static bool IsVersionCompatible(string? fileVersion, string currentVersion)
    {
        if (string.IsNullOrEmpty(fileVersion)) return true;
        
        var fileParts = fileVersion.Split('.');
        var currentParts = currentVersion.Split('.');
        
        // Major version must match
        return fileParts.Length > 0 && currentParts.Length > 0 &&
               fileParts[0] == currentParts[0];
    }
    
    // Error explanation generators using Phase 6 patterns
    
    private static ErrorExplanation CreateUnsupportedFormatExplanation(string extension)
    {
        return new ErrorExplanation
        {
            Code = "UNSUPPORTED_FORMAT",
            Title = "Unsupported File Format",
            Summary = $"ScalarScope cannot import files with '{extension}' extension.",
            RootCause = "Only JSON and CSV trajectory formats are currently supported.",
            TroubleshootingSteps = new[]
            {
                "Export your data as JSON with timestep/scalars structure",
                "Export your data as CSV with header row",
                "Check TRAJECTORY_FORMAT.md for schema examples"
            }
        };
    }
    
    private static ErrorExplanation CreateJsonParseErrorExplanation(JsonException ex)
    {
        return new ErrorExplanation
        {
            Code = "JSON_PARSE_ERROR",
            Title = "Invalid JSON",
            Summary = "The file contains malformed JSON that cannot be parsed.",
            RootCause = ex.Message,
            TroubleshootingSteps = new[]
            {
                "Open the file in a text editor and check for syntax errors",
                "Use a JSON validator (jsonlint.com) to find issues",
                "Look for missing commas, brackets, or quotes",
                "Ensure the file uses UTF-8 encoding"
            },
            TechnicalNote = ex.LineNumber > 0 ? $"Error near line {ex.LineNumber}" : ""
        };
    }
    
    private static ErrorExplanation CreateMissingFieldExplanation(string fieldName)
    {
        return new ErrorExplanation
        {
            Code = "MISSING_FIELD",
            Title = "Missing Required Field",
            Summary = $"The trajectory file is missing the required '{fieldName}' field.",
            RootCause = "ScalarScope needs timestep data to perform comparisons.",
            TroubleshootingSteps = new[]
            {
                $"Add a '{fieldName}' array to your JSON file",
                "Check TRAJECTORY_FORMAT.md for the expected structure",
                "Verify your export tool is producing the correct format"
            }
        };
    }
    
    private static ErrorExplanation CreateInvalidTypeExplanation(string fieldName, string expectedType)
    {
        return new ErrorExplanation
        {
            Code = "INVALID_FIELD_TYPE",
            Title = "Invalid Field Type",
            Summary = $"Field '{fieldName}' has the wrong type.",
            RootCause = $"Expected {expectedType} but found a different type.",
            TroubleshootingSteps = new[]
            {
                $"Ensure '{fieldName}' is a {expectedType} in your JSON",
                "Check for typos or structural errors",
                "Refer to TRAJECTORY_FORMAT.md for examples"
            }
        };
    }
    
    private static ErrorExplanation CreateEmptyFileExplanation()
    {
        return new ErrorExplanation
        {
            Code = "EMPTY_FILE",
            Title = "Empty File",
            Summary = "The file contains no data.",
            RootCause = "The file may not have been written correctly or was truncated.",
            TroubleshootingSteps = new[]
            {
                "Check if the export completed successfully",
                "Try exporting the data again",
                "Verify the file size is greater than 0 bytes"
            }
        };
    }
    
    private static ErrorExplanation CreateEmptyHeaderExplanation()
    {
        return new ErrorExplanation
        {
            Code = "EMPTY_HEADER",
            Title = "Empty Header Row",
            Summary = "The CSV file has no column headers.",
            RootCause = "CSV files must have a header row defining column names.",
            TroubleshootingSteps = new[]
            {
                "Add a header row with column names",
                "First column should be 'timestep' or 't'",
                "Remaining columns should be scalar names"
            }
        };
    }
    
    private static ErrorExplanation CreateColumnMismatchExplanation(int row, int actual, int expected)
    {
        return new ErrorExplanation
        {
            Code = "COLUMN_COUNT_MISMATCH",
            Title = "Column Count Mismatch",
            Summary = $"Row {row} has an inconsistent number of columns.",
            RootCause = $"Found {actual} columns but header defines {expected}.",
            TroubleshootingSteps = new[]
            {
                $"Check row {row} for missing or extra values",
                "Ensure all rows have the same number of columns",
                "Look for unescaped commas in values"
            }
        };
    }
    
    private static ErrorExplanation CreateNoScalarDataExplanation()
    {
        return new ErrorExplanation
        {
            Code = "NO_SCALAR_DATA",
            Title = "No Scalar Data Found",
            Summary = "Timesteps don't contain any scalar values.",
            RootCause = "Each timestep must have numeric scalar data to compare.",
            TroubleshootingSteps = new[]
            {
                "Add a 'scalars' object to each timestep",
                "Include numeric values for each tracked scalar",
                "Check TRAJECTORY_FORMAT.md for the expected structure"
            }
        };
    }
}

/// <summary>
/// Describes a supported import format.
/// </summary>
public record ImportFormat
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string[] Extensions { get; init; }
    public required string SchemaVersion { get; init; }
    public required string Description { get; init; }
    public required string MimeType { get; init; }
}

/// <summary>
/// Result of import validation.
/// </summary>
public class ImportValidationResult
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public ImportFormat? DetectedFormat { get; set; }
    public string? SchemaVersion { get; set; }
    public bool IsValid { get; set; }
    public int TimestepCount { get; set; }
    public int ScalarCount { get; set; }
    public List<string>? ScalarNames { get; set; }
    public string? TrajectoryName { get; set; }
    public bool HasMetadata { get; set; }
    
    public List<ImportValidationIssue> Errors { get; } = new();
    public List<ImportValidationIssue> Warnings { get; } = new();
    
    public void AddError(string code, string message, ErrorExplanation? explanation = null)
    {
        Errors.Add(new ImportValidationIssue
        {
            Code = code,
            Message = message,
            Severity = ValidationSeverity.Error,
            Explanation = explanation
        });
    }
    
    public void AddWarning(string code, string message, ErrorExplanation? explanation = null)
    {
        Warnings.Add(new ImportValidationIssue
        {
            Code = code,
            Message = message,
            Severity = ValidationSeverity.Warning,
            Explanation = explanation
        });
    }
    
    public string GetSummary()
    {
        if (!IsValid)
        {
            return $"Invalid: {Errors.First().Message}";
        }
        
        return $"{TimestepCount} timesteps, {ScalarCount} scalars ({DetectedFormat?.Name ?? "Unknown"})";
    }
}

/// <summary>
/// A validation issue found during import.
/// </summary>
public record ImportValidationIssue
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required ValidationSeverity Severity { get; init; }
    public ErrorExplanation? Explanation { get; init; }
}

/// <summary>
/// Severity of a validation issue.
/// </summary>
public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
