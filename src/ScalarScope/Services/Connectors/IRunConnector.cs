// IRunConnector Interface and Base Infrastructure
// Universal contract for importing training run data.

namespace ScalarScope.Services.Connectors;

/// <summary>
/// Universal contract for training run connectors.
/// Implement this to add support for a new data source.
/// </summary>
public interface IRunConnector
{
    /// <summary>Unique connector identifier (e.g., "tensorboard", "mlflow").</summary>
    string ConnectorId { get; }
    
    /// <summary>Human-readable display name.</summary>
    string DisplayName { get; }
    
    /// <summary>Connector description.</summary>
    string Description { get; }
    
    /// <summary>Capabilities this connector can provide.</summary>
    ConnectorCapabilities Capabilities { get; }
    
    /// <summary>Source type this connector handles.</summary>
    ConnectorSourceType SourceType { get; }
    
    /// <summary>File extensions this connector recognizes.</summary>
    IReadOnlyList<string> FileExtensions { get; }
    
    /// <summary>
    /// Probe a source to check if this connector can handle it.
    /// </summary>
    /// <param name="source">Source path or URI.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Probe result with confidence and details.</returns>
    Task<ConnectorProbeResult> ProbeAsync(string source, CancellationToken ct = default);
    
    /// <summary>
    /// Import a complete run trace from source.
    /// </summary>
    /// <param name="source">Source path or URI.</param>
    /// <param name="options">Import options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Complete run trace.</returns>
    Task<RunTrace> ImportAsync(string source, ConnectorOptions options, CancellationToken ct = default);
    
    /// <summary>
    /// Begin streaming import (for live connectors).
    /// </summary>
    /// <param name="source">Source path or URI.</param>
    /// <param name="options">Import options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async stream of trace updates.</returns>
    IAsyncEnumerable<RunTraceUpdate> StreamAsync(string source, ConnectorOptions options, CancellationToken ct = default);
}

/// <summary>
/// Result of probing a source with a connector.
/// </summary>
public sealed record ConnectorProbeResult
{
    /// <summary>Whether this connector can handle the source.</summary>
    public required bool CanHandle { get; init; }
    
    /// <summary>Confidence level (0-1) that this is the right connector.</summary>
    public required double Confidence { get; init; }
    
    /// <summary>Detected source type.</summary>
    public ConnectorSourceType? DetectedType { get; init; }
    
    /// <summary>Brief description of what was found.</summary>
    public string? Description { get; init; }
    
    /// <summary>Detected capabilities available in this source.</summary>
    public ConnectorCapabilities DetectedCapabilities { get; init; }
    
    /// <summary>Available metric names found.</summary>
    public IReadOnlyList<string>? AvailableMetrics { get; init; }
    
    /// <summary>Estimated number of runs (for multi-run sources).</summary>
    public int? EstimatedRunCount { get; init; }
    
    /// <summary>Warnings or notes about the source.</summary>
    public IReadOnlyList<string>? Warnings { get; init; }
    
    /// <summary>Error if probing failed.</summary>
    public string? Error { get; init; }
    
    /// <summary>Create a "cannot handle" result.</summary>
    public static ConnectorProbeResult CannotHandle(string? reason = null) => new()
    {
        CanHandle = false,
        Confidence = 0.0,
        Error = reason
    };
    
    /// <summary>Create a successful probe result.</summary>
    public static ConnectorProbeResult Success(
        double confidence,
        ConnectorSourceType type,
        ConnectorCapabilities capabilities,
        string? description = null) => new()
    {
        CanHandle = true,
        Confidence = confidence,
        DetectedType = type,
        DetectedCapabilities = capabilities,
        Description = description
    };
}

/// <summary>
/// Options for importing a run trace.
/// </summary>
public sealed record ConnectorOptions
{
    /// <summary>Preset to use for signal mapping.</summary>
    public ConnectorPreset? Preset { get; init; }
    
    /// <summary>Custom label for the run (overrides detected label).</summary>
    public string? CustomLabel { get; init; }
    
    /// <summary>Maximum steps to import (null = all).</summary>
    public int? MaxSteps { get; init; }
    
    /// <summary>Step range to import (inclusive).</summary>
    public (int Start, int End)? StepRange { get; init; }
    
    /// <summary>Metrics to import (null = all).</summary>
    public IReadOnlyList<string>? MetricFilter { get; init; }
    
    /// <summary>Whether to include artifacts.</summary>
    public bool IncludeArtifacts { get; init; } = true;
    
    /// <summary>Whether to verify artifact hashes.</summary>
    public bool VerifyArtifactHashes { get; init; } = false;
    
    /// <summary>Progress callback (0-1).</summary>
    public Action<double>? ProgressCallback { get; init; }
    
    /// <summary>Default options.</summary>
    public static ConnectorOptions Default => new();
}

/// <summary>
/// Registry for discovering and managing connectors.
/// </summary>
public sealed class ConnectorRegistry
{
    /// <summary>Singleton instance.</summary>
    public static readonly ConnectorRegistry Instance = new();
    
    private readonly Dictionary<string, IRunConnector> _connectors = new(StringComparer.OrdinalIgnoreCase);
    
    private ConnectorRegistry() { }
    
    /// <summary>Register a connector.</summary>
    public void Register(IRunConnector connector)
    {
        _connectors[connector.ConnectorId] = connector;
    }
    
    /// <summary>Get a connector by ID.</summary>
    public IRunConnector? Get(string connectorId)
        => _connectors.TryGetValue(connectorId, out var c) ? c : null;
    
    /// <summary>Get all registered connectors.</summary>
    public IReadOnlyCollection<IRunConnector> GetAll() => _connectors.Values;
    
    /// <summary>Get connectors for a specific source type.</summary>
    public IEnumerable<IRunConnector> GetForSourceType(ConnectorSourceType type)
        => _connectors.Values.Where(c => c.SourceType == type);
    
    /// <summary>Get connectors that support a file extension.</summary>
    public IEnumerable<IRunConnector> GetForExtension(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : $".{extension}";
        return _connectors.Values.Where(c => 
            c.FileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Auto-detect the best connector for a source.
    /// </summary>
    public async Task<(IRunConnector? Connector, ConnectorProbeResult Result)> DetectAsync(
        string source,
        CancellationToken ct = default)
    {
        IRunConnector? bestConnector = null;
        ConnectorProbeResult? bestResult = null;
        
        foreach (var connector in _connectors.Values)
        {
            try
            {
                var result = await connector.ProbeAsync(source, ct);
                if (result.CanHandle && (bestResult is null || result.Confidence > bestResult.Confidence))
                {
                    bestConnector = connector;
                    bestResult = result;
                }
            }
            catch
            {
                // Probe failed, try next connector
            }
        }
        
        return (bestConnector, bestResult ?? ConnectorProbeResult.CannotHandle("No suitable connector found"));
    }
    
    /// <summary>
    /// Import a run using auto-detection.
    /// </summary>
    public async Task<RunTrace?> ImportAutoAsync(
        string source,
        ConnectorOptions? options = null,
        CancellationToken ct = default)
    {
        var (connector, result) = await DetectAsync(source, ct);
        if (connector is null || !result.CanHandle)
            return null;
        
        return await connector.ImportAsync(source, options ?? ConnectorOptions.Default, ct);
    }
}

/// <summary>
/// Base class for connectors with common functionality.
/// </summary>
public abstract class RunConnectorBase : IRunConnector
{
    /// <inheritdoc />
    public abstract string ConnectorId { get; }
    
    /// <inheritdoc />
    public abstract string DisplayName { get; }
    
    /// <inheritdoc />
    public abstract string Description { get; }
    
    /// <inheritdoc />
    public abstract ConnectorCapabilities Capabilities { get; }
    
    /// <inheritdoc />
    public abstract ConnectorSourceType SourceType { get; }
    
    /// <inheritdoc />
    public virtual IReadOnlyList<string> FileExtensions => Array.Empty<string>();
    
    /// <inheritdoc />
    public abstract Task<ConnectorProbeResult> ProbeAsync(string source, CancellationToken ct = default);
    
    /// <inheritdoc />
    public abstract Task<RunTrace> ImportAsync(string source, ConnectorOptions options, CancellationToken ct = default);
    
    /// <inheritdoc />
    public virtual async IAsyncEnumerable<RunTraceUpdate> StreamAsync(
        string source, 
        ConnectorOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Default implementation: import once and yield completion
        var trace = await ImportAsync(source, options, ct);
        
        // Yield all scalars as initial update
        var scalarUpdates = new Dictionary<string, ScalarDataPoint>();
        foreach (var (name, series) in trace.Scalars)
        {
            if (series.Steps.Count > 0)
            {
                scalarUpdates[name] = new ScalarDataPoint
                {
                    Step = series.Steps[^1],
                    Value = series.Values[^1]
                };
            }
        }
        
        yield return new RunTraceUpdate
        {
            Type = TraceUpdateType.Complete,
            ScalarUpdates = scalarUpdates,
            NewStepCount = trace.Timeline.StepCount,
            IsComplete = true
        };
    }
    
    /// <summary>
    /// Helper to get effective preset (options preset or default for source).
    /// </summary>
    protected ConnectorPreset GetEffectivePreset(ConnectorOptions options)
    {
        return options.Preset ?? ConnectorPresetService.Instance.GetDefault(SourceType)
            ?? throw new InvalidOperationException($"No preset available for {SourceType}");
    }
    
    /// <summary>
    /// Helper to apply preset mapping to raw metrics.
    /// </summary>
    protected Dictionary<string, ScalarSeries> ApplyPresetMapping(
        Dictionary<string, ScalarSeries> rawMetrics,
        ConnectorPreset preset)
    {
        var result = new Dictionary<string, ScalarSeries>(rawMetrics);
        var metricNames = rawMetrics.Keys.ToList();
        
        // Map primary signals
        if (preset.Mappings.LearningSignal?.Resolve(metricNames) is string learningMetric)
        {
            if (rawMetrics.TryGetValue(learningMetric, out var series))
                result["_learningSignal"] = series;
        }
        
        if (preset.Mappings.EvaluationSignal?.Resolve(metricNames) is string evalMetric)
        {
            if (rawMetrics.TryGetValue(evalMetric, out var series))
                result["_evaluationSignal"] = series;
        }
        
        if (preset.Mappings.CurvatureSignal?.Resolve(metricNames) is string curvMetric)
        {
            if (rawMetrics.TryGetValue(curvMetric, out var series))
                result["_curvatureSignal"] = series;
        }
        
        return result;
    }
    
    /// <summary>
    /// Determine if a path exists as file or directory.
    /// </summary>
    protected static bool PathExists(string path)
    {
        return File.Exists(path) || Directory.Exists(path);
    }
}
