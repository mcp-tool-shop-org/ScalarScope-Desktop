using System.Text.Json.Serialization;
using ScalarScope.Models;

namespace ScalarScope.Services.Evidence;

/// <summary>
/// Phase 3.1: Comparison Set Definition
/// Defines a versioned, reproducible set of comparison pairs for scientific tuning.
/// </summary>
public record ComparisonSetDefinition
{
    /// <summary>Unique identifier for this comparison set.</summary>
    [JsonPropertyName("set_id")]
    public required string SetId { get; init; }

    /// <summary>Version string (semver recommended).</summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>Human-readable description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    /// <summary>Creation timestamp.</summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Research owner who approved this set.</summary>
    [JsonPropertyName("approved_by")]
    public string? ApprovedBy { get; init; }

    /// <summary>Comparison pairs in this set.</summary>
    [JsonPropertyName("pairs")]
    public required List<ComparisonPairDefinition> Pairs { get; init; }

    /// <summary>Alignment modes to test across all pairs.</summary>
    [JsonPropertyName("alignment_modes")]
    public List<TemporalAlignment> AlignmentModes { get; init; } = 
        [TemporalAlignment.ByStep, TemporalAlignment.ByConvergence, TemporalAlignment.ByFirstInstability];

    /// <summary>Whether demo runs are included (must be explicitly labeled).</summary>
    [JsonPropertyName("includes_demo_runs")]
    public bool IncludesDemoRuns { get; init; } = false;

    /// <summary>Configuration hash for reproducibility.</summary>
    [JsonPropertyName("config_hash")]
    public string? ConfigHash { get; init; }
}

/// <summary>
/// A single comparison pair definition.
/// </summary>
public record ComparisonPairDefinition
{
    /// <summary>Stable ID for this pair (e.g., "pair_001").</summary>
    [JsonPropertyName("pair_id")]
    public required string PairId { get; init; }

    /// <summary>Category of this comparison.</summary>
    [JsonPropertyName("category")]
    public required ComparisonCategory Category { get; init; }

    /// <summary>Path to run A file (relative to data root).</summary>
    [JsonPropertyName("run_a_path")]
    public required string RunAPath { get; init; }

    /// <summary>Path to run B file (relative to data root).</summary>
    [JsonPropertyName("run_b_path")]
    public required string RunBPath { get; init; }

    /// <summary>Expected deltas for this pair (for validation).</summary>
    [JsonPropertyName("expected_deltas")]
    public List<string> ExpectedDeltas { get; init; } = [];

    /// <summary>Expected suppressed deltas.</summary>
    [JsonPropertyName("expected_suppressed")]
    public List<string> ExpectedSuppressed { get; init; } = [];

    /// <summary>Notes about this pair.</summary>
    [JsonPropertyName("notes")]
    public string Notes { get; init; } = "";

    /// <summary>Whether this is a demo run pair.</summary>
    [JsonPropertyName("is_demo")]
    public bool IsDemo { get; init; } = false;
}

/// <summary>
/// Categories of comparison pairs for scientific coverage.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComparisonCategory
{
    /// <summary>Clearly different runs - multiple deltas expected.</summary>
    ClearlyDifferent,

    /// <summary>Subtly different - borderline deltas.</summary>
    SubtlyDifferent,

    /// <summary>Nearly identical - most deltas suppressed.</summary>
    NearlyIdentical,

    /// <summary>One run failed, one stable.</summary>
    OneFailureOneStable,

    /// <summary>Mismatched length / timing (early vs late convergence).</summary>
    MismatchedTiming,

    /// <summary>Demo data (explicitly labeled).</summary>
    Demo
}

/// <summary>
/// Loaded comparison pair with actual run data.
/// </summary>
public record LoadedComparisonPair
{
    public required ComparisonPairDefinition Definition { get; init; }
    public required GeometryRun RunA { get; init; }
    public required GeometryRun RunB { get; init; }
    public DateTime LoadedAt { get; init; } = DateTime.UtcNow;
}
