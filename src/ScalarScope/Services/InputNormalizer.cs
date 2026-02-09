namespace ScalarScope.Services;

/// <summary>
/// Phase 6.1: Input normalization for deterministic comparison.
/// Ensures minor input variations don't affect output hashes.
/// </summary>
public static class InputNormalizer
{
    /// <summary>
    /// Default decimal precision for floating-point normalization.
    /// </summary>
    public const int DefaultPrecision = 10;
    
    /// <summary>
    /// Normalize a double value to consistent precision.
    /// </summary>
    public static double NormalizeDouble(double value, int precision = DefaultPrecision)
    {
        if (double.IsNaN(value)) return 0.0;
        if (double.IsPositiveInfinity(value)) return double.MaxValue;
        if (double.IsNegativeInfinity(value)) return double.MinValue;
        
        // Round to consistent precision to avoid floating-point variance
        var multiplier = Math.Pow(10, precision);
        return Math.Round(value * multiplier) / multiplier;
    }
    
    /// <summary>
    /// Normalize an array of doubles.
    /// </summary>
    public static double[] NormalizeDoubleArray(IEnumerable<double>? values, int precision = DefaultPrecision)
    {
        if (values == null) return [];
        return values.Select(v => NormalizeDouble(v, precision)).ToArray();
    }
    
    /// <summary>
    /// Normalize a string for consistent comparison.
    /// </summary>
    public static string NormalizeString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        
        // Trim whitespace, normalize line endings, collapse multiple spaces
        return string.Join(" ", value
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }
    
    /// <summary>
    /// Normalize a run ID for consistent hashing.
    /// </summary>
    public static string NormalizeRunId(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId)) return "UNKNOWN";
        
        // Trim, lowercase, remove special chars that might vary
        return runId.Trim().ToLowerInvariant().Replace(" ", "_");
    }
    
    /// <summary>
    /// Normalize timestep data for consistent comparison.
    /// </summary>
    public static NormalizedTimestep NormalizeTimestep(
        int stepIndex,
        IReadOnlyList<double>? state2D,
        double velocityMagnitude,
        double curvature,
        double firstEigenvalue)
    {
        return new NormalizedTimestep
        {
            StepIndex = stepIndex,
            X = NormalizeDouble(state2D?.Count > 0 ? state2D[0] : 0),
            Y = NormalizeDouble(state2D?.Count > 1 ? state2D[1] : 0),
            VelocityMagnitude = NormalizeDouble(velocityMagnitude),
            Curvature = NormalizeDouble(curvature),
            FirstEigenvalue = NormalizeDouble(firstEigenvalue)
        };
    }
    
    /// <summary>
    /// Compute a normalized fingerprint for a trajectory.
    /// </summary>
    public static string ComputeTrajectoryFingerprint(
        IReadOnlyList<NormalizedTimestep> timesteps,
        int sampleCount = 10)
    {
        if (timesteps.Count == 0) return "EMPTY";
        
        // Sample evenly spaced timesteps for fingerprint
        var step = Math.Max(1, timesteps.Count / sampleCount);
        var samples = new List<string>();
        
        for (int i = 0; i < timesteps.Count; i += step)
        {
            var ts = timesteps[i];
            samples.Add($"{ts.X:F6},{ts.Y:F6},{ts.VelocityMagnitude:F6}");
        }
        
        return string.Join("|", samples);
    }
    
    /// <summary>
    /// Normalize comparison inputs and return canonical form.
    /// </summary>
    public static NormalizedComparisonInput NormalizeComparisonInput(
        string? leftRunId,
        string? rightRunId,
        int leftTimestepCount,
        int rightTimestepCount,
        int alignmentMode)
    {
        return new NormalizedComparisonInput
        {
            LeftRunId = NormalizeRunId(leftRunId),
            RightRunId = NormalizeRunId(rightRunId),
            LeftTimestepCount = leftTimestepCount,
            RightTimestepCount = rightTimestepCount,
            AlignmentMode = alignmentMode,
            CanonicalForm = BuildCanonicalForm(
                NormalizeRunId(leftRunId),
                NormalizeRunId(rightRunId),
                leftTimestepCount,
                rightTimestepCount,
                alignmentMode)
        };
    }
    
    private static string BuildCanonicalForm(
        string leftRunId,
        string rightRunId,
        int leftSteps,
        int rightSteps,
        int alignment)
    {
        // Deterministic string format for hashing
        return $"L:{leftRunId}:{leftSteps}|R:{rightRunId}:{rightSteps}|A:{alignment}";
    }
}

/// <summary>
/// Normalized timestep data for deterministic comparison.
/// </summary>
public record NormalizedTimestep
{
    public int StepIndex { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double VelocityMagnitude { get; init; }
    public double Curvature { get; init; }
    public double FirstEigenvalue { get; init; }
}

/// <summary>
/// Normalized comparison input for deterministic fingerprinting.
/// </summary>
public record NormalizedComparisonInput
{
    public required string LeftRunId { get; init; }
    public required string RightRunId { get; init; }
    public int LeftTimestepCount { get; init; }
    public int RightTimestepCount { get; init; }
    public int AlignmentMode { get; init; }
    public required string CanonicalForm { get; init; }
}
