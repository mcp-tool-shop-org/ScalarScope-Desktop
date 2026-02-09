using System.Diagnostics;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.5: Performance profiling service for tracking render times and memory usage.
/// Provides gates for acceptable performance thresholds.
/// </summary>
public static class PerformanceProfiler
{
    private static readonly Lock _lock = new();
    private static readonly Dictionary<string, PerformanceMetrics> _metrics = new();
    
    // Performance gate thresholds
    public static class Gates
    {
        /// <summary>Maximum acceptable frame render time (16.6ms = 60fps)</summary>
        public const double MaxFrameTimeMs = 16.67;
        
        /// <summary>Maximum acceptable delta calculation time</summary>
        public const double MaxDeltaCalcMs = 100.0;
        
        /// <summary>Maximum acceptable export time for still images</summary>
        public const double MaxExportStillMs = 5000.0;
        
        /// <summary>Maximum acceptable memory growth per operation (50MB)</summary>
        public const long MaxMemoryGrowthBytes = 50 * 1024 * 1024;
        
        /// <summary>Warning threshold for GC pressure (Gen2 collections)</summary>
        public const int MaxGen2CollectionsPerMinute = 5;
    }
    
    /// <summary>
    /// Start timing an operation.
    /// </summary>
    public static PerformanceScope BeginScope(string operationName)
    {
        return new PerformanceScope(operationName);
    }
    
    /// <summary>
    /// Record a timing measurement.
    /// </summary>
    public static void RecordTiming(string operationName, double durationMs)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(operationName, out var metrics))
            {
                metrics = new PerformanceMetrics();
                _metrics[operationName] = metrics;
            }
            metrics.RecordTiming(durationMs);
        }
        
        // Check gates
        CheckGates(operationName, durationMs);
    }
    
    /// <summary>
    /// Get metrics for an operation.
    /// </summary>
    public static PerformanceMetrics? GetMetrics(string operationName)
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(operationName, out var metrics) ? metrics : null;
        }
    }
    
    /// <summary>
    /// Get all recorded metrics.
    /// </summary>
    public static Dictionary<string, PerformanceMetrics> GetAllMetrics()
    {
        lock (_lock)
        {
            return new Dictionary<string, PerformanceMetrics>(_metrics);
        }
    }
    
    /// <summary>
    /// Clear all recorded metrics.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _metrics.Clear();
        }
    }
    
    /// <summary>
    /// Get current memory statistics.
    /// </summary>
    public static MemoryStats GetMemoryStats()
    {
        using var process = Process.GetCurrentProcess();
        return new MemoryStats
        {
            WorkingSetMB = process.WorkingSet64 / (1024.0 * 1024.0),
            PrivateMemoryMB = process.PrivateMemorySize64 / (1024.0 * 1024.0),
            GCTotalMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2)
        };
    }
    
    /// <summary>
    /// Generate performance report.
    /// </summary>
    public static string GenerateReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                 Performance Profile Report                     ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        
        var memory = GetMemoryStats();
        sb.AppendLine("## Memory");
        sb.AppendLine($"  Working Set:    {memory.WorkingSetMB:F1} MB");
        sb.AppendLine($"  Private Memory: {memory.PrivateMemoryMB:F1} MB");
        sb.AppendLine($"  GC Heap:        {memory.GCTotalMB:F1} MB");
        sb.AppendLine($"  GC Collections: Gen0={memory.Gen0Collections} Gen1={memory.Gen1Collections} Gen2={memory.Gen2Collections}");
        sb.AppendLine();
        
        var metrics = GetAllMetrics();
        if (metrics.Count > 0)
        {
            sb.AppendLine("## Timing Metrics");
            foreach (var (name, m) in metrics.OrderByDescending(kv => kv.Value.TotalMs))
            {
                var gate = GetGateForOperation(name);
                var status = m.AverageMs <= gate ? "✓" : "⚠";
                sb.AppendLine($"  {name}:");
                sb.AppendLine($"    Count: {m.Count}  Avg: {m.AverageMs:F2}ms  Max: {m.MaxMs:F2}ms  Total: {m.TotalMs:F0}ms {status}");
            }
        }
        
        return sb.ToString();
    }
    
    private static void CheckGates(string operationName, double durationMs)
    {
        var gate = GetGateForOperation(operationName);
        if (durationMs > gate)
        {
            Debug.WriteLine($"[PerfGate] {operationName} exceeded gate: {durationMs:F2}ms > {gate}ms");
        }
    }
    
    private static double GetGateForOperation(string operationName)
    {
        return operationName.ToLowerInvariant() switch
        {
            var n when n.Contains("frame") || n.Contains("render") => Gates.MaxFrameTimeMs,
            var n when n.Contains("delta") || n.Contains("compare") => Gates.MaxDeltaCalcMs,
            var n when n.Contains("export") => Gates.MaxExportStillMs,
            _ => 1000.0 // Default 1 second gate
        };
    }
}

/// <summary>
/// Disposable scope for timing operations.
/// </summary>
public readonly struct PerformanceScope : IDisposable
{
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    
    public PerformanceScope(string operationName)
    {
        _operationName = operationName;
        _stopwatch = Stopwatch.StartNew();
    }
    
    public void Dispose()
    {
        _stopwatch.Stop();
        PerformanceProfiler.RecordTiming(_operationName, _stopwatch.Elapsed.TotalMilliseconds);
    }
}

/// <summary>
/// Aggregated performance metrics for an operation.
/// </summary>
public class PerformanceMetrics
{
    private readonly List<double> _samples = new();
    private const int MaxSamples = 1000;
    
    public int Count => _samples.Count;
    public double TotalMs => _samples.Sum();
    public double AverageMs => _samples.Count > 0 ? _samples.Average() : 0;
    public double MinMs => _samples.Count > 0 ? _samples.Min() : 0;
    public double MaxMs => _samples.Count > 0 ? _samples.Max() : 0;
    public double P95Ms => GetPercentile(95);
    public double P99Ms => GetPercentile(99);
    
    public void RecordTiming(double durationMs)
    {
        _samples.Add(durationMs);
        
        // Keep bounded sample size
        if (_samples.Count > MaxSamples)
        {
            _samples.RemoveAt(0);
        }
    }
    
    private double GetPercentile(int percentile)
    {
        if (_samples.Count == 0) return 0;
        var sorted = _samples.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

/// <summary>
/// Memory statistics snapshot.
/// </summary>
public record MemoryStats
{
    public double WorkingSetMB { get; init; }
    public double PrivateMemoryMB { get; init; }
    public double GCTotalMB { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
}
