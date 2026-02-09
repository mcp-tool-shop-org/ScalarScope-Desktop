// Phase 7.2.1: Bundle Integrity Service
// Centralized hashing utilities for bundle verification.
// Reused by both ComparisonBundleService and ReproAuditExportService.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.2: Centralized service for bundle integrity operations.
/// Provides consistent hashing across all bundle types.
/// </summary>
public static class BundleIntegrityService
{
    /// <summary>
    /// Compute SHA-256 hash of string content.
    /// </summary>
    public static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
    
    /// <summary>
    /// Compute SHA-256 hash of byte array.
    /// </summary>
    public static string ComputeHash(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }
    
    /// <summary>
    /// Compute SHA-256 hash of file.
    /// </summary>
    public static string? ComputeFileHash(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexStringLower(hash);
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Compute bundle-level hash from file hashes.
    /// Hash of all file hashes sorted alphabetically by path.
    /// </summary>
    public static string ComputeBundleHash(Dictionary<string, string> fileHashes)
    {
        var sortedPaths = fileHashes.Keys.OrderBy(k => k, StringComparer.Ordinal);
        var combined = new StringBuilder();
        
        foreach (var path in sortedPaths)
        {
            combined.Append(path);
            combined.Append(':');
            combined.Append(fileHashes[path]);
            combined.Append('\n');
        }
        
        return ComputeHash(combined.ToString());
    }
    
    /// <summary>
    /// Verify bundle integrity from integrity.json.
    /// </summary>
    public static async Task<BundleIntegrityVerification> VerifyBundleAsync(string bundlePath)
    {
        var verification = new BundleIntegrityVerification
        {
            BundlePath = bundlePath,
            VerifiedAt = DateTime.UtcNow
        };
        
        if (!File.Exists(bundlePath))
        {
            verification.IsValid = false;
            verification.AddError("BundleNotFound", $"Bundle file not found: {bundlePath}");
            return verification;
        }
        
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(bundlePath);
            
            // Read integrity.json
            var integrityEntry = archive.GetEntry("integrity.json");
            if (integrityEntry is null)
            {
                verification.IsValid = false;
                verification.AddError("MissingIntegrity", "Bundle does not contain integrity.json");
                return verification;
            }
            
            BundleIntegrityInfo? integrityInfo;
            using (var stream = integrityEntry.Open())
            using (var reader = new StreamReader(stream))
            {
                var json = await reader.ReadToEndAsync();
                integrityInfo = JsonSerializer.Deserialize<BundleIntegrityInfo>(json, GetJsonOptions());
            }
            
            if (integrityInfo is null)
            {
                verification.IsValid = false;
                verification.AddError("InvalidIntegrity", "Could not parse integrity.json");
                return verification;
            }
            
            verification.ExpectedBundleHash = integrityInfo.BundleHash;
            verification.ExpectedFileCount = integrityInfo.FileHashes.Count;
            
            // Verify each file
            var actualHashes = new Dictionary<string, string>();
            var missingFiles = new List<string>();
            var modifiedFiles = new List<string>();
            
            foreach (var (path, expectedHash) in integrityInfo.FileHashes)
            {
                var entry = archive.GetEntry(path);
                if (entry is null)
                {
                    missingFiles.Add(path);
                    continue;
                }
                
                using var stream = entry.Open();
                using var memStream = new MemoryStream();
                await stream.CopyToAsync(memStream);
                var actualHash = ComputeHash(memStream.ToArray());
                actualHashes[path] = actualHash;
                
                if (actualHash != expectedHash)
                {
                    modifiedFiles.Add(path);
                    verification.AddWarning("HashMismatch", $"File modified: {path}");
                }
            }
            
            verification.ActualFileCount = actualHashes.Count;
            verification.MissingFiles = missingFiles;
            verification.ModifiedFiles = modifiedFiles;
            
            // Compute actual bundle hash
            verification.ActualBundleHash = ComputeBundleHash(actualHashes);
            verification.BundleHashMatch = verification.ActualBundleHash == integrityInfo.BundleHash;
            
            // Set overall validity
            verification.IsValid = missingFiles.Count == 0 && 
                                   modifiedFiles.Count == 0 && 
                                   verification.BundleHashMatch;
            
            if (!verification.IsValid && !verification.HasErrors)
            {
                if (missingFiles.Count > 0)
                {
                    verification.AddError("MissingFiles", $"{missingFiles.Count} file(s) missing from bundle");
                }
                if (modifiedFiles.Count > 0)
                {
                    verification.AddError("ModifiedFiles", $"{modifiedFiles.Count} file(s) have been modified");
                }
                if (!verification.BundleHashMatch)
                {
                    verification.AddError("BundleHashMismatch", "Bundle hash does not match expected value");
                }
            }
        }
        catch (Exception ex)
        {
            verification.IsValid = false;
            verification.AddError("VerificationFailed", $"Verification failed: {ex.Message}");
            ErrorLoggingService.Instance.Log(ex, "BundleIntegrityVerification");
        }
        
        return verification;
    }
    
    /// <summary>
    /// Quick check if bundle appears valid (manifest exists).
    /// </summary>
    public static bool QuickValidate(string bundlePath)
    {
        if (!File.Exists(bundlePath)) return false;
        
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(bundlePath);
            return archive.GetEntry("manifest.json") is not null;
        }
        catch
        {
            return false;
        }
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}

/// <summary>
/// Result of bundle integrity verification.
/// </summary>
public class BundleIntegrityVerification
{
    public required string BundlePath { get; init; }
    public DateTime VerifiedAt { get; init; }
    public bool IsValid { get; set; }
    
    public string? ExpectedBundleHash { get; set; }
    public string? ActualBundleHash { get; set; }
    public bool BundleHashMatch { get; set; }
    
    public int ExpectedFileCount { get; set; }
    public int ActualFileCount { get; set; }
    
    public List<string> MissingFiles { get; set; } = [];
    public List<string> ModifiedFiles { get; set; } = [];
    
    public List<IntegrityIssue> Issues { get; } = [];
    
    public void AddError(string code, string message)
    {
        Issues.Add(new IntegrityIssue
        {
            Code = code,
            Message = message,
            Severity = IntegrityIssueSeverity.Error
        });
    }
    
    public void AddWarning(string code, string message)
    {
        Issues.Add(new IntegrityIssue
        {
            Code = code,
            Message = message,
            Severity = IntegrityIssueSeverity.Warning
        });
    }
    
    public bool HasErrors => Issues.Any(i => i.Severity == IntegrityIssueSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == IntegrityIssueSeverity.Warning);
    
    /// <summary>
    /// Get a summary of the verification result.
    /// </summary>
    public string GetSummary()
    {
        if (IsValid)
        {
            return $"✓ Bundle integrity verified ({ActualFileCount} files)";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("✗ Bundle integrity check failed:");
        foreach (var issue in Issues.Where(i => i.Severity == IntegrityIssueSeverity.Error))
        {
            sb.AppendLine($"  - {issue.Message}");
        }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// An integrity verification issue.
/// </summary>
public record IntegrityIssue
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required IntegrityIssueSeverity Severity { get; init; }
}

/// <summary>
/// Severity of integrity issue.
/// </summary>
public enum IntegrityIssueSeverity
{
    Warning,
    Error
}
