// RuntimeRunTrace v1.0.0 Models
// Unified representation for training, inference, and evaluation runs.
// Supports before/after comparison with runtime-specific milestones.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace ScalarScope.Services.Connectors;

#region Enums

/// <summary>
/// Type of run (training, inference, evaluation).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunType
{
    [JsonPropertyName("training")] Training,
    [JsonPropertyName("inference")] Inference,
    [JsonPropertyName("evaluation")] Evaluation
}

/// <summary>
/// Framework that produced the run data.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FrameworkType
{
    [JsonPropertyName("tensorflowrt")] TensorFlowRT,
    [JsonPropertyName("tensorflow")] TensorFlow,
    [JsonPropertyName("pytorch")] PyTorch,
    [JsonPropertyName("jax")] Jax,
    [JsonPropertyName("mlflow")] MLflow,
    [JsonPropertyName("wandb")] WandB,
    [JsonPropertyName("tensorboard")] TensorBoard,
    [JsonPropertyName("unknown")] Unknown
}

/// <summary>
/// Unit for scalar series values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScalarUnit
{
    [JsonPropertyName("none")] None,
    [JsonPropertyName("loss")] Loss,
    [JsonPropertyName("accuracy")] Accuracy,
    [JsonPropertyName("percent")] Percent,
    [JsonPropertyName("seconds")] Seconds,
    [JsonPropertyName("milliseconds")] Milliseconds,
    [JsonPropertyName("microseconds")] Microseconds,
    [JsonPropertyName("items_per_second")] ItemsPerSecond,
    [JsonPropertyName("bytes")] Bytes,
    [JsonPropertyName("megabytes")] Megabytes,
    [JsonPropertyName("gigabytes")] Gigabytes,
    [JsonPropertyName("count")] Count
}

/// <summary>
/// Aggregation type for scalar values.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScalarAggregation
{
    [JsonPropertyName("none")] None,
    [JsonPropertyName("mean")] Mean,
    [JsonPropertyName("median")] Median,
    [JsonPropertyName("p50")] P50,
    [JsonPropertyName("p90")] P90,
    [JsonPropertyName("p95")] P95,
    [JsonPropertyName("p99")] P99
}

/// <summary>
/// Runtime milestone types (broader than training-only).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeMilestoneType
{
    [JsonPropertyName("epoch_start")] EpochStart,
    [JsonPropertyName("epoch_end")] EpochEnd,
    [JsonPropertyName("eval")] Eval,
    [JsonPropertyName("checkpoint")] Checkpoint,
    [JsonPropertyName("warmup_end")] WarmupEnd,
    [JsonPropertyName("steady_state_start")] SteadyStateStart,
    [JsonPropertyName("steady_state_end")] SteadyStateEnd,
    [JsonPropertyName("custom")] Custom
}

/// <summary>
/// Artifact types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeArtifactType
{
    [JsonPropertyName("model")] Model,
    [JsonPropertyName("checkpoint")] Checkpoint,
    [JsonPropertyName("log")] Log,
    [JsonPropertyName("profile")] Profile,
    [JsonPropertyName("other")] Other
}

#endregion

#region RuntimeRunTrace - Core Model

/// <summary>
/// Universal run trace for training, inference, and evaluation.
/// Supports before/after comparison with deterministic fingerprints.
/// </summary>
public sealed record RuntimeRunTrace
{
    /// <summary>Schema version (1.0.0).</summary>
    public required string SchemaVersion { get; init; }
    
    /// <summary>Unique run identifier.</summary>
    public required string RunId { get; init; }
    
    /// <summary>Type of run.</summary>
    public required RunType RunType { get; init; }
    
    /// <summary>Framework that produced this run.</summary>
    public required FrameworkType Framework { get; init; }
    
    /// <summary>When this trace was created.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
    
    /// <summary>Human-readable label.</summary>
    public string? Label { get; init; }
    
    /// <summary>Run metadata with fingerprints.</summary>
    public required RuntimeMetadata Metadata { get; init; }
    
    /// <summary>Timeline (steps, wall time).</summary>
    public required RuntimeTimeline Timeline { get; init; }
    
    /// <summary>Scalar metric series.</summary>
    public required RuntimeScalars Scalars { get; init; }
    
    /// <summary>Milestones (epochs, evals, warmup, steady state).</summary>
    public required RuntimeMilestones Milestones { get; init; }
    
    /// <summary>Artifacts (models, checkpoints, logs).</summary>
    public IReadOnlyList<RuntimeArtifact>? Artifacts { get; init; }
    
    /// <summary>Capability flags.</summary>
    public required RuntimeCapabilities Capabilities { get; init; }
    
    /// <summary>Import provenance.</summary>
    public RuntimeProvenance? Provenance { get; init; }
    
    /// <summary>Current schema version.</summary>
    public const string CurrentSchemaVersion = "1.0.0";
}

/// <summary>
/// Runtime metadata with deterministic fingerprints.
/// </summary>
public sealed record RuntimeMetadata
{
    /// <summary>Model fingerprint (SHA-256).</summary>
    public required string ModelFingerprint { get; init; }
    
    /// <summary>Dataset fingerprint (SHA-256).</summary>
    public required string DatasetFingerprint { get; init; }
    
    /// <summary>Code fingerprint (SHA-256).</summary>
    public required string CodeFingerprint { get; init; }
    
    /// <summary>Environment fingerprint (SHA-256).</summary>
    public required string EnvironmentFingerprint { get; init; }
    
    /// <summary>Random seed (null if not deterministic).</summary>
    public int? Seed { get; init; }
    
    /// <summary>User-defined tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
    
    /// <summary>Notes about this run.</summary>
    public string? Notes { get; init; }
    
    /// <summary>Framework-specific details.</summary>
    public IReadOnlyDictionary<string, object>? FrameworkDetails { get; init; }
    
    /// <summary>Create a fingerprint from content string.</summary>
    public static string CreateFingerprint(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    /// <summary>Create a fingerprint for "unknown" placeholder.</summary>
    public static string UnknownFingerprint(string category)
        => CreateFingerprint($"unknown:{category}:{DateTimeOffset.UtcNow:yyyy-MM-dd}");
}

/// <summary>
/// Runtime timeline with steps and optional wall time.
/// </summary>
public sealed record RuntimeTimeline
{
    /// <summary>Step indices (monotonically increasing).</summary>
    public required IReadOnlyList<int> Steps { get; init; }
    
    /// <summary>Wall clock seconds from start (optional).</summary>
    public IReadOnlyList<double>? WallTimeSeconds { get; init; }
    
    /// <summary>Epoch indices (optional, for training).</summary>
    public IReadOnlyList<int>? Epoch { get; init; }
    
    /// <summary>Number of steps.</summary>
    public int StepCount => Steps.Count;
    
    /// <summary>First step index.</summary>
    public int FirstStep => Steps.Count > 0 ? Steps[0] : 0;
    
    /// <summary>Last step index.</summary>
    public int LastStep => Steps.Count > 0 ? Steps[^1] : 0;
}

/// <summary>
/// Scalar metric series.
/// </summary>
public sealed record RuntimeScalarSeries
{
    /// <summary>Metric name.</summary>
    public required string Name { get; init; }
    
    /// <summary>Unit of measurement.</summary>
    public required ScalarUnit Unit { get; init; }
    
    /// <summary>Values (one per step, null for missing).</summary>
    public required IReadOnlyList<double?> Values { get; init; }
    
    /// <summary>Description.</summary>
    public string? Description { get; init; }
    
    /// <summary>Original key from source.</summary>
    public string? SourceKey { get; init; }
    
    /// <summary>Aggregation type (for percentile series).</summary>
    public ScalarAggregation? Aggregation { get; init; }
    
    /// <summary>Number of non-null values.</summary>
    public int ValidCount => Values.Count(v => v.HasValue);
}

/// <summary>
/// Container for scalar series.
/// </summary>
public sealed record RuntimeScalars
{
    /// <summary>All scalar series.</summary>
    public required IReadOnlyList<RuntimeScalarSeries> Series { get; init; }
    
    /// <summary>Get series by name.</summary>
    public RuntimeScalarSeries? GetByName(string name)
        => Series.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>Check if a named series exists.</summary>
    public bool Has(string name)
        => Series.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Runtime milestone.
/// </summary>
public sealed record RuntimeMilestone
{
    /// <summary>Milestone type.</summary>
    public required RuntimeMilestoneType Type { get; init; }
    
    /// <summary>Step at which milestone occurred.</summary>
    public required int Step { get; init; }
    
    /// <summary>Optional label.</summary>
    public string? Label { get; init; }
    
    /// <summary>Additional metadata.</summary>
    public IReadOnlyDictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Container for milestones.
/// </summary>
public sealed record RuntimeMilestones
{
    /// <summary>List of milestones.</summary>
    public required IReadOnlyList<RuntimeMilestone> List { get; init; }
    
    /// <summary>Get milestones by type.</summary>
    public IEnumerable<RuntimeMilestone> OfType(RuntimeMilestoneType type)
        => List.Where(m => m.Type == type);
    
    /// <summary>Get warmup end step (or null).</summary>
    public int? WarmupEndStep => OfType(RuntimeMilestoneType.WarmupEnd).FirstOrDefault()?.Step;
    
    /// <summary>Get steady state start step (or null).</summary>
    public int? SteadyStateStartStep => OfType(RuntimeMilestoneType.SteadyStateStart).FirstOrDefault()?.Step;
}

/// <summary>
/// Runtime artifact reference.
/// </summary>
public sealed record RuntimeArtifact
{
    /// <summary>Artifact type.</summary>
    public required RuntimeArtifactType Type { get; init; }
    
    /// <summary>Artifact name.</summary>
    public required string Name { get; init; }
    
    /// <summary>Path to artifact.</summary>
    public string? Path { get; init; }
    
    /// <summary>SHA-256 hash.</summary>
    public required string Sha256 { get; init; }
    
    /// <summary>Size in bytes.</summary>
    public long? Bytes { get; init; }
    
    /// <summary>Content type.</summary>
    public string? ContentType { get; init; }
}

/// <summary>
/// Capability flags indicating what's present in the trace.
/// </summary>
public sealed record RuntimeCapabilities
{
    /// <summary>Has loss metric.</summary>
    public required bool HasLoss { get; init; }
    
    /// <summary>Has accuracy metric.</summary>
    public required bool HasAccuracy { get; init; }
    
    /// <summary>Has latency metric.</summary>
    public required bool HasLatency { get; init; }
    
    /// <summary>Has throughput metric.</summary>
    public required bool HasThroughput { get; init; }
    
    /// <summary>Has memory metric.</summary>
    public required bool HasMemory { get; init; }
    
    /// <summary>Has checkpoint artifacts.</summary>
    public required bool HasCheckpoints { get; init; }
    
    /// <summary>Has profiler data.</summary>
    public required bool HasProfiler { get; init; }
    
    /// <summary>Has evaluator vectors.</summary>
    public required bool HasEvaluatorVectors { get; init; }
    
    /// <summary>Has eigenvalue spectrum.</summary>
    public required bool HasEigenSpectrum { get; init; }
    
    /// <summary>Create capabilities from scalar series.</summary>
    public static RuntimeCapabilities Detect(RuntimeScalars scalars, bool hasProfiler = false)
    {
        return new RuntimeCapabilities
        {
            HasLoss = scalars.Has("loss") || scalars.Has("train_loss"),
            HasAccuracy = scalars.Has("accuracy") || scalars.Has("train_accuracy"),
            HasLatency = scalars.Has("latency_ms") || scalars.Has("latency"),
            HasThroughput = scalars.Has("throughput_items_per_sec") || scalars.Has("throughput"),
            HasMemory = scalars.Has("memory_bytes") || scalars.Has("memory_mb"),
            HasCheckpoints = false, // Detected from artifacts
            HasProfiler = hasProfiler,
            HasEvaluatorVectors = false,
            HasEigenSpectrum = false
        };
    }
}

/// <summary>
/// Import provenance.
/// </summary>
public sealed record RuntimeProvenance
{
    /// <summary>Original source path/URI.</summary>
    public required string Source { get; init; }
    
    /// <summary>Source version.</summary>
    public string? SourceVersion { get; init; }
    
    /// <summary>When the trace was ingested.</summary>
    public DateTimeOffset? IngestedUtc { get; init; }
    
    /// <summary>Connector ID that produced this trace.</summary>
    public string? ConnectorId { get; init; }
    
    /// <summary>Connector version.</summary>
    public string? ConnectorVersion { get; init; }
}

#endregion

#region Runtime Alignment

/// <summary>
/// Alignment modes for runtime (before/after) comparison.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeAlignmentMode
{
    /// <summary>Align by step index.</summary>
    Step,
    
    /// <summary>Align at first steady-state step.</summary>
    SteadyStateStart,
    
    /// <summary>Align at warmup end.</summary>
    WarmupEnd,
    
    /// <summary>Align by milestone index.</summary>
    Milestone,
    
    /// <summary>Align at convergence onset.</summary>
    ConvergenceOnset
}

/// <summary>
/// Helper for detecting steady state in runtime metrics.
/// </summary>
public static class SteadyStateDetector
{
    /// <summary>
    /// Detect warmup end step using latency stability heuristic.
    /// </summary>
    public static int? DetectWarmupEnd(IReadOnlyList<double?> latencyValues, int minWarmupSteps = 5)
    {
        if (latencyValues.Count < minWarmupSteps * 2)
            return null;
        
        // Get valid values
        var valid = latencyValues.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (valid.Count < minWarmupSteps * 2)
            return null;
        
        // Find where latency stops decreasing rapidly
        // Use a simple heuristic: find first point where local avg stabilizes
        var windowSize = Math.Max(3, valid.Count / 20);
        
        for (int i = windowSize; i < valid.Count - windowSize; i++)
        {
            var prevWindow = valid.Skip(i - windowSize).Take(windowSize).Average();
            var nextWindow = valid.Skip(i).Take(windowSize).Average();
            
            // If next window is not significantly lower, warmup is done
            var relativeChange = (prevWindow - nextWindow) / prevWindow;
            if (relativeChange < 0.05) // Less than 5% improvement
            {
                // Map back to original index
                int validIndex = 0;
                for (int j = 0; j < latencyValues.Count && validIndex <= i; j++)
                {
                    if (latencyValues[j].HasValue)
                    {
                        if (validIndex == i)
                            return j;
                        validIndex++;
                    }
                }
            }
        }
        
        return minWarmupSteps; // Default fallback
    }
    
    /// <summary>
    /// Detect steady state window.
    /// </summary>
    public static (int Start, int End)? DetectSteadyState(
        IReadOnlyList<double?> values, 
        int warmupEndStep,
        double stabilityThreshold = 0.1)
    {
        if (values.Count <= warmupEndStep)
            return null;
        
        var steadyValues = values.Skip(warmupEndStep).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        if (steadyValues.Count < 10)
            return null;
        
        // Calculate coefficient of variation for steady state validation
        var mean = steadyValues.Average();
        var stdDev = Math.Sqrt(steadyValues.Select(v => Math.Pow(v - mean, 2)).Average());
        var cv = stdDev / Math.Max(mean, 1e-10);
        
        if (cv < stabilityThreshold)
        {
            return (warmupEndStep, values.Count - 1);
        }
        
        return null;
    }
}

#endregion
