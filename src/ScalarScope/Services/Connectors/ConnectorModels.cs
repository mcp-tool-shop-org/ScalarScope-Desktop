// RunConnectorContract v1.0 - Core Models
// Universal intermediate representation for training run data.

using System.Text.Json.Serialization;

namespace ScalarScope.Services.Connectors;

#region Enums

/// <summary>
/// Capabilities a connector can provide.
/// Connectors declare these; UI adapts based on what's available.
/// </summary>
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorCapabilities
{
    /// <summary>No capabilities (invalid).</summary>
    None = 0,
    
    /// <summary>Basic scalar metrics (minimum requirement).</summary>
    Scalars = 1 << 0,
    
    /// <summary>Epoch/eval/checkpoint markers.</summary>
    Milestones = 1 << 1,
    
    /// <summary>Checkpoint/model file references with hashes.</summary>
    Artifacts = 1 << 2,
    
    /// <summary>Wall-clock timing per step.</summary>
    WallClock = 1 << 3,
    
    /// <summary>Curvature proxy signals.</summary>
    Curvature = 1 << 4,
    
    /// <summary>Eigenvalue spectrum data.</summary>
    Spectrum = 1 << 5,
    
    /// <summary>Evaluator alignment vectors.</summary>
    Evaluators = 1 << 6,
    
    /// <summary>Supports live append-only streaming.</summary>
    Streaming = 1 << 7,
    
    /// <summary>Full fingerprint provenance (code/dataset/model/seed).</summary>
    Fingerprints = 1 << 8,
    
    /// <summary>Standard offline connector capabilities.</summary>
    StandardOffline = Scalars | Milestones,
    
    /// <summary>Full offline capabilities.</summary>
    FullOffline = Scalars | Milestones | Artifacts | WallClock | Fingerprints,
    
    /// <summary>Full capabilities including streaming.</summary>
    Full = FullOffline | Curvature | Spectrum | Evaluators | Streaming
}

/// <summary>
/// Milestone types in training runs.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MilestoneType
{
    /// <summary>Epoch boundary.</summary>
    [JsonPropertyName("epoch")] Epoch,
    
    /// <summary>Evaluation/validation point.</summary>
    [JsonPropertyName("eval")] Eval,
    
    /// <summary>Checkpoint save point.</summary>
    [JsonPropertyName("checkpoint")] Checkpoint,
    
    /// <summary>Learning rate schedule step.</summary>
    [JsonPropertyName("lr_step")] LrStep,
    
    /// <summary>Custom user-defined marker.</summary>
    [JsonPropertyName("custom")] Custom
}

/// <summary>
/// Artifact types that can be referenced.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArtifactType
{
    /// <summary>Model checkpoint file.</summary>
    [JsonPropertyName("checkpoint")] Checkpoint,
    
    /// <summary>Final/best model.</summary>
    [JsonPropertyName("model")] Model,
    
    /// <summary>Optimizer state.</summary>
    [JsonPropertyName("optimizer")] Optimizer,
    
    /// <summary>Training configuration.</summary>
    [JsonPropertyName("config")] Config,
    
    /// <summary>Other artifact.</summary>
    [JsonPropertyName("other")] Other
}

/// <summary>
/// Alignment modes for comparing training runs.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TrainingAlignmentMode
{
    /// <summary>Align by training step (default for same-length runs).</summary>
    [JsonPropertyName("step")] Step,
    
    /// <summary>Align when signal stabilizes (different schedules).</summary>
    [JsonPropertyName("convergenceOnset")] ConvergenceOnset,
    
    /// <summary>Align by evaluation milestone index.</summary>
    [JsonPropertyName("evalMilestone")] EvalMilestone,
    
    /// <summary>Align by epoch boundary.</summary>
    [JsonPropertyName("epoch")] Epoch
}

/// <summary>
/// Step unit for timeline interpretation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepUnit
{
    /// <summary>Training iteration/batch.</summary>
    [JsonPropertyName("iteration")] Iteration,
    
    /// <summary>Epoch number.</summary>
    [JsonPropertyName("epoch")] Epoch,
    
    /// <summary>Samples seen.</summary>
    [JsonPropertyName("samples")] Samples,
    
    /// <summary>Tokens processed.</summary>
    [JsonPropertyName("tokens")] Tokens,
    
    /// <summary>Wall-clock seconds.</summary>
    [JsonPropertyName("seconds")] Seconds
}

/// <summary>
/// Source type for run data.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConnectorSourceType
{
    /// <summary>TensorBoard event files.</summary>
    [JsonPropertyName("tensorboard")] TensorBoard,
    
    /// <summary>MLflow tracking server or local store.</summary>
    [JsonPropertyName("mlflow")] MLflow,
    
    /// <summary>Weights & Biases.</summary>
    [JsonPropertyName("wandb")] WeightsAndBiases,
    
    /// <summary>CSV/JSON log directory.</summary>
    [JsonPropertyName("logs")] LogDirectory,
    
    /// <summary>HuggingFace Trainer logs.</summary>
    [JsonPropertyName("huggingface")] HuggingFace,
    
    /// <summary>ScalarScope native format.</summary>
    [JsonPropertyName("native")] Native,
    
    /// <summary>Unknown/other source.</summary>
    [JsonPropertyName("unknown")] Unknown
}

#endregion

#region RunTrace - Core Model

/// <summary>
/// Universal intermediate representation for training run data.
/// All connectors produce this format; ScalarScope consumes it.
/// </summary>
public sealed record RunTrace
{
    /// <summary>Schema version (currently "1.0.0").</summary>
    public required string TraceVersion { get; init; }
    
    /// <summary>Run metadata (IDs, labels, fingerprints).</summary>
    public required RunTraceMetadata Metadata { get; init; }
    
    /// <summary>Capabilities present in this trace.</summary>
    public required ConnectorCapabilities Capabilities { get; init; }
    
    /// <summary>Timeline information (step count, timing).</summary>
    public required RunTraceTimeline Timeline { get; init; }
    
    /// <summary>Scalar metric series.</summary>
    public required IReadOnlyDictionary<string, ScalarSeries> Scalars { get; init; }
    
    /// <summary>Training milestones (epochs, evals, checkpoints).</summary>
    public IReadOnlyList<Milestone>? Milestones { get; init; }
    
    /// <summary>Advanced signal data (curvature, spectrum, evaluators).</summary>
    public SignalData? Signals { get; init; }
    
    /// <summary>Artifact references (checkpoints, models).</summary>
    public IReadOnlyList<ArtifactReference>? Artifacts { get; init; }
    
    /// <summary>Current trace version constant.</summary>
    public const string CurrentVersion = "1.0.0";
    
    /// <summary>Create a minimal trace with only scalars.</summary>
    public static RunTrace CreateMinimal(
        string runId,
        string label,
        ConnectorSourceType source,
        IReadOnlyDictionary<string, ScalarSeries> scalars)
    {
        var stepCount = scalars.Values.MaxBy(s => s.Steps.Count)?.Steps.Count ?? 0;
        
        return new RunTrace
        {
            TraceVersion = CurrentVersion,
            Metadata = new RunTraceMetadata
            {
                RunId = runId,
                Label = label,
                Source = source,
                CreatedUtc = DateTimeOffset.UtcNow
            },
            Capabilities = ConnectorCapabilities.Scalars,
            Timeline = new RunTraceTimeline
            {
                StepCount = stepCount,
                StepUnit = StepUnit.Iteration
            },
            Scalars = scalars
        };
    }
}

/// <summary>
/// Run metadata including identifiers and fingerprints.
/// </summary>
public sealed record RunTraceMetadata
{
    /// <summary>Unique run identifier.</summary>
    public required string RunId { get; init; }
    
    /// <summary>Human-readable label.</summary>
    public required string Label { get; init; }
    
    /// <summary>Source connector type.</summary>
    public required ConnectorSourceType Source { get; init; }
    
    /// <summary>Source connector version (e.g., TensorBoard 2.15.0).</summary>
    public string? SourceVersion { get; init; }
    
    /// <summary>When this trace was created/imported.</summary>
    public required DateTimeOffset CreatedUtc { get; init; }
    
    /// <summary>Provenance fingerprints for reproducibility.</summary>
    public RunFingerprints? Fingerprints { get; init; }
    
    /// <summary>User-defined tags.</summary>
    public IReadOnlyDictionary<string, string>? Tags { get; init; }
    
    /// <summary>Original source path/URI.</summary>
    public string? SourcePath { get; init; }
}

/// <summary>
/// Provenance fingerprints for reproducibility tracking.
/// </summary>
public sealed record RunFingerprints
{
    /// <summary>Code/git commit hash.</summary>
    public string? Code { get; init; }
    
    /// <summary>Dataset fingerprint.</summary>
    public string? Dataset { get; init; }
    
    /// <summary>Model architecture fingerprint.</summary>
    public string? Model { get; init; }
    
    /// <summary>Random seed used.</summary>
    public int? Seed { get; init; }
    
    /// <summary>Docker image hash (for containerized training).</summary>
    public string? DockerImage { get; init; }
    
    /// <summary>Environment fingerprint (packages, versions).</summary>
    public string? Environment { get; init; }
}

/// <summary>
/// Timeline information for the training run.
/// </summary>
public sealed record RunTraceTimeline
{
    /// <summary>Total number of steps.</summary>
    public required int StepCount { get; init; }
    
    /// <summary>What a "step" represents.</summary>
    public required StepUnit StepUnit { get; init; }
    
    /// <summary>Training start time (wall clock).</summary>
    public DateTimeOffset? WallClockStart { get; init; }
    
    /// <summary>Training end time (wall clock).</summary>
    public DateTimeOffset? WallClockEnd { get; init; }
    
    /// <summary>Wall clock time per step (seconds from start). Optional.</summary>
    public IReadOnlyList<double>? WallClockPerStep { get; init; }
    
    /// <summary>Total training duration.</summary>
    public TimeSpan? Duration => WallClockStart.HasValue && WallClockEnd.HasValue
        ? WallClockEnd.Value - WallClockStart.Value
        : null;
}

/// <summary>
/// A series of scalar values over training steps.
/// </summary>
public sealed record ScalarSeries
{
    /// <summary>Step indices where values were recorded.</summary>
    public required IReadOnlyList<int> Steps { get; init; }
    
    /// <summary>Values at each step.</summary>
    public required IReadOnlyList<double> Values { get; init; }
    
    /// <summary>Wall-clock timestamps (optional).</summary>
    public IReadOnlyList<double>? WallClockSeconds { get; init; }
    
    /// <summary>Number of data points.</summary>
    public int Count => Steps.Count;
    
    /// <summary>Whether this series is empty.</summary>
    public bool IsEmpty => Steps.Count == 0;
}

/// <summary>
/// A training milestone (epoch, eval, checkpoint).
/// </summary>
public sealed record Milestone
{
    /// <summary>Step at which milestone occurred.</summary>
    public required int Step { get; init; }
    
    /// <summary>Type of milestone.</summary>
    public required MilestoneType Type { get; init; }
    
    /// <summary>Index within type (e.g., epoch 5, eval 3).</summary>
    public int Index { get; init; }
    
    /// <summary>Optional path (for checkpoints).</summary>
    public string? Path { get; init; }
    
    /// <summary>Optional hash (for checkpoints).</summary>
    public string? Hash { get; init; }
    
    /// <summary>Custom label.</summary>
    public string? Label { get; init; }
    
    /// <summary>Additional metadata.</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Advanced signal data (curvature, spectrum, evaluators).
/// </summary>
public sealed record SignalData
{
    /// <summary>Curvature proxy signal.</summary>
    public ScalarSeries? Curvature { get; init; }
    
    /// <summary>Eigenvalue spectrum over time. Each entry is a vector of eigenvalues.</summary>
    public SpectrumSeries? EigenSpectrum { get; init; }
    
    /// <summary>Evaluator alignment vectors.</summary>
    public VectorSeries? EvaluatorVectors { get; init; }
    
    /// <summary>Gradient norm signal.</summary>
    public ScalarSeries? GradientNorm { get; init; }
    
    /// <summary>Update norm signal.</summary>
    public ScalarSeries? UpdateNorm { get; init; }
}

/// <summary>
/// Spectrum (eigenvalue) series over training.
/// </summary>
public sealed record SpectrumSeries
{
    /// <summary>Steps where spectrum was computed.</summary>
    public required IReadOnlyList<int> Steps { get; init; }
    
    /// <summary>Eigenvalue vectors at each step.</summary>
    public required IReadOnlyList<IReadOnlyList<double>> Values { get; init; }
}

/// <summary>
/// Vector series (e.g., evaluator vectors) over training.
/// </summary>
public sealed record VectorSeries
{
    /// <summary>Steps where vectors were recorded.</summary>
    public required IReadOnlyList<int> Steps { get; init; }
    
    /// <summary>Vectors at each step.</summary>
    public required IReadOnlyList<IReadOnlyList<double>> Values { get; init; }
}

/// <summary>
/// Reference to an artifact (checkpoint, model file).
/// </summary>
public sealed record ArtifactReference
{
    /// <summary>Step at which artifact was created.</summary>
    public required int Step { get; init; }
    
    /// <summary>Type of artifact.</summary>
    public required ArtifactType Type { get; init; }
    
    /// <summary>Path to artifact.</summary>
    public required string Path { get; init; }
    
    /// <summary>SHA-256 hash of artifact.</summary>
    public string? Hash { get; init; }
    
    /// <summary>Size in bytes.</summary>
    public long? Bytes { get; init; }
    
    /// <summary>Additional metadata.</summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

#endregion

#region Streaming Support

/// <summary>
/// Incremental update for streaming connectors.
/// </summary>
public sealed record RunTraceUpdate
{
    /// <summary>Type of update.</summary>
    public required TraceUpdateType Type { get; init; }
    
    /// <summary>New scalar data points.</summary>
    public IReadOnlyDictionary<string, ScalarDataPoint>? ScalarUpdates { get; init; }
    
    /// <summary>New milestone.</summary>
    public Milestone? NewMilestone { get; init; }
    
    /// <summary>New artifact reference.</summary>
    public ArtifactReference? NewArtifact { get; init; }
    
    /// <summary>Signal updates.</summary>
    public SignalUpdate? SignalUpdate { get; init; }
    
    /// <summary>Updated step count.</summary>
    public int? NewStepCount { get; init; }
    
    /// <summary>Stream is complete (training finished).</summary>
    public bool IsComplete { get; init; }
}

/// <summary>
/// Type of trace update.
/// </summary>
public enum TraceUpdateType
{
    /// <summary>New scalar data points.</summary>
    Scalars,
    
    /// <summary>New milestone added.</summary>
    Milestone,
    
    /// <summary>New artifact added.</summary>
    Artifact,
    
    /// <summary>Signal data update.</summary>
    Signal,
    
    /// <summary>Metadata update.</summary>
    Metadata,
    
    /// <summary>Training completed.</summary>
    Complete,
    
    /// <summary>Error occurred.</summary>
    Error
}

/// <summary>
/// Single scalar data point for streaming.
/// </summary>
public sealed record ScalarDataPoint
{
    /// <summary>Step number.</summary>
    public required int Step { get; init; }
    
    /// <summary>Value.</summary>
    public required double Value { get; init; }
    
    /// <summary>Wall clock time (seconds from start).</summary>
    public double? WallClockSeconds { get; init; }
}

/// <summary>
/// Signal data update for streaming.
/// </summary>
public sealed record SignalUpdate
{
    /// <summary>Step number.</summary>
    public required int Step { get; init; }
    
    /// <summary>Curvature value.</summary>
    public double? Curvature { get; init; }
    
    /// <summary>Eigenvalue spectrum.</summary>
    public IReadOnlyList<double>? EigenSpectrum { get; init; }
    
    /// <summary>Evaluator vector.</summary>
    public IReadOnlyList<double>? EvaluatorVector { get; init; }
    
    /// <summary>Gradient norm.</summary>
    public double? GradientNorm { get; init; }
}

#endregion
