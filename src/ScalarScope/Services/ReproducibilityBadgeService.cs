// Phase 7.1.4: Reproducibility Badge Service
// Provides visual labels for comparison reproducibility status.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.1: Service for tracking and displaying reproducibility status.
/// Provides badges indicating whether comparisons can be exactly reproduced.
/// </summary>
public sealed class ReproducibilityBadgeService
{
    private static readonly Lazy<ReproducibilityBadgeService> _instance = 
        new(() => new ReproducibilityBadgeService());
    
    public static ReproducibilityBadgeService Instance => _instance.Value;
    
    private ReproducibilityBadgeService() { }
    
    /// <summary>
    /// Compute reproducibility status for a comparison result.
    /// </summary>
    public ReproducibilityStatus ComputeStatus(DeltaComputationResult result)
    {
        // Check if determinism is enabled
        if (!DeterminismService.IsDeterministic)
        {
            return new ReproducibilityStatus
            {
                Level = ReproducibilityLevel.NonDeterministic,
                Label = "Non-deterministic",
                Description = "Comparison ran without determinism enabled. Results may vary between runs.",
                BadgeColor = "#F59E0B", // Amber
                IconGlyph = "⚡",
                Confidence = 0.0,
                Factors = [
                    new ReproducibilityFactor
                    {
                        Name = "Determinism",
                        Status = FactorStatus.Missing,
                        Description = "Deterministic mode was not enabled"
                    }
                ]
            };
        }
        
        var factors = new List<ReproducibilityFactor>();
        var confidence = 1.0;
        
        // Check input fingerprint
        if (string.IsNullOrEmpty(result.InputFingerprint))
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Input Fingerprint",
                Status = FactorStatus.Missing,
                Description = "Input data was not fingerprinted"
            });
            confidence -= 0.25;
        }
        else
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Input Fingerprint",
                Status = FactorStatus.Present,
                Description = $"Fingerprint: {result.InputFingerprint[..Math.Min(16, result.InputFingerprint.Length)]}..."
            });
        }
        
        // Check delta hash
        if (string.IsNullOrEmpty(result.DeltaHash))
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Delta Hash",
                Status = FactorStatus.Missing,
                Description = "Delta outputs were not hashed"
            });
            confidence -= 0.25;
        }
        else
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Delta Hash",
                Status = FactorStatus.Present,
                Description = $"Hash: {result.DeltaHash[..Math.Min(16, result.DeltaHash.Length)]}..."
            });
        }
        
        // Check reproducibility metadata
        if (result.Reproducibility is not null)
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Metadata",
                Status = FactorStatus.Present,
                Description = $"Seed: {result.Reproducibility.Seed}, Version: {result.Reproducibility.Version}"
            });
        }
        else
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Metadata",
                Status = FactorStatus.Missing,
                Description = "Reproducibility metadata not captured"
            });
            confidence -= 0.25;
        }
        
        // Check determinism seed
        if (DeterminismService.Seed.HasValue)
        {
            factors.Add(new ReproducibilityFactor
            {
                Name = "Determinism Seed",
                Status = FactorStatus.Present,
                Description = $"Seed: {DeterminismService.Seed.Value}"
            });
        }
        
        // Determine level based on factors
        var missingCount = factors.Count(f => f.Status == FactorStatus.Missing);
        var level = missingCount switch
        {
            0 => ReproducibilityLevel.Reproducible,
            1 => ReproducibilityLevel.Modified,
            _ => ReproducibilityLevel.Modified
        };
        
        return new ReproducibilityStatus
        {
            Level = level,
            Label = level switch
            {
                ReproducibilityLevel.Reproducible => "Reproducible",
                ReproducibilityLevel.Modified => "Modified",
                _ => "Non-deterministic"
            },
            Description = level switch
            {
                ReproducibilityLevel.Reproducible => "This comparison can be exactly reproduced with the same inputs.",
                ReproducibilityLevel.Modified => "Some reproducibility data is missing. Results may vary slightly.",
                _ => "Results may vary between runs."
            },
            BadgeColor = level switch
            {
                ReproducibilityLevel.Reproducible => "#10B981", // Green
                ReproducibilityLevel.Modified => "#F59E0B", // Amber
                _ => "#EF4444" // Red
            },
            IconGlyph = level switch
            {
                ReproducibilityLevel.Reproducible => "✓",
                ReproducibilityLevel.Modified => "~",
                _ => "⚡"
            },
            Confidence = Math.Max(0, confidence),
            Factors = factors
        };
    }
    
    /// <summary>
    /// Validate that a replayed result matches the original.
    /// </summary>
    public ReproducibilityValidation ValidateReplay(
        ComparisonReplaySpec originalSpec,
        DeltaComputationResult replayedResult)
    {
        var validation = new ReproducibilityValidation
        {
            OriginalFingerprint = originalSpec.InputFingerprint,
            ReplayedFingerprint = replayedResult.InputFingerprint ?? "",
            OriginalDeltaHash = originalSpec.DeltaHash,
            ReplayedDeltaHash = replayedResult.DeltaHash ?? "",
            OriginalTimestepCount = originalSpec.TimestepCount,
            ReplayedTimestepCount = replayedResult.Alignment.CompareIndex.Length
        };
        
        validation.FingerprintMatch = validation.OriginalFingerprint == validation.ReplayedFingerprint;
        validation.DeltaHashMatch = validation.OriginalDeltaHash == validation.ReplayedDeltaHash;
        validation.TimestepCountMatch = validation.OriginalTimestepCount == validation.ReplayedTimestepCount;
        
        validation.IsExactMatch = validation.FingerprintMatch && 
                                   validation.DeltaHashMatch && 
                                   validation.TimestepCountMatch;
        
        if (validation.IsExactMatch)
        {
            validation.Level = ReproducibilityLevel.Reproducible;
            validation.Summary = "Exact reproduction achieved";
        }
        else
        {
            var mismatches = new List<string>();
            if (!validation.FingerprintMatch) mismatches.Add("input fingerprint");
            if (!validation.DeltaHashMatch) mismatches.Add("delta hash");
            if (!validation.TimestepCountMatch) mismatches.Add("timestep count");
            
            validation.Level = ReproducibilityLevel.Modified;
            validation.Summary = $"Diverged in: {string.Join(", ", mismatches)}";
        }
        
        return validation;
    }
    
    /// <summary>
    /// Generate reproducibility label for display.
    /// </summary>
    public string GetLabel(ReproducibilityLevel level) => level switch
    {
        ReproducibilityLevel.Reproducible => "Reproducible ✓",
        ReproducibilityLevel.Modified => "Modified ~",
        ReproducibilityLevel.NonDeterministic => "Non-deterministic ⚡",
        _ => "Unknown"
    };
    
    /// <summary>
    /// Get badge color for a reproducibility level.
    /// </summary>
    public string GetBadgeColor(ReproducibilityLevel level) => level switch
    {
        ReproducibilityLevel.Reproducible => "#10B981",
        ReproducibilityLevel.Modified => "#F59E0B",
        ReproducibilityLevel.NonDeterministic => "#EF4444",
        _ => "#6B7280"
    };
}

/// <summary>
/// Reproducibility status for a comparison.
/// </summary>
public record ReproducibilityStatus
{
    /// <summary>
    /// Overall reproducibility level.
    /// </summary>
    public required ReproducibilityLevel Level { get; init; }
    
    /// <summary>
    /// Human-readable label.
    /// </summary>
    public required string Label { get; init; }
    
    /// <summary>
    /// Detailed description.
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Badge color (hex).
    /// </summary>
    public required string BadgeColor { get; init; }
    
    /// <summary>
    /// Icon glyph for badge.
    /// </summary>
    public required string IconGlyph { get; init; }
    
    /// <summary>
    /// Confidence score (0-1).
    /// </summary>
    public double Confidence { get; init; }
    
    /// <summary>
    /// Individual factors contributing to status.
    /// </summary>
    public List<ReproducibilityFactor> Factors { get; init; } = [];
    
    /// <summary>
    /// Get formatted badge text.
    /// </summary>
    public string GetBadgeText() => $"{IconGlyph} {Label}";
}

/// <summary>
/// Individual factor affecting reproducibility.
/// </summary>
public record ReproducibilityFactor
{
    public required string Name { get; init; }
    public required FactorStatus Status { get; init; }
    public required string Description { get; init; }
}

/// <summary>
/// Status of a reproducibility factor.
/// </summary>
public enum FactorStatus
{
    Present,
    Missing,
    Modified
}

/// <summary>
/// Level of reproducibility.
/// </summary>
public enum ReproducibilityLevel
{
    /// <summary>
    /// Comparison can be exactly reproduced.
    /// </summary>
    Reproducible,
    
    /// <summary>
    /// Some reproducibility data is missing or modified.
    /// </summary>
    Modified,
    
    /// <summary>
    /// Comparison ran without determinism.
    /// </summary>
    NonDeterministic
}

/// <summary>
/// Validation result for replay reproducibility.
/// </summary>
public record ReproducibilityValidation
{
    public required string OriginalFingerprint { get; init; }
    public required string ReplayedFingerprint { get; init; }
    public required string OriginalDeltaHash { get; init; }
    public required string ReplayedDeltaHash { get; init; }
    public int OriginalTimestepCount { get; init; }
    public int ReplayedTimestepCount { get; init; }
    
    public bool FingerprintMatch { get; set; }
    public bool DeltaHashMatch { get; set; }
    public bool TimestepCountMatch { get; set; }
    public bool IsExactMatch { get; set; }
    
    public ReproducibilityLevel Level { get; set; }
    public string Summary { get; set; } = "";
}

/// <summary>
/// Extension methods for adding reproducibility labels to comparison results.
/// </summary>
public static class ReproducibilityExtensions
{
    /// <summary>
    /// Get reproducibility label for a comparison result.
    /// </summary>
    public static string GetReproducibilityLabel(this DeltaComputationResult result)
    {
        var status = ReproducibilityBadgeService.Instance.ComputeStatus(result);
        return status.Label;
    }
    
    /// <summary>
    /// Check if comparison is fully reproducible.
    /// </summary>
    public static bool IsReproducible(this DeltaComputationResult result)
    {
        var status = ReproducibilityBadgeService.Instance.ComputeStatus(result);
        return status.Level == ReproducibilityLevel.Reproducible;
    }
    
    /// <summary>
    /// Get full reproducibility status.
    /// </summary>
    public static ReproducibilityStatus GetReproducibilityStatus(this DeltaComputationResult result)
    {
        return ReproducibilityBadgeService.Instance.ComputeStatus(result);
    }
}
