// Phase 7.1.5: Reproducibility Audit Export Service
// Generates comprehensive export bundles for audit and reproducibility.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.1: Service for exporting reproducibility audit bundles.
/// Creates comprehensive packages with all data needed to reproduce comparisons.
/// </summary>
public sealed class ReproAuditExportService
{
    private static readonly Lazy<ReproAuditExportService> _instance = 
        new(() => new ReproAuditExportService());
    
    public static ReproAuditExportService Instance => _instance.Value;
    
    private readonly string _exportDirectory;
    
    private ReproAuditExportService()
    {
        _exportDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "audit-exports");
        
        Directory.CreateDirectory(_exportDirectory);
    }
    
    /// <summary>
    /// Current audit bundle spec version.
    /// </summary>
    public const string AuditBundleVersion = "1.0.0";
    
    /// <summary>
    /// Create a reproducibility audit bundle from a comparison.
    /// </summary>
    public ReproAuditBundle CreateBundle(
        DeltaComputationResult result,
        string? trajectory1Path,
        string? trajectory2Path,
        ImportPreset? preset = null,
        string? notes = null)
    {
        var bundle = new ReproAuditBundle
        {
            Id = Guid.NewGuid().ToString("N"),
            BundleVersion = AuditBundleVersion,
            CreatedAt = DateTime.UtcNow,
            
            // Fingerprints section
            Fingerprints = new FingerprintSection
            {
                InputFingerprint = result.InputFingerprint ?? "",
                DeltaHash = result.DeltaHash ?? "",
                EnvironmentHash = ComputeEnvironmentHash()
            },
            
            // Determinism section
            Determinism = new DeterminismSection
            {
                IsEnabled = DeterminismService.IsDeterministic,
                Seed = DeterminismService.Seed,
                LastFingerprint = DeterminismService.LastFingerprint
            },
            
            // Versioning section
            Versioning = new VersioningSection
            {
                AppVersion = AppSession.Version,
                DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
                AuditBundleVersion = AuditBundleVersion
            },
            
            // Environment section
            Environment = new EnvironmentSection
            {
                Platform = Environment.OSVersion.ToString(),
                DotNetVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                Is64BitProcess = Environment.Is64BitProcess,
                WorkingSet = Environment.WorkingSet,
                SystemDirectory = Environment.SystemDirectory
            },
            
            // Input section
            Inputs = new InputSection
            {
                Trajectory1Path = trajectory1Path,
                Trajectory2Path = trajectory2Path,
                Trajectory1Exists = !string.IsNullOrEmpty(trajectory1Path) && File.Exists(trajectory1Path),
                Trajectory2Exists = !string.IsNullOrEmpty(trajectory2Path) && File.Exists(trajectory2Path),
                Trajectory1Size = GetFileSize(trajectory1Path),
                Trajectory2Size = GetFileSize(trajectory2Path),
                Trajectory1Hash = ComputeFileHash(trajectory1Path),
                Trajectory2Hash = ComputeFileHash(trajectory2Path)
            },
            
            // Preset section
            Preset = preset is not null ? new PresetSection
            {
                Id = preset.Id,
                Name = preset.Name,
                Fingerprint = preset.GetSettingsFingerprint(),
                NormalizationMethod = preset.Normalization.Method.ToString(),
                AlignmentMode = preset.Alignment.Mode.ToString()
            } : null,
            
            // Results summary
            Results = new ResultsSection
            {
                AlignmentMode = result.Alignment.Mode.ToString(),
                TimestepCount = result.Alignment.CompareIndex.Length,
                DeltaCount = result.Deltas?.Count ?? 0,
                TotalDeltaMagnitude = result.Deltas?.Sum(d => Math.Abs(d.Delta)) ?? 0,
                MeaningfulDeltaCount = result.Deltas?.Count(d => d.IsMeaningful) ?? 0,
                ComparativeSummary = result.ComparativeSummary
            },
            
            // Notes
            Notes = notes
        };
        
        // Compute bundle integrity hash using with expression
        bundle = bundle with { IntegrityHash = ComputeBundleHash(bundle) };
        
        return bundle;
    }
    
    /// <summary>
    /// Export audit bundle to JSON file.
    /// </summary>
    public async Task<string> ExportToJsonAsync(ReproAuditBundle bundle, string? outputPath = null)
    {
        var path = outputPath ?? Path.Combine(
            _exportDirectory, 
            $"audit-{bundle.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        
        var json = JsonSerializer.Serialize(bundle, GetJsonOptions());
        await File.WriteAllTextAsync(path, json);
        
        return path;
    }
    
    /// <summary>
    /// Export audit bundle as compressed archive with input data.
    /// </summary>
    public async Task<string> ExportToArchiveAsync(
        ReproAuditBundle bundle,
        bool includeInputFiles = true,
        string? outputPath = null)
    {
        var archiveName = $"audit-{bundle.Id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        var path = outputPath ?? Path.Combine(_exportDirectory, archiveName);
        
        using var stream = new FileStream(path, FileMode.Create);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        
        // Add manifest
        var manifestEntry = archive.CreateEntry("manifest.json");
        using (var manifestStream = manifestEntry.Open())
        using (var writer = new StreamWriter(manifestStream))
        {
            await writer.WriteAsync(JsonSerializer.Serialize(bundle, GetJsonOptions()));
        }
        
        // Add input files if requested
        if (includeInputFiles)
        {
            if (!string.IsNullOrEmpty(bundle.Inputs.Trajectory1Path) && 
                File.Exists(bundle.Inputs.Trajectory1Path))
            {
                var entry = archive.CreateEntry($"inputs/trajectory1{Path.GetExtension(bundle.Inputs.Trajectory1Path)}");
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(bundle.Inputs.Trajectory1Path);
                await fileStream.CopyToAsync(entryStream);
            }
            
            if (!string.IsNullOrEmpty(bundle.Inputs.Trajectory2Path) && 
                File.Exists(bundle.Inputs.Trajectory2Path))
            {
                var entry = archive.CreateEntry($"inputs/trajectory2{Path.GetExtension(bundle.Inputs.Trajectory2Path)}");
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(bundle.Inputs.Trajectory2Path);
                await fileStream.CopyToAsync(entryStream);
            }
        }
        
        // Add preset if present
        if (bundle.Preset is not null)
        {
            var presetEntry = archive.CreateEntry("preset.json");
            using var presetStream = presetEntry.Open();
            using var writer = new StreamWriter(presetStream);
            await writer.WriteAsync(JsonSerializer.Serialize(bundle.Preset, GetJsonOptions()));
        }
        
        // Add README
        var readmeEntry = archive.CreateEntry("README.md");
        using (var readmeStream = readmeEntry.Open())
        using (var writer = new StreamWriter(readmeStream))
        {
            await writer.WriteAsync(GenerateReadme(bundle));
        }
        
        return path;
    }
    
    /// <summary>
    /// Import audit bundle from JSON.
    /// </summary>
    public async Task<ReproAuditBundle?> ImportFromJsonAsync(string path)
    {
        if (!File.Exists(path)) return null;
        
        var json = await File.ReadAllTextAsync(path);
        var bundle = JsonSerializer.Deserialize<ReproAuditBundle>(json, GetJsonOptions());
        
        // Verify integrity
        if (bundle is not null && !string.IsNullOrEmpty(bundle.IntegrityHash))
        {
            var storedHash = bundle.IntegrityHash;
            bundle = bundle with { IntegrityHash = null };
            var computedHash = ComputeBundleHash(bundle);
            bundle = bundle with { IntegrityHash = storedHash };
            
            if (computedHash != storedHash)
            {
                throw new InvalidDataException($"Bundle integrity check failed. Expected: {storedHash}, Got: {computedHash}");
            }
        }
        
        return bundle;
    }
    
    /// <summary>
    /// Verify audit bundle integrity.
    /// </summary>
    public bool VerifyBundleIntegrity(ReproAuditBundle bundle)
    {
        if (string.IsNullOrEmpty(bundle.IntegrityHash)) return false;
        
        var storedHash = bundle.IntegrityHash;
        var bundleWithoutHash = bundle with { IntegrityHash = null };
        var computedHash = ComputeBundleHash(bundleWithoutHash);
        
        return computedHash == storedHash;
    }
    
    /// <summary>
    /// Get all exported bundles.
    /// </summary>
    public IReadOnlyList<ReproAuditBundle> GetExportedBundles()
    {
        var bundles = new List<ReproAuditBundle>();
        var files = Directory.GetFiles(_exportDirectory, "audit-*.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var bundle = JsonSerializer.Deserialize<ReproAuditBundle>(json, GetJsonOptions());
                if (bundle is not null)
                {
                    bundles.Add(bundle);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return bundles.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly();
    }
    
    private string GenerateReadme(ReproAuditBundle bundle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ScalarScope Audit Bundle");
        sb.AppendLine();
        sb.AppendLine($"**Bundle ID:** `{bundle.Id}`");
        sb.AppendLine($"**Created:** {bundle.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Bundle Version:** {bundle.BundleVersion}");
        sb.AppendLine();
        sb.AppendLine("## Reproducibility Information");
        sb.AppendLine();
        sb.AppendLine("### Fingerprints");
        sb.AppendLine($"- Input Fingerprint: `{bundle.Fingerprints.InputFingerprint}`");
        sb.AppendLine($"- Delta Hash: `{bundle.Fingerprints.DeltaHash}`");
        sb.AppendLine($"- Environment Hash: `{bundle.Fingerprints.EnvironmentHash}`");
        sb.AppendLine();
        sb.AppendLine("### Determinism");
        sb.AppendLine($"- Enabled: {bundle.Determinism.IsEnabled}");
        if (bundle.Determinism.Seed.HasValue)
        {
            sb.AppendLine($"- Seed: {bundle.Determinism.Seed}");
        }
        sb.AppendLine();
        sb.AppendLine("### Versions");
        sb.AppendLine($"- App Version: {bundle.Versioning.AppVersion}");
        sb.AppendLine($"- Delta Spec Version: {bundle.Versioning.DeltaSpecVersion}");
        sb.AppendLine();
        sb.AppendLine("### Environment");
        sb.AppendLine($"- Platform: {bundle.Environment.Platform}");
        sb.AppendLine($"- .NET Version: {bundle.Environment.DotNetVersion}");
        sb.AppendLine($"- 64-bit: {bundle.Environment.Is64BitProcess}");
        sb.AppendLine();
        sb.AppendLine("## Results Summary");
        sb.AppendLine($"- Timesteps: {bundle.Results.TimestepCount}");
        sb.AppendLine($"- Deltas: {bundle.Results.DeltaCount}");
        sb.AppendLine($"- Meaningful: {bundle.Results.MeaningfulDeltaCount}");
        sb.AppendLine($"- Total Magnitude: {bundle.Results.TotalDeltaMagnitude:F6}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(bundle.Notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine(bundle.Notes);
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine("*Generated by ScalarScope Reproducibility Audit*");
        
        return sb.ToString();
    }
    
    private static string ComputeEnvironmentHash()
    {
        var envString = $"{Environment.OSVersion}|{Environment.Version}|{Environment.MachineName}|{Environment.ProcessorCount}";
        return ComputeHash(envString)[..16];
    }
    
    private static string ComputeBundleHash(ReproAuditBundle bundle)
    {
        // Create deterministic string from bundle content
        var content = $"{bundle.Id}|{bundle.CreatedAt:O}|{bundle.Fingerprints.InputFingerprint}|{bundle.Fingerprints.DeltaHash}|{bundle.Determinism.Seed}|{bundle.Versioning.AppVersion}";
        return ComputeHash(content);
    }
    
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
    
    private static string? ComputeFileHash(string? path)
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
    
    private static long? GetFileSize(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return null;
        }
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

/// <summary>
/// Complete reproducibility audit bundle.
/// </summary>
public record ReproAuditBundle
{
    public required string Id { get; init; }
    public required string BundleVersion { get; init; }
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// Integrity hash for tamper detection.
    /// </summary>
    public string? IntegrityHash { get; init; }
    
    /// <summary>
    /// Fingerprint data for verification.
    /// </summary>
    public required FingerprintSection Fingerprints { get; init; }
    
    /// <summary>
    /// Determinism configuration.
    /// </summary>
    public required DeterminismSection Determinism { get; init; }
    
    /// <summary>
    /// Version information.
    /// </summary>
    public required VersioningSection Versioning { get; init; }
    
    /// <summary>
    /// Execution environment.
    /// </summary>
    public required EnvironmentSection Environment { get; init; }
    
    /// <summary>
    /// Input file information.
    /// </summary>
    public required InputSection Inputs { get; init; }
    
    /// <summary>
    /// Preset configuration.
    /// </summary>
    public PresetSection? Preset { get; init; }
    
    /// <summary>
    /// Results summary.
    /// </summary>
    public required ResultsSection Results { get; init; }
    
    /// <summary>
    /// User notes.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Fingerprint data section.
/// </summary>
public record FingerprintSection
{
    public required string InputFingerprint { get; init; }
    public required string DeltaHash { get; init; }
    public required string EnvironmentHash { get; init; }
}

/// <summary>
/// Determinism configuration section.
/// </summary>
public record DeterminismSection
{
    public bool IsEnabled { get; init; }
    public int? Seed { get; init; }
    public string? LastFingerprint { get; init; }
}

/// <summary>
/// Version information section.
/// </summary>
public record VersioningSection
{
    public required string AppVersion { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public required string AuditBundleVersion { get; init; }
}

/// <summary>
/// Environment information section.
/// </summary>
public record EnvironmentSection
{
    public required string Platform { get; init; }
    public required string DotNetVersion { get; init; }
    public required string MachineName { get; init; }
    public int ProcessorCount { get; init; }
    public bool Is64BitProcess { get; init; }
    public long WorkingSet { get; init; }
    public required string SystemDirectory { get; init; }
}

/// <summary>
/// Input file information section.
/// </summary>
public record InputSection
{
    public string? Trajectory1Path { get; init; }
    public string? Trajectory2Path { get; init; }
    public bool Trajectory1Exists { get; init; }
    public bool Trajectory2Exists { get; init; }
    public long? Trajectory1Size { get; init; }
    public long? Trajectory2Size { get; init; }
    public string? Trajectory1Hash { get; init; }
    public string? Trajectory2Hash { get; init; }
}

/// <summary>
/// Preset configuration section.
/// </summary>
public record PresetSection
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Fingerprint { get; init; }
    public required string NormalizationMethod { get; init; }
    public required string AlignmentMode { get; init; }
}

/// <summary>
/// Results summary section.
/// </summary>
public record ResultsSection
{
    public required string AlignmentMode { get; init; }
    public int TimestepCount { get; init; }
    public int DeltaCount { get; init; }
    public double TotalDeltaMagnitude { get; init; }
    public int MeaningfulDeltaCount { get; init; }
    public string? ComparativeSummary { get; init; }
}
