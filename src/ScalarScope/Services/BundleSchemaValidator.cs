// Phase 7.2.1: Bundle Schema Validator
// Validates bundle structure and schema compliance.
// Mirrors ImportSchemaService pattern for consistency.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.2: Validator for comparison bundle structure and schema.
/// Follows same pattern as ImportSchemaService for consistency.
/// </summary>
public static class BundleSchemaValidator
{
    /// <summary>
    /// Minimum supported bundle version.
    /// </summary>
    public const string MinSupportedVersion = "1.0.0";
    
    /// <summary>
    /// Maximum supported bundle version (current).
    /// </summary>
    public const string MaxSupportedVersion = ComparisonBundleService.BundleSpecVersion;
    
    /// <summary>
    /// Required files for a valid bundle.
    /// </summary>
    private static readonly string[] RequiredFiles = 
    {
        "manifest.json",
        "findings/deltas.json",
        "findings/why.json",
        "repro/repro.json",
        "integrity.json"
    };
    
    /// <summary>
    /// Validate a bundle file.
    /// </summary>
    public static async Task<BundleValidationResult> ValidateAsync(string bundlePath)
    {
        var result = new BundleValidationResult
        {
            BundlePath = bundlePath,
            ValidatedAt = DateTime.UtcNow
        };
        
        // Check file exists
        if (!File.Exists(bundlePath))
        {
            result.AddError("FileNotFound", "Bundle file does not exist", 
                $"The file '{bundlePath}' was not found.");
            return result;
        }
        
        // Check extension
        if (!bundlePath.EndsWith(ComparisonBundleService.BundleExtension, StringComparison.OrdinalIgnoreCase) &&
            !bundlePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            result.AddWarning("UnexpectedExtension", 
                $"Expected {ComparisonBundleService.BundleExtension} extension",
                "The file may not be a valid ScalarScope bundle.");
        }
        
        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            
            // Check required files
            foreach (var requiredFile in RequiredFiles)
            {
                if (archive.GetEntry(requiredFile) is null)
                {
                    result.AddError("MissingFile", $"Required file missing: {requiredFile}",
                        $"A valid bundle must contain {requiredFile}");
                }
            }
            
            if (result.HasErrors) return result;
            
            // Validate manifest.json
            var manifestEntry = archive.GetEntry("manifest.json")!;
            BundleManifest? manifest;
            
            using (var stream = manifestEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                manifest = JsonSerializer.Deserialize<BundleManifest>(json, GetJsonOptions());
            }
            
            if (manifest is null)
            {
                result.AddError("InvalidManifest", "Could not parse manifest.json",
                    "The manifest file is malformed or corrupted.");
                return result;
            }
            
            result.Manifest = manifest;
            
            // Validate version
            if (!IsVersionSupported(manifest.BundleVersion))
            {
                if (CompareVersions(manifest.BundleVersion, MaxSupportedVersion) > 0)
                {
                    result.AddError("UnsupportedVersion", 
                        $"Bundle version {manifest.BundleVersion} is newer than supported {MaxSupportedVersion}",
                        "Please update ScalarScope to open this bundle.");
                }
                else
                {
                    result.AddError("UnsupportedVersion",
                        $"Bundle version {manifest.BundleVersion} is older than supported {MinSupportedVersion}",
                        "This bundle was created with an older version and cannot be opened.");
                }
            }
            
            // Validate required sections based on contents manifest
            if (manifest.Contents.IncludesInsights)
            {
                if (archive.GetEntry("insights/insights.json") is null)
                {
                    result.AddWarning("MissingInsights", 
                        "Manifest indicates insights but insights.json not found",
                        "Insights may not be displayed.");
                }
            }
            
            if (manifest.Contents.IncludesAudit)
            {
                if (archive.GetEntry("audit/audit.json") is null)
                {
                    result.AddWarning("MissingAudit",
                        "Manifest indicates audit data but audit.json not found",
                        "Audit information may not be available.");
                }
            }
            
            // Validate deltas.json structure
            var deltasEntry = archive.GetEntry("findings/deltas.json")!;
            using (var stream = deltasEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var deltas = JsonSerializer.Deserialize<List<BundleDelta>>(json, GetJsonOptions());
                
                if (deltas is null)
                {
                    result.AddError("InvalidDeltas", "Could not parse deltas.json",
                        "The deltas file is malformed.");
                }
                else
                {
                    result.DeltaCount = deltas.Count;
                }
            }
            
            // Validate repro.json structure
            var reproEntry = archive.GetEntry("repro/repro.json")!;
            using (var stream = reproEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                var repro = JsonSerializer.Deserialize<BundleRepro>(json, GetJsonOptions());
                
                if (repro is null)
                {
                    result.AddError("InvalidRepro", "Could not parse repro.json",
                        "The reproducibility file is malformed.");
                }
                else
                {
                    result.ReproInfo = repro;
                }
            }
            
            // Run integrity check
            var integrityResult = await BundleIntegrityService.VerifyBundleAsync(bundlePath);
            result.IntegrityVerified = integrityResult.IsValid;
            
            if (!integrityResult.IsValid)
            {
                foreach (var issue in integrityResult.Issues)
                {
                    if (issue.Severity == IntegrityIssueSeverity.Error)
                    {
                        result.AddError("IntegrityFailed", issue.Message,
                            "Bundle files may have been modified after export.");
                    }
                    else
                    {
                        result.AddWarning("IntegrityWarning", issue.Message, "");
                    }
                }
            }
            
            result.BundleHash = integrityResult.ActualBundleHash;
        }
        catch (InvalidDataException)
        {
            result.AddError("InvalidArchive", "File is not a valid ZIP archive",
                "The file may be corrupted or not a valid bundle.");
        }
        catch (Exception ex)
        {
            result.AddError("ValidationFailed", $"Validation failed: {ex.Message}",
                "An unexpected error occurred during validation.");
            ErrorLoggingService.Instance.Log(ex, "BundleValidation");
        }
        
        result.IsValid = !result.HasErrors;
        return result;
    }
    
    /// <summary>
    /// Quick validation - just checks structure without deep validation.
    /// </summary>
    public static bool QuickValidate(string bundlePath)
    {
        if (!File.Exists(bundlePath)) return false;
        
        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            
            // Check for manifest
            if (archive.GetEntry("manifest.json") is null) return false;
            
            // Check for findings
            if (archive.GetEntry("findings/deltas.json") is null) return false;
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Get bundle manifest without full validation.
    /// </summary>
    public static async Task<BundleManifest?> GetManifestAsync(string bundlePath)
    {
        if (!File.Exists(bundlePath)) return null;
        
        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null) return null;
            
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            return JsonSerializer.Deserialize<BundleManifest>(json, GetJsonOptions());
        }
        catch
        {
            return null;
        }
    }
    
    private static bool IsVersionSupported(string version)
    {
        var min = CompareVersions(version, MinSupportedVersion);
        var max = CompareVersions(version, MaxSupportedVersion);
        return min >= 0 && max <= 0;
    }
    
    private static int CompareVersions(string v1, string v2)
    {
        var parts1 = v1.Split('.').Select(int.Parse).ToArray();
        var parts2 = v2.Split('.').Select(int.Parse).ToArray();
        
        for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
        {
            var p1 = i < parts1.Length ? parts1[i] : 0;
            var p2 = i < parts2.Length ? parts2[i] : 0;
            
            if (p1 != p2) return p1.CompareTo(p2);
        }
        
        return 0;
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

/// <summary>
/// Result of bundle validation.
/// </summary>
public class BundleValidationResult
{
    public required string BundlePath { get; init; }
    public DateTime ValidatedAt { get; init; }
    public bool IsValid { get; set; }
    
    public BundleManifest? Manifest { get; set; }
    public BundleRepro? ReproInfo { get; set; }
    public int DeltaCount { get; set; }
    public string? BundleHash { get; set; }
    public bool IntegrityVerified { get; set; }
    
    public List<BundleValidationIssue> Issues { get; } = [];
    
    public void AddError(string code, string message, string guidance)
    {
        Issues.Add(new BundleValidationIssue
        {
            Code = code,
            Message = message,
            Guidance = guidance,
            Severity = BundleValidationSeverity.Error
        });
    }
    
    public void AddWarning(string code, string message, string guidance)
    {
        Issues.Add(new BundleValidationIssue
        {
            Code = code,
            Message = message,
            Guidance = guidance,
            Severity = BundleValidationSeverity.Warning
        });
    }
    
    public bool HasErrors => Issues.Any(i => i.Severity == BundleValidationSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == BundleValidationSeverity.Warning);
    
    /// <summary>
    /// Get error explanation using ErrorExplanationService pattern.
    /// </summary>
    public ErrorExplanation? GetErrorExplanation()
    {
        if (!HasErrors) return null;
        
        var firstError = Issues.First(i => i.Severity == BundleValidationSeverity.Error);
        
        return new ErrorExplanation
        {
            Code = firstError.Code,
            Title = "Bundle Validation Failed",
            Summary = firstError.Message,
            RootCause = $"Bundle validation failed with code: {firstError.Code}",
            TroubleshootingSteps = [firstError.Guidance, "Obtain a valid bundle", "Check bundle source"]
        };
    }
}

/// <summary>
/// A validation issue.
/// </summary>
public record BundleValidationIssue
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string Guidance { get; init; }
    public required BundleValidationSeverity Severity { get; init; }
}

/// <summary>
/// Severity of validation issue.
/// </summary>
public enum BundleValidationSeverity
{
    Warning,
    Error
}
