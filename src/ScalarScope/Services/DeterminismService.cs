using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.1: Determinism service for reproducible analysis.
/// Ensures identical inputs produce identical outputs.
/// </summary>
public static class DeterminismService
{
    /// <summary>
    /// Current determinism seed. Set to null for non-deterministic behavior.
    /// </summary>
    public static int? Seed { get; private set; }
    
    /// <summary>
    /// Whether deterministic mode is enabled.
    /// </summary>
    public static bool IsDeterministic => Seed.HasValue;
    
    /// <summary>
    /// Last computed analysis fingerprint.
    /// </summary>
    public static string? LastFingerprint { get; private set; }
    
    /// <summary>
    /// Enable deterministic mode with a specific seed.
    /// </summary>
    public static void EnableDeterminism(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }
    
    /// <summary>
    /// Enable deterministic mode with seed derived from input data.
    /// This ensures same inputs always produce same seed.
    /// </summary>
    public static void EnableDeterminismFromInput(string inputFingerprint)
    {
        var hash = ComputeHash(inputFingerprint);
        Seed = BitConverter.ToInt32(hash, 0);
        _random = new Random(Seed.Value);
    }
    
    /// <summary>
    /// Disable deterministic mode (use system randomness).
    /// </summary>
    public static void DisableDeterminism()
    {
        Seed = null;
        _random = null;
    }
    
    private static Random? _random;
    
    /// <summary>
    /// Get a deterministic random value (0.0 to 1.0).
    /// Falls back to system random if not in deterministic mode.
    /// </summary>
    public static double NextDouble()
    {
        return (_random ?? Random.Shared).NextDouble();
    }
    
    /// <summary>
    /// Get a deterministic random integer.
    /// </summary>
    public static int NextInt(int maxExclusive)
    {
        return (_random ?? Random.Shared).Next(maxExclusive);
    }
    
    /// <summary>
    /// Compute a deterministic fingerprint for comparison inputs.
    /// Same inputs will always produce same fingerprint.
    /// </summary>
    public static string ComputeInputFingerprint(
        string? leftRunId,
        string? rightRunId,
        int alignmentMode,
        int leftTimestepCount,
        int rightTimestepCount)
    {
        var sb = new StringBuilder();
        sb.Append(leftRunId ?? "null");
        sb.Append('|');
        sb.Append(rightRunId ?? "null");
        sb.Append('|');
        sb.Append(alignmentMode);
        sb.Append('|');
        sb.Append(leftTimestepCount);
        sb.Append('|');
        sb.Append(rightTimestepCount);
        
        var fingerprint = ComputeHashString(sb.ToString());
        LastFingerprint = fingerprint;
        return fingerprint;
    }
    
    /// <summary>
    /// Compute a deterministic hash for delta outputs.
    /// Used to verify reproducibility.
    /// </summary>
    public static string ComputeDeltaHash(IEnumerable<object> deltas)
    {
        var serialized = JsonSerializer.Serialize(deltas, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return ComputeHashString(serialized);
    }
    
    /// <summary>
    /// Verify that current outputs match expected hash.
    /// </summary>
    public static DeterminismVerification VerifyDeterminism(
        string expectedHash,
        IEnumerable<object> actualDeltas)
    {
        var actualHash = ComputeDeltaHash(actualDeltas);
        var matches = string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
        
        return new DeterminismVerification
        {
            ExpectedHash = expectedHash,
            ActualHash = actualHash,
            IsMatch = matches,
            Seed = Seed,
            Timestamp = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Get reproducibility metadata for exports.
    /// </summary>
    public static ReproducibilityMetadata GetReproducibilityMetadata()
    {
        return new ReproducibilityMetadata
        {
            IsDeterministic = IsDeterministic,
            Seed = Seed,
            InputFingerprint = LastFingerprint,
            Version = VersionInfo.Version,
            Timestamp = DateTime.UtcNow
        };
    }
    
    private static byte[] ComputeHash(string input)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(input));
    }
    
    private static string ComputeHashString(string input)
    {
        var hash = ComputeHash(input);
        return Convert.ToHexString(hash)[..16]; // 16-char truncated hash
    }
}

/// <summary>
/// Result of determinism verification.
/// </summary>
public record DeterminismVerification
{
    public required string ExpectedHash { get; init; }
    public required string ActualHash { get; init; }
    public required bool IsMatch { get; init; }
    public int? Seed { get; init; }
    public DateTime Timestamp { get; init; }
    
    public override string ToString() => 
        IsMatch 
            ? $"✓ Determinism verified (hash: {ActualHash})"
            : $"✗ Determinism mismatch: expected {ExpectedHash}, got {ActualHash}";
}

/// <summary>
/// Metadata for reproducibility in exports.
/// </summary>
public record ReproducibilityMetadata
{
    public bool IsDeterministic { get; init; }
    public int? Seed { get; init; }
    public string? InputFingerprint { get; init; }
    public string? Version { get; init; }
    public DateTime Timestamp { get; init; }
}
