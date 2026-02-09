// BundleIntegrityService v1.0.0
// Implements file integrity verification and bundle hash validation.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScalarScope.Services.Bundles;

/// <summary>
/// Service for computing and verifying bundle integrity.
/// Implements the v1.0.0 integrity model with per-file hashing and bundle hash verification.
/// </summary>
public interface IBundleIntegrityService
{
    /// <summary>
    /// Create manifest entries (path, sha256, bytes, contentType) for all files.
    /// </summary>
    IReadOnlyList<FileIntegrityEntry> ComputeFileEntries(IReadOnlyDictionary<string, byte[]> files);

    /// <summary>
    /// Compute bundle hash from manifestCore canonical bytes and file entries.
    /// </summary>
    string ComputeBundleHash(byte[] manifestCoreCanonicalBytes, IReadOnlyList<FileIntegrityEntry> sortedEntries);

    /// <summary>
    /// Verify all file hashes and bundle hash against manifest.
    /// </summary>
    BundleIntegrityReport Verify(IReadOnlyDictionary<string, byte[]> files, ComparisonBundleManifest manifest);
    
    /// <summary>
    /// Verify integrity asynchronously from a stream-based source.
    /// </summary>
    Task<BundleIntegrityReport> VerifyAsync(
        Func<string, Task<byte[]?>> fileReader,
        ComparisonBundleManifest manifest,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of bundle integrity verification.
/// </summary>
public sealed record BundleIntegrityReport
{
    /// <summary>Whether the bundle passes all integrity checks.</summary>
    public required bool IsValid { get; init; }
    
    /// <summary>List of integrity issues found.</summary>
    public required IReadOnlyList<BundleIntegrityIssue> Issues { get; init; }
    
    /// <summary>Summary message suitable for UI display.</summary>
    public string Summary => IsValid 
        ? "Bundle integrity verified successfully." 
        : $"Bundle integrity check failed. {Issues.Count} issue(s) found.";
    
    /// <summary>True if verification was lenient (allowed extra files).</summary>
    public bool IsLenient { get; init; }
    
    /// <summary>Create a valid report.</summary>
    public static BundleIntegrityReport Valid() => new()
    {
        IsValid = true,
        Issues = Array.Empty<BundleIntegrityIssue>()
    };
    
    /// <summary>Create an invalid report from issues.</summary>
    public static BundleIntegrityReport Invalid(IReadOnlyList<BundleIntegrityIssue> issues) => new()
    {
        IsValid = false,
        Issues = issues
    };
}

/// <summary>
/// Single integrity issue found during verification.
/// </summary>
public sealed record BundleIntegrityIssue
{
    /// <summary>Machine-readable issue code.</summary>
    public required string Code { get; init; }
    
    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }
    
    /// <summary>Path of the affected file (if applicable).</summary>
    public string? Path { get; init; }
    
    /// <summary>Expected value (hash, etc).</summary>
    public string? Expected { get; init; }
    
    /// <summary>Actual value found.</summary>
    public string? Actual { get; init; }
    
    /// <summary>Severity level.</summary>
    public IntegrityIssueSeverity Severity { get; init; } = IntegrityIssueSeverity.Error;
}

/// <summary>
/// Severity level for integrity issues.
/// </summary>
public enum IntegrityIssueSeverity
{
    /// <summary>Fatal error - bundle is invalid.</summary>
    Error,
    
    /// <summary>Warning - bundle may be usable but has issues.</summary>
    Warning,
    
    /// <summary>Informational - not a problem.</summary>
    Info
}

/// <summary>
/// Well-known integrity issue codes.
/// </summary>
public static class IntegrityIssueCodes
{
    /// <summary>A required file is missing from the bundle.</summary>
    public const string MissingFile = "BUNDLE_MISSING_FILE";
    
    /// <summary>Bundle hash does not match computed value.</summary>
    public const string BundleHashMismatch = "BUNDLE_HASH_MISMATCH";
    
    /// <summary>Individual file hash does not match.</summary>
    public const string FileHashMismatch = "FILE_HASH_MISMATCH";
    
    /// <summary>Manifest schema is invalid.</summary>
    public const string ManifestSchemaInvalid = "MANIFEST_SCHEMA_INVALID";
    
    /// <summary>An undeclared file is present in the bundle.</summary>
    public const string UndeclaredFilePresent = "UNDECLARED_FILE_PRESENT";
    
    /// <summary>A required entry is missing from manifest.</summary>
    public const string ManifestMissingEntry = "MANIFEST_MISSING_ENTRY";
    
    /// <summary>Manifest version is unsupported.</summary>
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
    
    /// <summary>Integrity block is missing from manifest.</summary>
    public const string IntegrityBlockMissing = "INTEGRITY_BLOCK_MISSING";
}

/// <summary>
/// Default implementation of IBundleIntegrityService.
/// </summary>
public sealed class BundleIntegrityService : IBundleIntegrityService
{
    /// <summary>Singleton instance.</summary>
    public static readonly BundleIntegrityService Instance = new();
    
    /// <summary>Policy for handling undeclared files.</summary>
    public bool StrictMode { get; set; } = true;
    
    private static readonly JsonSerializerOptions s_canonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    
    /// <inheritdoc />
    public IReadOnlyList<FileIntegrityEntry> ComputeFileEntries(IReadOnlyDictionary<string, byte[]> files)
    {
        var entries = new List<FileIntegrityEntry>();
        
        foreach (var (path, content) in files)
        {
            // Skip manifest.json - it's not included in its own integrity list
            if (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;
            
            // Normalize path (forward slashes, no leading ./)
            var normalizedPath = NormalizePath(path);
            
            // For JSON files, canonicalize before hashing for determinism
            var bytesToHash = IsJsonFile(normalizedPath) 
                ? CanonicalizeJsonBytes(content) 
                : content;
            
            var sha256 = ComputeSha256Hex(bytesToHash);
            var contentType = GuessContentType(normalizedPath);
            
            entries.Add(new FileIntegrityEntry
            {
                Path = normalizedPath,
                Sha256 = sha256,
                Bytes = content.Length,
                ContentType = contentType
            });
        }
        
        // Sort lexicographically by path for deterministic ordering
        return entries
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ToList();
    }
    
    /// <inheritdoc />
    public string ComputeBundleHash(byte[] manifestCoreCanonicalBytes, IReadOnlyList<FileIntegrityEntry> sortedEntries)
    {
        // 1. Hash the manifestCore bytes
        var manifestCoreHash = ComputeSha256Hex(manifestCoreCanonicalBytes);
        
        // 2. Concatenate all file hashes in order (entries must already be sorted)
        var fileHashesConcat = string.Concat(sortedEntries.Select(e => e.Sha256));
        
        // 3. Final hash: SHA256(manifestCoreHash + fileHashesConcat)
        var accumulator = manifestCoreHash + fileHashesConcat;
        return ComputeSha256Hex(Encoding.UTF8.GetBytes(accumulator));
    }
    
    /// <inheritdoc />
    public BundleIntegrityReport Verify(IReadOnlyDictionary<string, byte[]> files, ComparisonBundleManifest manifest)
    {
        var issues = new List<BundleIntegrityIssue>();
        
        // 1. Validate manifest has integrity block
        if (manifest.Integrity is null)
        {
            issues.Add(new BundleIntegrityIssue
            {
                Code = IntegrityIssueCodes.IntegrityBlockMissing,
                Message = "Manifest is missing integrity block.",
                Severity = IntegrityIssueSeverity.Error
            });
            return BundleIntegrityReport.Invalid(issues);
        }
        
        var integrity = manifest.Integrity;
        
        // 2. Validate manifest version
        if (manifest.BundleVersion != "1.0.0")
        {
            issues.Add(new BundleIntegrityIssue
            {
                Code = IntegrityIssueCodes.UnsupportedVersion,
                Message = $"Unsupported bundle version: {manifest.BundleVersion}",
                Expected = "1.0.0",
                Actual = manifest.BundleVersion,
                Severity = IntegrityIssueSeverity.Error
            });
        }
        
        // 3. Verify required files exist
        var requiredFiles = manifest.Contents?.Required ?? Array.Empty<string>();
        foreach (var required in requiredFiles)
        {
            var normalizedRequired = NormalizePath(required);
            if (!files.Keys.Any(k => NormalizePath(k).Equals(normalizedRequired, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new BundleIntegrityIssue
                {
                    Code = IntegrityIssueCodes.MissingFile,
                    Message = $"Required file missing: {required}",
                    Path = required,
                    Severity = IntegrityIssueSeverity.Error
                });
            }
        }
        
        // 4. Verify per-file hashes
        var declaredFiles = integrity.Files?.ToDictionary(
            f => NormalizePath(f.Path), 
            f => f,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, FileIntegrityEntry>();
        
        var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var (path, content) in files)
        {
            // Skip manifest.json
            if (path.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
                continue;
            
            var normalizedPath = NormalizePath(path);
            processedFiles.Add(normalizedPath);
            
            if (!declaredFiles.TryGetValue(normalizedPath, out var expected))
            {
                // Undeclared file
                if (StrictMode)
                {
                    issues.Add(new BundleIntegrityIssue
                    {
                        Code = IntegrityIssueCodes.UndeclaredFilePresent,
                        Message = $"Undeclared file present: {path}",
                        Path = path,
                        Severity = IntegrityIssueSeverity.Error
                    });
                }
                else
                {
                    issues.Add(new BundleIntegrityIssue
                    {
                        Code = IntegrityIssueCodes.UndeclaredFilePresent,
                        Message = $"Undeclared file present: {path}",
                        Path = path,
                        Severity = IntegrityIssueSeverity.Warning
                    });
                }
                continue;
            }
            
            // Compute hash (canonicalize JSON first)
            var bytesToHash = IsJsonFile(normalizedPath) 
                ? CanonicalizeJsonBytes(content) 
                : content;
            var actualHash = ComputeSha256Hex(bytesToHash);
            
            if (!actualHash.Equals(expected.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new BundleIntegrityIssue
                {
                    Code = IntegrityIssueCodes.FileHashMismatch,
                    Message = $"Hash mismatch for file: {path}",
                    Path = path,
                    Expected = expected.Sha256,
                    Actual = actualHash,
                    Severity = IntegrityIssueSeverity.Error
                });
            }
        }
        
        // 5. Check for missing declared files
        foreach (var declared in declaredFiles.Keys)
        {
            if (!processedFiles.Contains(declared))
            {
                issues.Add(new BundleIntegrityIssue
                {
                    Code = IntegrityIssueCodes.MissingFile,
                    Message = $"Declared file missing: {declared}",
                    Path = declared,
                    Severity = IntegrityIssueSeverity.Error
                });
            }
        }
        
        // 6. Verify bundle hash
        if (!string.IsNullOrEmpty(integrity.BundleHash))
        {
            // Reconstruct manifestCore (manifest without integrity block)
            // We serialize, parse, remove integrity key, then re-serialize for canonical form
            var manifestCoreBytes = GetManifestCoreBytes(manifest);
            
            // Compute file entries for present files
            var fileEntries = ComputeFileEntries(files);
            var computedBundleHash = ComputeBundleHash(manifestCoreBytes, fileEntries);
            
            if (!computedBundleHash.Equals(integrity.BundleHash, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new BundleIntegrityIssue
                {
                    Code = IntegrityIssueCodes.BundleHashMismatch,
                    Message = "Bundle hash does not match computed value.",
                    Expected = integrity.BundleHash,
                    Actual = computedBundleHash,
                    Severity = IntegrityIssueSeverity.Error
                });
            }
        }
        
        // Determine overall validity
        var hasErrors = issues.Any(i => i.Severity == IntegrityIssueSeverity.Error);
        return new BundleIntegrityReport
        {
            IsValid = !hasErrors,
            Issues = issues,
            IsLenient = !StrictMode
        };
    }
    
    /// <inheritdoc />
    public async Task<BundleIntegrityReport> VerifyAsync(
        Func<string, Task<byte[]?>> fileReader,
        ComparisonBundleManifest manifest,
        CancellationToken cancellationToken = default)
    {
        // Load all declared files
        var files = new Dictionary<string, byte[]>();
        
        // Include required files from manifest
        var pathsToLoad = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        if (manifest.Contents?.Required != null)
        {
            foreach (var required in manifest.Contents.Required)
                pathsToLoad.Add(required);
        }
        
        if (manifest.Integrity?.Files != null)
        {
            foreach (var entry in manifest.Integrity.Files)
                pathsToLoad.Add(entry.Path);
        }
        
        // Always include manifest.json
        pathsToLoad.Add("manifest.json");
        
        foreach (var path in pathsToLoad)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var content = await fileReader(path);
            if (content != null)
            {
                files[path] = content;
            }
        }
        
        return Verify(files, manifest);
    }
    
    /// <summary>
    /// Compute SHA-256 hash of bytes, return lowercase hex string.
    /// </summary>
    private static string ComputeSha256Hex(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>
    /// Extract manifestCore bytes (manifest without integrity block) in canonical form.
    /// </summary>
    private static byte[] GetManifestCoreBytes(ComparisonBundleManifest manifest)
    {
        // Serialize manifest to JSON
        var manifestJson = JsonSerializer.SerializeToUtf8Bytes(manifest, s_canonicalOptions);
        
        // Parse to JsonDocument and rebuild without "integrity" key
        using var doc = JsonDocument.Parse(manifestJson);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
        
        writer.WriteStartObject();
        foreach (var prop in doc.RootElement.EnumerateObject()
            .Where(p => !p.Name.Equals("integrity", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.Ordinal))
        {
            writer.WritePropertyName(prop.Name);
            WriteCanonical(prop.Value, writer);
        }
        writer.WriteEndObject();
        writer.Flush();
        
        return stream.ToArray();
    }
    
    /// <summary>
    /// Normalize path to use forward slashes and no leading ./
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path
            .Replace('\\', '/')
            .TrimStart('.', '/');
    }
    
    /// <summary>
    /// Check if path is a JSON file.
    /// </summary>
    private static bool IsJsonFile(string path)
    {
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Canonicalize JSON bytes for deterministic hashing.
    /// Parses JSON, sorts keys, re-serializes without indentation.
    /// </summary>
    private static byte[] CanonicalizeJsonBytes(byte[] jsonBytes)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonBytes);
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = false
            });
            
            WriteCanonical(doc.RootElement, writer);
            writer.Flush();
            return stream.ToArray();
        }
        catch
        {
            // If JSON parsing fails, return original bytes
            return jsonBytes;
        }
    }
    
    /// <summary>
    /// Write JSON element with sorted keys (canonical form).
    /// </summary>
    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                // Sort properties by name (ordinal)
                var properties = element.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .ToList();
                foreach (var prop in properties)
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonical(prop.Value, writer);
                }
                writer.WriteEndObject();
                break;
                
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }
                writer.WriteEndArray();
                break;
                
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
                
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                    writer.WriteNumberValue(longValue);
                else
                    writer.WriteNumberValue(element.GetDouble());
                break;
                
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
                
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
                
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
        }
    }
    
    /// <summary>
    /// Guess content type from file extension.
    /// </summary>
    private static string GuessContentType(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".json" => "application/json",
            ".md" => "text/markdown",
            ".png" => "image/png",
            ".svg" => "image/svg+xml",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".html" => "text/html",
            _ => "application/octet-stream"
        };
    }
}
