// Phase 7.1.3: Comparison Replay Service
// Re-run comparisons using stored fingerprints and determinism seeds.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.1: Service for replaying comparisons with full reproducibility.
/// Uses stored fingerprints and determinism seeds to recreate exact results.
/// </summary>
public sealed class ComparisonReplayService
{
    private static readonly Lazy<ComparisonReplayService> _instance = 
        new(() => new ComparisonReplayService());
    
    public static ComparisonReplayService Instance => _instance.Value;
    
    private readonly string _replayDirectory;
    
    private ComparisonReplayService()
    {
        _replayDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "replays");
        
        Directory.CreateDirectory(_replayDirectory);
    }
    
    /// <summary>
    /// Current replay spec version.
    /// </summary>
    public const string ReplaySpecVersion = "1.0.0";
    
    /// <summary>
    /// Delta specification version for tracking computation changes.
    /// </summary>
    public const string DeltaSpecVersion = "3.2.0";
    
    /// <summary>
    /// Create a replay specification from a comparison result.
    /// </summary>
    public ComparisonReplaySpec CreateReplaySpec(
        DeltaComputationResult result,
        string? trajectory1Path,
        string? trajectory2Path,
        ImportPreset? preset = null)
    {
        // Compute total delta magnitude for verification
        var totalDelta = result.Deltas?.Sum(d => Math.Abs(d.Delta)) ?? 0;
        
        return new ComparisonReplaySpec
        {
            Id = Guid.NewGuid().ToString("N"),
            SpecVersion = ReplaySpecVersion,
            CreatedAt = DateTime.UtcNow,
            
            // Input references
            Trajectory1Path = trajectory1Path,
            Trajectory2Path = trajectory2Path,
            
            // Determinism data from result
            InputFingerprint = result.InputFingerprint ?? "",
            DeltaHash = result.DeltaHash ?? "",
            DeterminismSeed = DeterminismService.Seed ?? 0,
            DeltaSpecVersion = DeltaSpecVersion,
            
            // Preset settings
            PresetId = preset?.Id,
            PresetFingerprint = preset?.GetSettingsFingerprint(),
            
            // Result summary for verification
            TimestepCount = result.Alignment.CompareIndex.Length,
            ScalarCount = result.Deltas?.Count ?? 0,
            TotalDelta = totalDelta,
            
            // Environment info
            Environment = new ReplayEnvironmentInfo
            {
                AppVersion = AppSession.Version,
                Platform = Environment.OSVersion.ToString(),
                DotNetVersion = Environment.Version.ToString(),
                MachineName = Environment.MachineName
            }
        };
    }
    
    /// <summary>
    /// Save a replay spec for later use.
    /// </summary>
    public async Task<string> SaveReplaySpecAsync(ComparisonReplaySpec spec)
    {
        var path = Path.Combine(_replayDirectory, $"{spec.Id}.replay.json");
        var json = JsonSerializer.Serialize(spec, GetJsonOptions());
        await File.WriteAllTextAsync(path, json);
        return path;
    }
    
    /// <summary>
    /// Load a replay spec from file.
    /// </summary>
    public async Task<ComparisonReplaySpec?> LoadReplaySpecAsync(string path)
    {
        if (!File.Exists(path)) return null;
        
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<ComparisonReplaySpec>(json, GetJsonOptions());
    }
    
    /// <summary>
    /// Get all available replay specs.
    /// </summary>
    public IReadOnlyList<ComparisonReplaySpec> GetAvailableReplays()
    {
        var replays = new List<ComparisonReplaySpec>();
        var files = Directory.GetFiles(_replayDirectory, "*.replay.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var spec = JsonSerializer.Deserialize<ComparisonReplaySpec>(json, GetJsonOptions());
                if (spec is not null)
                {
                    replays.Add(spec);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return replays.OrderByDescending(r => r.CreatedAt).ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Verify if a replay can be performed.
    /// </summary>
    public ReplayVerification VerifyReplayability(ComparisonReplaySpec spec)
    {
        var verification = new ReplayVerification
        {
            SpecId = spec.Id,
            CheckedAt = DateTime.UtcNow
        };
        
        // Check input files exist
        if (!string.IsNullOrEmpty(spec.Trajectory1Path))
        {
            if (File.Exists(spec.Trajectory1Path))
            {
                verification.Trajectory1Available = true;
            }
            else
            {
                verification.AddIssue(ReplayIssueType.MissingInput,
                    $"Trajectory 1 not found: {spec.Trajectory1Path}");
            }
        }
        
        if (!string.IsNullOrEmpty(spec.Trajectory2Path))
        {
            if (File.Exists(spec.Trajectory2Path))
            {
                verification.Trajectory2Available = true;
            }
            else
            {
                verification.AddIssue(ReplayIssueType.MissingInput,
                    $"Trajectory 2 not found: {spec.Trajectory2Path}");
            }
        }
        
        // Check spec version compatibility
        if (spec.SpecVersion != ReplaySpecVersion)
        {
            var specParts = spec.SpecVersion.Split('.');
            var currentParts = ReplaySpecVersion.Split('.');
            
            if (specParts[0] != currentParts[0])
            {
                verification.AddIssue(ReplayIssueType.VersionMismatch,
                    $"Replay spec version {spec.SpecVersion} incompatible with current {ReplaySpecVersion}");
            }
            else
            {
                verification.AddWarning(ReplayIssueType.VersionMismatch,
                    $"Replay spec version {spec.SpecVersion} differs from current {ReplaySpecVersion}");
            }
        }
        
        // Check delta spec version
        if (spec.DeltaSpecVersion != DeltaSpecVersion)
        {
            verification.AddWarning(ReplayIssueType.DeltaSpecChanged,
                $"Delta spec version changed: {spec.DeltaSpecVersion} → {DeltaSpecVersion}");
        }
        
        // Check preset availability
        if (!string.IsNullOrEmpty(spec.PresetId))
        {
            var preset = ImportPresetService.Instance.Get(spec.PresetId);
            if (preset is null)
            {
                verification.AddWarning(ReplayIssueType.PresetMissing,
                    $"Original preset '{spec.PresetId}' not found, will use defaults");
            }
            else if (spec.PresetFingerprint != preset.GetSettingsFingerprint())
            {
                verification.AddWarning(ReplayIssueType.PresetModified,
                    $"Preset '{spec.PresetId}' has been modified since original comparison");
            }
            else
            {
                verification.PresetAvailable = true;
            }
        }
        
        verification.CanReplay = verification.Trajectory1Available && 
                                 verification.Trajectory2Available &&
                                 !verification.Issues.Any(i => i.Severity == ReplayIssueSeverity.Error);
        
        return verification;
    }
    
    /// <summary>
    /// Replay a comparison and verify results match.
    /// </summary>
    public async Task<ReplayResult> ReplayComparisonAsync(
        ComparisonReplaySpec spec,
        Func<string?, string?, ImportPreset?, Task<DeltaComputationResult>> compareFunc)
    {
        var result = new ReplayResult
        {
            OriginalSpec = spec,
            ReplayedAt = DateTime.UtcNow
        };
        
        // Verify replayability first
        var verification = VerifyReplayability(spec);
        result.Verification = verification;
        
        if (!verification.CanReplay)
        {
            result.Success = false;
            result.FailureReason = "Replay verification failed: " + 
                string.Join("; ", verification.Issues.Where(i => i.Severity == ReplayIssueSeverity.Error).Select(i => i.Message));
            return result;
        }
        
        try
        {
            // Set determinism seed for reproduction
            DeterminismService.EnableDeterminism(spec.DeterminismSeed);
            
            // Get preset if available
            ImportPreset? preset = null;
            if (!string.IsNullOrEmpty(spec.PresetId))
            {
                preset = ImportPresetService.Instance.Get(spec.PresetId);
            }
            
            // Perform the comparison
            var replayedResult = await compareFunc(
                spec.Trajectory1Path,
                spec.Trajectory2Path,
                preset);
            
            result.ReplayedResult = replayedResult;
            
            // Compute total delta for comparison
            var replayedTotalDelta = replayedResult.Deltas?.Sum(d => Math.Abs(d.Delta)) ?? 0;
            
            // Verify reproducibility
            result.FingerprintMatch = replayedResult.InputFingerprint == spec.InputFingerprint;
            result.DeltaHashMatch = replayedResult.DeltaHash == spec.DeltaHash;
            result.TimestepCountMatch = replayedResult.Alignment.CompareIndex.Length == spec.TimestepCount;
            result.TotalDeltaMatch = Math.Abs(replayedTotalDelta - (spec.TotalDelta ?? 0)) < 1e-10;
            
            result.Success = result.FingerprintMatch && result.DeltaHashMatch;
            
            if (!result.Success)
            {
                var mismatches = new List<string>();
                if (!result.FingerprintMatch) mismatches.Add("input fingerprint");
                if (!result.DeltaHashMatch) mismatches.Add("delta hash");
                if (!result.TimestepCountMatch) mismatches.Add("timestep count");
                if (!result.TotalDeltaMatch) mismatches.Add("total delta");
                
                result.FailureReason = $"Reproduction mismatch in: {string.Join(", ", mismatches)}";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.FailureReason = $"Replay failed: {ex.Message}";
            result.Exception = ex;
            
            ErrorLoggingService.Instance.Log(ex, "ComparisonReplay");
        }
        
        return result;
    }
    
    /// <summary>
    /// Delete a replay spec.
    /// </summary>
    public bool DeleteReplaySpec(string specId)
    {
        var path = Path.Combine(_replayDirectory, $"{specId}.replay.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

/// <summary>
/// Complete specification for replaying a comparison.
/// </summary>
public record ComparisonReplaySpec
{
    public required string Id { get; init; }
    public required string SpecVersion { get; init; }
    public DateTime CreatedAt { get; init; }
    
    // Input references
    public string? Trajectory1Path { get; init; }
    public string? Trajectory2Path { get; init; }
    
    // Determinism info
    public required string InputFingerprint { get; init; }
    public required string DeltaHash { get; init; }
    public int DeterminismSeed { get; init; }
    public required string DeltaSpecVersion { get; init; }
    
    // Preset info
    public string? PresetId { get; init; }
    public string? PresetFingerprint { get; init; }
    
    // Result summary
    public int TimestepCount { get; init; }
    public int ScalarCount { get; init; }
    public double? TotalDelta { get; init; }
    
    // Environment
    public ReplayEnvironmentInfo? Environment { get; init; }
    
    /// <summary>
    /// Get a summary string for display.
    /// </summary>
    public string GetSummary()
    {
        var t1 = Path.GetFileName(Trajectory1Path ?? "Unknown");
        var t2 = Path.GetFileName(Trajectory2Path ?? "Unknown");
        return $"{t1} vs {t2} ({TimestepCount} steps, {CreatedAt:yyyy-MM-dd HH:mm})";
    }
    
    /// <summary>
    /// Get the display label for reproduction UI.
    /// </summary>
    public string GetReproducedFromLabel()
    {
        return $"Reproduced from comparison on {CreatedAt:MMMM d, yyyy} at {CreatedAt:h:mm tt}";
    }
}

/// <summary>
/// Environment info captured during comparison.
/// </summary>
public record ReplayEnvironmentInfo
{
    public required string AppVersion { get; init; }
    public required string Platform { get; init; }
    public required string DotNetVersion { get; init; }
    public required string MachineName { get; init; }
}

/// <summary>
/// Verification results for replay capability.
/// </summary>
public class ReplayVerification
{
    public required string SpecId { get; init; }
    public DateTime CheckedAt { get; init; }
    public bool CanReplay { get; set; }
    public bool Trajectory1Available { get; set; }
    public bool Trajectory2Available { get; set; }
    public bool PresetAvailable { get; set; }
    
    public List<ReplayIssue> Issues { get; } = new();
    
    public void AddIssue(ReplayIssueType type, string message)
    {
        Issues.Add(new ReplayIssue
        {
            Type = type,
            Message = message,
            Severity = ReplayIssueSeverity.Error
        });
    }
    
    public void AddWarning(ReplayIssueType type, string message)
    {
        Issues.Add(new ReplayIssue
        {
            Type = type,
            Message = message,
            Severity = ReplayIssueSeverity.Warning
        });
    }
    
    public bool HasWarnings => Issues.Any(i => i.Severity == ReplayIssueSeverity.Warning);
    public bool HasErrors => Issues.Any(i => i.Severity == ReplayIssueSeverity.Error);
}

/// <summary>
/// An issue found during replay verification.
/// </summary>
public record ReplayIssue
{
    public required ReplayIssueType Type { get; init; }
    public required string Message { get; init; }
    public required ReplayIssueSeverity Severity { get; init; }
}

/// <summary>
/// Types of replay issues.
/// </summary>
public enum ReplayIssueType
{
    MissingInput,
    VersionMismatch,
    DeltaSpecChanged,
    PresetMissing,
    PresetModified,
    EnvironmentChanged
}

/// <summary>
/// Severity of a replay issue.
/// </summary>
public enum ReplayIssueSeverity
{
    Warning,
    Error
}

/// <summary>
/// Result of a replay attempt.
/// </summary>
public record ReplayResult
{
    public required ComparisonReplaySpec OriginalSpec { get; init; }
    public DateTime ReplayedAt { get; init; }
    public bool Success { get; set; }
    public string? FailureReason { get; set; }
    public Exception? Exception { get; set; }
    
    public ReplayVerification? Verification { get; set; }
    public DeltaComputationResult? ReplayedResult { get; set; }
    
    // Match results
    public bool FingerprintMatch { get; set; }
    public bool DeltaHashMatch { get; set; }
    public bool TimestepCountMatch { get; set; }
    public bool TotalDeltaMatch { get; set; }
    
    /// <summary>
    /// Get a summary of match results.
    /// </summary>
    public string GetMatchSummary()
    {
        if (Success)
        {
            return "✓ Exact reproduction achieved";
        }
        
        var mismatches = new List<string>();
        if (!FingerprintMatch) mismatches.Add("Input fingerprint differs");
        if (!DeltaHashMatch) mismatches.Add("Delta hash differs");
        if (!TimestepCountMatch) mismatches.Add("Timestep count differs");
        if (!TotalDeltaMatch) mismatches.Add("Total delta differs");
        
        return string.Join("\n", mismatches);
    }
}
