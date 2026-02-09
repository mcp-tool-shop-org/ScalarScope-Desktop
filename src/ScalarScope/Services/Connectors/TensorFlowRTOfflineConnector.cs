// TensorFlowRT Offline Connector
// Parses TFRT profiler exports, CSVs, and logs into RuntimeRunTrace.
// Enables before/after optimization comparison for inference workloads.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ScalarScope.Services.Connectors;

#region Error Codes

/// <summary>
/// TFRT-specific error codes.
/// All routed through Phase 6 ErrorExplanationService.
/// </summary>
public static partial class TfrtErrorCodes
{
    /// <summary>No supported TFRT export found at source.</summary>
    public const string TFRT_NO_SUPPORTED_EXPORT = "TFRT_NO_SUPPORTED_EXPORT";
    
    /// <summary>File unreadable or malformed.</summary>
    public const string TFRT_PARSE_FAILED = "TFRT_PARSE_FAILED";
    
    /// <summary>Cannot extract latency signal from source.</summary>
    public const string TFRT_NO_LATENCY_SIGNAL = "TFRT_NO_LATENCY_SIGNAL";
    
    /// <summary>Parsed series have inconsistent lengths.</summary>
    public const string TFRT_INCONSISTENT_LENGTHS = "TFRT_INCONSISTENT_LENGTHS";
    
    /// <summary>Cannot normalize units (ambiguous).</summary>
    public const string TFRT_UNIT_AMBIGUOUS = "TFRT_UNIT_AMBIGUOUS";
    
    // Legacy codes (kept for compatibility)
    
    /// <summary>Timeline data is inconsistent (non-monotonic steps).</summary>
    public const string TFRT_TIMELINE_INCONSISTENT = "TFRT_TIMELINE_INCONSISTENT";
    
    /// <summary>Metric units could not be determined.</summary>
    public const string TFRT_UNITS_UNKNOWN = "TFRT_UNITS_UNKNOWN";
    
    /// <summary>Profile data parsing failed.</summary>
    public const string TFRT_PROFILE_PARSE_FAILED = "TFRT_PROFILE_PARSE_FAILED";
    
    /// <summary>SavedModel could not be fingerprinted.</summary>
    public const string TFRT_MODEL_FINGERPRINT_FAILED = "TFRT_MODEL_FINGERPRINT_FAILED";
    
    /// <summary>CSV has unexpected format.</summary>
    public const string TFRT_CSV_FORMAT_ERROR = "TFRT_CSV_FORMAT_ERROR";
}

#endregion

#region TFRT Source Detection

/// <summary>
/// Detected TFRT source type.
/// Priority order: ProfilerTrace > ProfilerOverview > BenchmarkCsv > BenchmarkJson > RuntimeLog
/// </summary>
public enum TfrtSourceType
{
    /// <summary>Profiler trace.json (highest fidelity).</summary>
    ProfilerTrace,
    
    /// <summary>Profiler overview.json.</summary>
    ProfilerOverview,
    
    /// <summary>Benchmark CSV export.</summary>
    BenchmarkCsv,
    
    /// <summary>Benchmark JSON export.</summary>
    BenchmarkJson,
    
    /// <summary>Runtime log files (last resort).</summary>
    RuntimeLog,
    
    /// <summary>Unknown source.</summary>
    Unknown
}

/// <summary>
/// Detected TFRT source.
/// </summary>
public sealed record TfrtSource
{
    public required TfrtSourceType Type { get; init; }
    public required string Path { get; init; }
    
    /// <summary>Priority for selection (higher = preferred).</summary>
    public int Priority { get; init; }
    
    /// <summary>Additional context files found nearby.</summary>
    public TfrtFolderContext? Context { get; init; }
}

/// <summary>
/// Context from the TFRT folder structure.
/// </summary>
public sealed record TfrtFolderContext
{
    /// <summary>Path to saved_model directory (if found).</summary>
    public string? SavedModelPath { get; init; }
    
    /// <summary>Path to config.json (if found).</summary>
    public string? ConfigPath { get; init; }
    
    /// <summary>Explicit warmup_steps from config.</summary>
    public int? WarmupSteps { get; init; }
}

#endregion

#region TensorFlowRTOfflineConnector

/// <summary>
/// Offline connector for TensorFlow Runtime (TFRT) profiler data.
/// Converts TFRT exports to RuntimeRunTrace for before/after comparison.
/// </summary>
public sealed partial class TensorFlowRTOfflineConnector : IRunConnector
{
    /// <summary>Connector identifier.</summary>
    public const string Id = "tensorflowrt-offline";
    
    /// <summary>Connector version.</summary>
    public const string Version = "1.0.0";
    
    #region IRunConnector Implementation
    
    /// <inheritdoc />
    public string ConnectorId => Id;
    
    /// <inheritdoc />
    public string DisplayName => "TensorFlow-TRT Offline";
    
    /// <inheritdoc />
    public string Description => "Import TensorFlow-TensorRT profiler traces, CSVs, and logs for before/after comparison.";
    
    /// <inheritdoc />
    public ConnectorSourceType SourceType => ConnectorSourceType.LogDirectory;
    
    /// <inheritdoc />
    public IReadOnlyList<string> FileExtensions => [".json", ".json.gz", ".csv", ".log"];
    
    /// <inheritdoc />
    public ConnectorCapabilities Capabilities => 
        ConnectorCapabilities.Scalars | 
        ConnectorCapabilities.Milestones | 
        ConnectorCapabilities.WallClock |
        ConnectorCapabilities.Fingerprints;
    
    /// <inheritdoc />
    public async Task<ConnectorProbeResult> ProbeAsync(string source, CancellationToken ct = default)
    {
        var detectedSources = await DetectSourcesAsync(source, ct);
        
        if (detectedSources.Count == 0)
        {
            return ConnectorProbeResult.CannotHandle("No TFRT exports found");
        }
        
        var best = detectedSources.OrderByDescending(s => s.Priority).First();
        
        return ConnectorProbeResult.Success(
            confidence: best.Type == TfrtSourceType.ProfilerTrace ? 0.9 : 0.7,
            type: ConnectorSourceType.LogDirectory,
            capabilities: Capabilities,
            description: $"Found {best.Type} at {best.Path}"
        );
    }
    
    /// <inheritdoc />
    public async Task<RunTrace> ImportAsync(string source, ConnectorOptions options, CancellationToken ct = default)
    {
        // Step 1: Detect sources
        var detectedSources = await DetectSourcesAsync(source, ct);
        if (detectedSources.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_NO_SUPPORTED_EXPORT}] No supported TFRT export found at: {source}");
        }
        
        // Step 2: Parse best source
        var best = detectedSources.OrderByDescending(s => s.Priority).First();
        var rawData = await ParseSourceAsync(best, ct);
        
        // Step 3: Build RuntimeRunTrace
        var runtime = await BuildRuntimeTraceAsync(rawData, source, ct);
        
        // Step 4: Convert to standard RunTrace for ScalarScope
        return ConvertToRunTrace(runtime, options);
    }
    
    /// <inheritdoc />
    public IAsyncEnumerable<RunTraceUpdate> StreamAsync(string source, ConnectorOptions options, CancellationToken ct = default)
    {
        // TFRT offline connector does not support streaming
        throw new NotSupportedException("TFRT offline connector does not support streaming. Use ImportAsync.");
    }
    
    #endregion
    
    #region Source Detection
    
    /// <summary>
    /// Detect available TFRT sources at the given path.
    /// Scans recursively for the folder structure contract.
    /// Priority: profiler/trace.json > profiler/overview.json > benchmark.csv > benchmark.json > runtime.log
    /// </summary>
    private async Task<List<TfrtSource>> DetectSourcesAsync(string source, CancellationToken ct)
    {
        var sources = new List<TfrtSource>();
        
        // Handle file vs directory
        var isDirectory = Directory.Exists(source);
        var isFile = File.Exists(source);
        
        if (!isDirectory && !isFile)
            return sources;
        
        if (isFile)
        {
            var fileSource = ClassifyFile(source, null);
            if (fileSource != null)
                sources.Add(fileSource);
            return sources;
        }
        
        // Detect folder context (saved_model, config.json)
        var context = await DetectFolderContextAsync(source, ct);
        
        // Search directory for TFRT exports
        await Task.Run(() =>
        {
            // Priority 1 (100): profiler/trace.json (highest fidelity)
            var profilerDir = Path.Combine(source, "profiler");
            if (Directory.Exists(profilerDir))
            {
                var traceFile = Path.Combine(profilerDir, "trace.json");
                var traceGz = Path.Combine(profilerDir, "trace.json.gz");
                
                if (File.Exists(traceFile))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.ProfilerTrace, Path = traceFile, Priority = 100, Context = context });
                else if (File.Exists(traceGz))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.ProfilerTrace, Path = traceGz, Priority = 100, Context = context });
                
                // Priority 2 (90): profiler/overview.json
                var overviewFile = Path.Combine(profilerDir, "overview.json");
                if (File.Exists(overviewFile))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.ProfilerOverview, Path = overviewFile, Priority = 90, Context = context });
            }
            
            // Also search for trace.json recursively (fallback)
            foreach (var file in Directory.EnumerateFiles(source, "trace.json*", SearchOption.AllDirectories).Take(5))
            {
                if (!sources.Any(s => s.Path == file))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.ProfilerTrace, Path = file, Priority = 95, Context = context });
            }
            
            // Priority 3 (50): benchmark.csv
            var benchmarkCsv = Path.Combine(source, "benchmark.csv");
            if (File.Exists(benchmarkCsv) && IsTfrtCsv(benchmarkCsv))
                sources.Add(new TfrtSource { Type = TfrtSourceType.BenchmarkCsv, Path = benchmarkCsv, Priority = 50, Context = context });
            
            // Also search for any CSV with TFRT metrics
            foreach (var file in Directory.EnumerateFiles(source, "*.csv", SearchOption.AllDirectories).Take(10))
            {
                if (!sources.Any(s => s.Path == file) && IsTfrtCsv(file))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.BenchmarkCsv, Path = file, Priority = 45, Context = context });
            }
            
            // Priority 4 (40): benchmark.json
            var benchmarkJson = Path.Combine(source, "benchmark.json");
            if (File.Exists(benchmarkJson))
                sources.Add(new TfrtSource { Type = TfrtSourceType.BenchmarkJson, Path = benchmarkJson, Priority = 40, Context = context });
            
            // Priority 5 (10): runtime.log (last resort)
            var runtimeLog = Path.Combine(source, "runtime.log");
            if (File.Exists(runtimeLog) && IsTfrtLog(runtimeLog))
                sources.Add(new TfrtSource { Type = TfrtSourceType.RuntimeLog, Path = runtimeLog, Priority = 10, Context = context });
            
            // Also search for any log with TFRT patterns
            foreach (var file in Directory.EnumerateFiles(source, "*.log", SearchOption.AllDirectories).Take(10))
            {
                if (!sources.Any(s => s.Path == file) && IsTfrtLog(file))
                    sources.Add(new TfrtSource { Type = TfrtSourceType.RuntimeLog, Path = file, Priority = 5, Context = context });
            }
        }, ct);
        
        return sources;
    }
    
    /// <summary>
    /// Detect folder context (saved_model, config.json, warmup_steps).
    /// </summary>
    private static async Task<TfrtFolderContext?> DetectFolderContextAsync(string source, CancellationToken ct)
    {
        string? savedModelPath = null;
        string? configPath = null;
        int? warmupSteps = null;
        
        await Task.Run(() =>
        {
            // Look for saved_model directory
            var savedModelDir = Path.Combine(source, "saved_model");
            if (Directory.Exists(savedModelDir))
                savedModelPath = savedModelDir;
            
            // Look for config.json
            var configFile = Path.Combine(source, "config.json");
            if (File.Exists(configFile))
            {
                configPath = configFile;
                
                // Try to extract warmup_steps
                try
                {
                    var configText = File.ReadAllText(configFile);
                    using var doc = JsonDocument.Parse(configText);
                    
                    if (doc.RootElement.TryGetProperty("warmup_steps", out var warmupProp) ||
                        doc.RootElement.TryGetProperty("warmup_iterations", out warmupProp))
                    {
                        warmupSteps = warmupProp.GetInt32();
                    }
                }
                catch { }
            }
        }, ct);
        
        if (savedModelPath == null && configPath == null && warmupSteps == null)
            return null;
        
        return new TfrtFolderContext
        {
            SavedModelPath = savedModelPath,
            ConfigPath = configPath,
            WarmupSteps = warmupSteps
        };
    }
    
    private TfrtSource? ClassifyFile(string path, TfrtFolderContext? context)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        
        if (name.StartsWith("trace.json"))
            return new TfrtSource { Type = TfrtSourceType.ProfilerTrace, Path = path, Priority = 100, Context = context };
        
        if (name == "overview.json")
            return new TfrtSource { Type = TfrtSourceType.ProfilerOverview, Path = path, Priority = 90, Context = context };
        
        if (name == "benchmark.csv" || (name.EndsWith(".csv") && IsTfrtCsv(path)))
            return new TfrtSource { Type = TfrtSourceType.BenchmarkCsv, Path = path, Priority = 50, Context = context };
        
        if (name == "benchmark.json" && name.EndsWith(".json"))
            return new TfrtSource { Type = TfrtSourceType.BenchmarkJson, Path = path, Priority = 40, Context = context };
        
        if (name.EndsWith(".log") && IsTfrtLog(path))
            return new TfrtSource { Type = TfrtSourceType.RuntimeLog, Path = path, Priority = 10, Context = context };
        
        return null;
    }
    
    private static bool IsTfrtCsv(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            var header = reader.ReadLine();
            if (header == null) return false;
            
            // Required: latency_ms OR equivalent
            // Optional: throughput, memory_mb, iteration/step
            var headerLower = header.ToLowerInvariant();
            return headerLower.Contains("latency") ||
                   headerLower.Contains("throughput") ||
                   headerLower.Contains("memory");
        }
        catch
        {
            return false;
        }
    }
    
    private static bool IsTfrtLog(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            for (int i = 0; i < 20; i++)
            {
                var line = reader.ReadLine();
                if (line == null) break;
                
                // Look for TFRT-specific patterns
                if (line.Contains("TensorRT", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("latency_ms", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("TF-TRT", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("batch size", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        
        return false;
    }
    
    #endregion
    
    #region Source Parsing
    
    private async Task<TfrtRawData> ParseSourceAsync(TfrtSource source, CancellationToken ct)
    {
        var raw = source.Type switch
        {
            TfrtSourceType.ProfilerTrace => await ParseProfilerTraceAsync(source.Path, ct),
            TfrtSourceType.ProfilerOverview => await ParseProfilerOverviewAsync(source.Path, ct),
            TfrtSourceType.BenchmarkCsv => await ParseCsvExportAsync(source.Path, ct),
            TfrtSourceType.BenchmarkJson => await ParseBenchmarkJsonAsync(source.Path, ct),
            TfrtSourceType.RuntimeLog => await ParseLogFileAsync(source.Path, ct),
            _ => throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_NO_SUPPORTED_EXPORT}] Unknown source type: {source.Type}")
        };
        
        // Set context from folder structure
        raw.WarmupStepsFromConfig = source.Context?.WarmupSteps;
        raw.SavedModelPath = source.Context?.SavedModelPath;
        
        // Validate: must have latency signal
        if (raw.LatencyMs.Count == 0)
        {
            // For logs, this is a warning → expect lower quality
            if (source.Type == TfrtSourceType.RuntimeLog)
            {
                // Log-only: expect warnings in validation
            }
            else
            {
                throw new InvalidOperationException(
                    $"[{TfrtErrorCodes.TFRT_NO_LATENCY_SIGNAL}] Cannot extract latency signal from {source.Type}");
            }
        }
        
        // Validate: series lengths must match
        var stepCount = raw.Steps.Count;
        if (raw.LatencyMs.Count > 0 && raw.LatencyMs.Count != stepCount)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_INCONSISTENT_LENGTHS}] Latency length ({raw.LatencyMs.Count}) != steps ({stepCount})");
        }
        
        return raw;
    }
    
    /// <summary>
    /// Parse TensorBoard profiler trace.
    /// </summary>
    private async Task<TfrtRawData> ParseProfilerTraceAsync(string path, CancellationToken ct)
    {
        var raw = new TfrtRawData { SourcePath = path, SourceType = TfrtSourceType.ProfilerTrace };
        
        try
        {
            // Read potentially gzipped trace
            Stream stream;
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            {
                var compressed = await File.ReadAllBytesAsync(path, ct);
                stream = new System.IO.Compression.GZipStream(
                    new MemoryStream(compressed), 
                    System.IO.Compression.CompressionMode.Decompress);
            }
            else
            {
                stream = File.OpenRead(path);
            }
            
            await using (stream)
            {
                var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                ParseTraceEvents(json.RootElement, raw);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_PROFILE_PARSE_FAILED}] Failed to parse profiler trace: {ex.Message}", ex);
        }
        
        return raw;
    }
    
    private static void ParseTraceEvents(JsonElement root, TfrtRawData raw)
    {
        // Chrome trace format: { "traceEvents": [...] }
        if (!root.TryGetProperty("traceEvents", out var events))
            return;
        
        int step = 0;
        foreach (var evt in events.EnumerateArray())
        {
            if (!evt.TryGetProperty("name", out var nameProp))
                continue;
            
            var name = nameProp.GetString() ?? "";
            
            // Extract timing events
            if (name.Contains("TensorRT", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("inference", StringComparison.OrdinalIgnoreCase))
            {
                if (evt.TryGetProperty("dur", out var dur))
                {
                    var durationUs = dur.GetDouble();
                    raw.LatencyMs.Add(durationUs / 1000.0); // Convert us to ms
                }
                
                if (evt.TryGetProperty("ts", out var ts))
                {
                    var timestampUs = ts.GetDouble();
                    raw.WallTimeSeconds.Add(timestampUs / 1_000_000.0); // Convert us to s
                }
                
                raw.Steps.Add(step++);
            }
            
            // Extract memory events
            if (name.Contains("memory", StringComparison.OrdinalIgnoreCase) &&
                evt.TryGetProperty("args", out var args))
            {
                if (args.TryGetProperty("bytes", out var bytes))
                {
                    raw.MemoryBytes.Add(bytes.GetInt64());
                }
            }
        }
    }
    
    /// <summary>
    /// Parse profiler overview.json file.
    /// </summary>
    private async Task<TfrtRawData> ParseProfilerOverviewAsync(string path, CancellationToken ct)
    {
        var raw = new TfrtRawData { SourcePath = path, SourceType = TfrtSourceType.ProfilerOverview };
        
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Overview typically has summary stats, not per-iteration
            // Try to extract available metrics
            int step = 0;
            
            if (root.TryGetProperty("inference_stats", out var stats) ||
                root.TryGetProperty("run_stats", out stats))
            {
                if (stats.TryGetProperty("avg_latency_ms", out var avgLat))
                {
                    raw.LatencyMs.Add(avgLat.GetDouble());
                    raw.Steps.Add(step++);
                }
                
                if (stats.TryGetProperty("throughput", out var thr))
                {
                    raw.ThroughputItemsPerSec.Add(thr.GetDouble());
                }
                
                if (stats.TryGetProperty("peak_memory_bytes", out var mem))
                {
                    raw.MemoryBytes.Add(mem.GetInt64());
                }
            }
            
            // Try to get per-iteration data if available
            if (root.TryGetProperty("iterations", out var iterations))
            {
                foreach (var iter in iterations.EnumerateArray())
                {
                    if (iter.TryGetProperty("latency_ms", out var lat))
                    {
                        raw.LatencyMs.Add(lat.GetDouble());
                        raw.Steps.Add(raw.Steps.Count);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_PARSE_FAILED}] Failed to parse overview: {ex.Message}", ex);
        }
        
        return raw;
    }
    
    /// <summary>
    /// Parse benchmark.json file.
    /// </summary>
    private async Task<TfrtRawData> ParseBenchmarkJsonAsync(string path, CancellationToken ct)
    {
        var raw = new TfrtRawData { SourcePath = path, SourceType = TfrtSourceType.BenchmarkJson };
        
        try
        {
            var json = await File.ReadAllTextAsync(path, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Look for iterations/results array
            JsonElement results;
            if (root.TryGetProperty("results", out results) ||
                root.TryGetProperty("iterations", out results) ||
                root.TryGetProperty("benchmarks", out results))
            {
                int step = 0;
                foreach (var item in results.EnumerateArray())
                {
                    // Latency
                    if (item.TryGetProperty("latency_ms", out var lat) ||
                        item.TryGetProperty("latency", out lat))
                    {
                        raw.LatencyMs.Add(lat.GetDouble());
                    }
                    
                    // Throughput
                    if (item.TryGetProperty("throughput", out var thr) ||
                        item.TryGetProperty("items_per_sec", out thr))
                    {
                        raw.ThroughputItemsPerSec.Add(thr.GetDouble());
                    }
                    
                    // Memory
                    if (item.TryGetProperty("memory_mb", out var memMb))
                    {
                        raw.MemoryBytes.Add((long)(memMb.GetDouble() * 1_000_000));
                    }
                    else if (item.TryGetProperty("memory_bytes", out var mem))
                    {
                        raw.MemoryBytes.Add(mem.GetInt64());
                    }
                    
                    // Step
                    if (item.TryGetProperty("step", out var stepVal) ||
                        item.TryGetProperty("iteration", out stepVal))
                    {
                        raw.Steps.Add(stepVal.GetInt32());
                    }
                    else
                    {
                        raw.Steps.Add(step);
                    }
                    step++;
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_PARSE_FAILED}] Failed to parse benchmark JSON: {ex.Message}", ex);
        }
        
        return raw;
    }
    
    /// <summary>
    /// Parse CSV export.
    /// </summary>
    private async Task<TfrtRawData> ParseCsvExportAsync(string path, CancellationToken ct)
    {
        var raw = new TfrtRawData { SourcePath = path, SourceType = TfrtSourceType.BenchmarkCsv };
        
        try
        {
            var lines = await File.ReadAllLinesAsync(path, ct);
            if (lines.Length < 2)
            {
                throw new InvalidOperationException(
                    $"[{TfrtErrorCodes.TFRT_CSV_FORMAT_ERROR}] CSV file has no data rows");
            }
            
            // Parse header
            var header = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
            var stepIdx = FindColumnIndex(header, "step", "iteration", "batch");
            var latencyIdx = FindColumnIndex(header, "latency_ms", "latency", "time_ms");
            var throughputIdx = FindColumnIndex(header, "throughput", "items_per_sec", "samples_per_sec");
            var memoryIdx = FindColumnIndex(header, "memory_bytes", "memory_mb", "memory");
            var wallTimeIdx = FindColumnIndex(header, "wall_time", "timestamp", "time_sec");
            
            // Parse data rows
            int autoStep = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                
                var values = lines[i].Split(',');
                
                // Step
                if (stepIdx >= 0 && stepIdx < values.Length &&
                    int.TryParse(values[stepIdx], out var step))
                {
                    raw.Steps.Add(step);
                }
                else
                {
                    raw.Steps.Add(autoStep);
                }
                autoStep++;
                
                // Latency
                if (latencyIdx >= 0 && latencyIdx < values.Length &&
                    double.TryParse(values[latencyIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                {
                    raw.LatencyMs.Add(lat);
                }
                
                // Throughput
                if (throughputIdx >= 0 && throughputIdx < values.Length &&
                    double.TryParse(values[throughputIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out var thr))
                {
                    raw.ThroughputItemsPerSec.Add(thr);
                }
                
                // Memory
                if (memoryIdx >= 0 && memoryIdx < values.Length &&
                    long.TryParse(values[memoryIdx], out var mem))
                {
                    // Normalize to bytes if needed
                    if (header[memoryIdx].Contains("mb"))
                        mem *= 1_000_000;
                    raw.MemoryBytes.Add(mem);
                }
                
                // Wall time
                if (wallTimeIdx >= 0 && wallTimeIdx < values.Length &&
                    double.TryParse(values[wallTimeIdx], NumberStyles.Float, CultureInfo.InvariantCulture, out var wt))
                {
                    raw.WallTimeSeconds.Add(wt);
                }
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_CSV_FORMAT_ERROR}] Failed to parse CSV: {ex.Message}", ex);
        }
        
        return raw;
    }
    
    private static int FindColumnIndex(List<string> header, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var idx = header.FindIndex(h => h.Contains(candidate, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                return idx;
        }
        return -1;
    }
    
    /// <summary>
    /// Parse log file (last resort - expect warnings).
    /// </summary>
    private async Task<TfrtRawData> ParseLogFileAsync(string path, CancellationToken ct)
    {
        var raw = new TfrtRawData { SourcePath = path, SourceType = TfrtSourceType.RuntimeLog };
        
        // Regex patterns for common TFRT log formats
        var latencyRegex = LatencyPattern();
        var throughputRegex = ThroughputPattern();
        var memoryRegex = MemoryPattern();
        var stepRegex = StepPattern();
        
        var lines = await File.ReadAllLinesAsync(path, ct);
        int autoStep = 0;
        
        foreach (var line in lines)
        {
            // Try to extract step
            var stepMatch = stepRegex.Match(line);
            if (stepMatch.Success && int.TryParse(stepMatch.Groups[1].Value, out var step))
            {
                raw.Steps.Add(step);
            }
            
            // Extract latency
            var latMatch = latencyRegex.Match(line);
            if (latMatch.Success && double.TryParse(latMatch.Groups[1].Value, NumberStyles.Float, 
                CultureInfo.InvariantCulture, out var lat))
            {
                raw.LatencyMs.Add(lat);
                if (!stepMatch.Success)
                    raw.Steps.Add(autoStep++);
            }
            
            // Extract throughput
            var thrMatch = throughputRegex.Match(line);
            if (thrMatch.Success && double.TryParse(thrMatch.Groups[1].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var thr))
            {
                raw.ThroughputItemsPerSec.Add(thr);
            }
            
            // Extract memory
            var memMatch = memoryRegex.Match(line);
            if (memMatch.Success && long.TryParse(memMatch.Groups[1].Value, out var mem))
            {
                raw.MemoryBytes.Add(mem);
            }
        }
        
        return raw;
    }
    
    [GeneratedRegex(@"latency[_\s]*[:=]?\s*([\d.]+)\s*(?:ms)?", RegexOptions.IgnoreCase)]
    private static partial Regex LatencyPattern();
    
    [GeneratedRegex(@"throughput[_\s]*[:=]?\s*([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ThroughputPattern();
    
    [GeneratedRegex(@"memory[_\s]*[:=]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex MemoryPattern();
    
    [GeneratedRegex(@"step[_\s]*[:=]?\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex StepPattern();
    
    #endregion
    
    #region Runtime Trace Building
    
    private async Task<RuntimeRunTrace> BuildRuntimeTraceAsync(TfrtRawData raw, string source, CancellationToken ct)
    {
        // Validate timeline
        if (raw.Steps.Count == 0)
        {
            throw new InvalidOperationException(
                $"[{TfrtErrorCodes.TFRT_TIMELINE_INCONSISTENT}] No timeline data extracted from source");
        }
        
        // Ensure steps are monotonic
        for (int i = 1; i < raw.Steps.Count; i++)
        {
            if (raw.Steps[i] < raw.Steps[i - 1])
            {
                throw new InvalidOperationException(
                    $"[{TfrtErrorCodes.TFRT_TIMELINE_INCONSISTENT}] Non-monotonic step at index {i}: {raw.Steps[i]} < {raw.Steps[i - 1]}");
            }
        }
        
        // Build scalar series
        var series = new List<RuntimeScalarSeries>();
        
        if (raw.LatencyMs.Count > 0)
        {
            series.Add(new RuntimeScalarSeries
            {
                Name = "latency_ms",
                Unit = ScalarUnit.Milliseconds,
                Values = PadToStepCount(raw.LatencyMs, raw.Steps.Count),
                Description = "Inference latency per step",
                SourceKey = "latency_ms"
            });
        }
        
        if (raw.ThroughputItemsPerSec.Count > 0)
        {
            series.Add(new RuntimeScalarSeries
            {
                Name = "throughput_items_per_sec",
                Unit = ScalarUnit.ItemsPerSecond,
                Values = PadToStepCount(raw.ThroughputItemsPerSec, raw.Steps.Count),
                Description = "Inference throughput",
                SourceKey = "throughput"
            });
        }
        
        if (raw.MemoryBytes.Count > 0)
        {
            series.Add(new RuntimeScalarSeries
            {
                Name = "memory_bytes",
                Unit = ScalarUnit.Bytes,
                Values = PadToStepCount(raw.MemoryBytes.Select(m => (double)m).ToList(), raw.Steps.Count),
                Description = "Memory usage",
                SourceKey = "memory"
            });
        }
        
        var scalars = new RuntimeScalars { Series = series };
        
        // Detect milestones
        var milestones = DetectMilestones(raw, scalars);
        
        // Generate fingerprints
        var metadata = await GenerateMetadataAsync(raw, source, ct);
        
        // Build capabilities
        var capabilities = RuntimeCapabilities.Detect(scalars, raw.SourceType == TfrtSourceType.ProfilerTrace);
        
        return new RuntimeRunTrace
        {
            SchemaVersion = RuntimeRunTrace.CurrentSchemaVersion,
            RunId = Guid.NewGuid().ToString("N"),
            RunType = RunType.Inference,
            Framework = FrameworkType.TensorFlowRT,
            CreatedUtc = DateTimeOffset.UtcNow,
            Label = Path.GetFileNameWithoutExtension(source),
            Metadata = metadata,
            Timeline = new RuntimeTimeline
            {
                Steps = raw.Steps,
                WallTimeSeconds = raw.WallTimeSeconds.Count > 0 ? raw.WallTimeSeconds : null,
                Epoch = null // Not applicable to inference
            },
            Scalars = scalars,
            Milestones = milestones,
            Capabilities = capabilities,
            Provenance = new RuntimeProvenance
            {
                Source = source,
                ConnectorId = ConnectorId,
                ConnectorVersion = Version,
                IngestedUtc = DateTimeOffset.UtcNow
            }
        };
    }
    
    private static IReadOnlyList<double?> PadToStepCount(List<double> values, int stepCount)
    {
        var result = new double?[stepCount];
        for (int i = 0; i < Math.Min(values.Count, stepCount); i++)
        {
            result[i] = values[i];
        }
        return result;
    }
    
    private static RuntimeMilestones DetectMilestones(TfrtRawData raw, RuntimeScalars scalars)
    {
        var milestones = new List<RuntimeMilestone>();
        
        int? warmupEnd = null;
        
        // Priority 1: explicit warmup_steps from config.json
        if (raw.WarmupStepsFromConfig.HasValue)
        {
            warmupEnd = raw.WarmupStepsFromConfig.Value;
            milestones.Add(new RuntimeMilestone
            {
                Type = RuntimeMilestoneType.WarmupEnd,
                Step = warmupEnd.Value,
                Label = "Warmup Complete (from config)"
            });
        }
        else
        {
            // Priority 2: auto-detect from latency stabilization heuristic
            var latencySeries = scalars.GetByName("latency_ms");
            if (latencySeries != null)
            {
                warmupEnd = SteadyStateDetector.DetectWarmupEnd(latencySeries.Values);
                if (warmupEnd.HasValue)
                {
                    milestones.Add(new RuntimeMilestone
                    {
                        Type = RuntimeMilestoneType.WarmupEnd,
                        Step = warmupEnd.Value,
                        Label = "Warmup Complete"
                    });
                }
            }
        }
        
        // Detect steady state if we have warmup end
        if (warmupEnd.HasValue)
        {
            var latencySeries = scalars.GetByName("latency_ms");
            if (latencySeries != null)
            {
                var steadyState = SteadyStateDetector.DetectSteadyState(
                    latencySeries.Values, warmupEnd.Value);
                
                if (steadyState.HasValue)
                {
                    milestones.Add(new RuntimeMilestone
                    {
                        Type = RuntimeMilestoneType.SteadyStateStart,
                        Step = steadyState.Value.Start,
                        Label = "Steady State Begin"
                    });
                    
                    milestones.Add(new RuntimeMilestone
                    {
                        Type = RuntimeMilestoneType.SteadyStateEnd,
                        Step = steadyState.Value.End,
                        Label = "Steady State End"
                    });
                }
            }
        }
        
        return new RuntimeMilestones { List = milestones };
    }
    
    private async Task<RuntimeMetadata> GenerateMetadataAsync(TfrtRawData raw, string source, CancellationToken ct)
    {
        // Try to find SavedModel for model fingerprint
        var modelFingerprint = await ComputeModelFingerprintAsync(source, ct);
        
        // Environment fingerprint from system info
        var envFingerprint = ComputeEnvironmentFingerprint();
        
        // Code fingerprint (placeholder - would need git integration)
        var codeFingerprint = RuntimeMetadata.UnknownFingerprint("code");
        
        // Dataset fingerprint (placeholder - not available from TFRT exports)
        var datasetFingerprint = RuntimeMetadata.UnknownFingerprint("dataset");
        
        return new RuntimeMetadata
        {
            ModelFingerprint = modelFingerprint,
            DatasetFingerprint = datasetFingerprint,
            CodeFingerprint = codeFingerprint,
            EnvironmentFingerprint = envFingerprint,
            Tags = ["tensorflowrt", "inference"],
            FrameworkDetails = new Dictionary<string, object>
            {
                ["sourceType"] = raw.SourceType.ToString(),
                ["sourcePath"] = raw.SourcePath
            }
        };
    }
    
    private async Task<string> ComputeModelFingerprintAsync(string source, CancellationToken ct)
    {
        try
        {
            // Look for SavedModel in source directory
            var searchDir = File.Exists(source) ? Path.GetDirectoryName(source) : source;
            if (searchDir == null)
                return RuntimeMetadata.UnknownFingerprint("model");
            
            // Check for saved_model.pb
            var savedModelPath = Path.Combine(searchDir, "saved_model.pb");
            if (!File.Exists(savedModelPath))
            {
                // Search up to 2 levels up
                var parent = Path.GetDirectoryName(searchDir);
                if (parent != null)
                {
                    savedModelPath = Path.Combine(parent, "saved_model.pb");
                }
            }
            
            if (File.Exists(savedModelPath))
            {
                var bytes = await File.ReadAllBytesAsync(savedModelPath, ct);
                var hash = SHA256.HashData(bytes);
                return Convert.ToHexString(hash).ToLowerInvariant();
            }
            
            return RuntimeMetadata.UnknownFingerprint("model");
        }
        catch
        {
            return RuntimeMetadata.UnknownFingerprint("model");
        }
    }
    
    private static string ComputeEnvironmentFingerprint()
    {
        var sb = new StringBuilder();
        sb.Append(Environment.OSVersion.Platform);
        sb.Append(Environment.OSVersion.Version);
        sb.Append(Environment.ProcessorCount);
        sb.Append(Environment.Is64BitOperatingSystem);
        
        return RuntimeMetadata.CreateFingerprint(sb.ToString());
    }
    
    #endregion
    
    #region RunTrace Conversion
    
    /// <summary>
    /// Convert RuntimeRunTrace to standard RunTrace.
    /// </summary>
    private static RunTrace ConvertToRunTrace(RuntimeRunTrace runtime, ConnectorOptions? options)
    {
        // Map runtime scalars to RunTrace scalars
        var scalars = new Dictionary<string, ScalarSeries>();
        var steps = runtime.Timeline.Steps.ToList();
        
        foreach (var series in runtime.Scalars.Series)
        {
            var mapped = MapScalarName(series.Name);
            var values = series.Values.Select(v => v ?? double.NaN).ToList();
            scalars[mapped] = new ScalarSeries
            {
                Steps = steps,
                Values = values,
                WallClockSeconds = runtime.Timeline.WallTimeSeconds?.ToList()
            };
        }
        
        // Map milestones
        var milestones = runtime.Milestones.List.Select(m => new Milestone
        {
            Step = m.Step,
            Type = MapMilestoneType(m.Type),
            Label = m.Label
        }).ToList();
        
        return new RunTrace
        {
            TraceVersion = RunTrace.CurrentVersion,
            Capabilities = ConnectorCapabilities.Scalars | ConnectorCapabilities.Milestones | ConnectorCapabilities.WallClock,
            Metadata = new RunTraceMetadata
            {
                RunId = runtime.RunId,
                Label = runtime.Label ?? "TensorFlowRT Run",
                Source = ConnectorSourceType.LogDirectory,
                SourcePath = runtime.Provenance?.Source,
                CreatedUtc = runtime.CreatedUtc,
                Tags = runtime.Metadata.Tags?.ToDictionary(t => t, _ => "true"),
                Fingerprints = new RunFingerprints
                {
                    Model = runtime.Metadata.ModelFingerprint,
                    Dataset = runtime.Metadata.DatasetFingerprint,
                    Code = runtime.Metadata.CodeFingerprint,
                    Environment = runtime.Metadata.EnvironmentFingerprint
                }
            },
            Timeline = new RunTraceTimeline
            {
                StepCount = runtime.Timeline.StepCount,
                StepUnit = StepUnit.Iteration,
                WallClockPerStep = runtime.Timeline.WallTimeSeconds?.ToList()
            },
            Scalars = scalars,
            Milestones = milestones
        };
    }
    
    private static string MapScalarName(string name)
    {
        return name switch
        {
            "latency_ms" => "latency",
            "throughput_items_per_sec" => "throughput",
            "memory_bytes" => "memory",
            _ => name
        };
    }
    
    private static MilestoneType MapMilestoneType(RuntimeMilestoneType type)
    {
        return type switch
        {
            RuntimeMilestoneType.EpochStart or RuntimeMilestoneType.EpochEnd => MilestoneType.Epoch,
            RuntimeMilestoneType.Eval => MilestoneType.Eval,
            RuntimeMilestoneType.Checkpoint => MilestoneType.Checkpoint,
            RuntimeMilestoneType.WarmupEnd or RuntimeMilestoneType.SteadyStateStart or 
            RuntimeMilestoneType.SteadyStateEnd => MilestoneType.Custom,
            _ => MilestoneType.Custom
        };
    }
    
    #endregion
}

#endregion

#region Internal Types

/// <summary>
/// Raw data extracted from TFRT sources before transformation.
/// </summary>
internal sealed class TfrtRawData
{
    public required string SourcePath { get; init; }
    public required TfrtSourceType SourceType { get; init; }
    
    public List<int> Steps { get; } = [];
    public List<double> LatencyMs { get; } = [];
    public List<double> ThroughputItemsPerSec { get; } = [];
    public List<long> MemoryBytes { get; } = [];
    public List<double> WallTimeSeconds { get; } = [];
    
    // CPU/GPU load (optional)
    public List<double> CpuPercent { get; } = [];
    public List<double> GpuPercent { get; } = [];
    
    // Folder context
    public int? WarmupStepsFromConfig { get; set; }
    public string? SavedModelPath { get; set; }
}

#endregion

#region Registration Extension

/// <summary>
/// Extension to register TFRT connector.
/// </summary>
public static class TensorFlowRTConnectorExtensions
{
    /// <summary>
    /// Register the TensorFlowRT offline connector.
    /// </summary>
    public static ConnectorRegistry RegisterTensorFlowRT(this ConnectorRegistry registry)
    {
        registry.Register(new TensorFlowRTOfflineConnector());
        return registry;
    }
}

#endregion
