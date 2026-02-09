// TensorFlowRT Runtime Preset v1
// Semantic mapping for TensorFlow-TensorRT inference workloads.
// Locks human-readable UI copy and delta signal assignments.

namespace ScalarScope.Services.Connectors;

#region TFRT Runtime Preset

/// <summary>
/// TensorFlowRT runtime v1 preset.
/// Specialized for inference workload comparison (before/after optimization).
/// </summary>
public static class TfrtRuntimePreset
{
    /// <summary>Preset ID.</summary>
    public const string PresetId = "tensorflowrt-runtime-v1";
    
    /// <summary>Preset version.</summary>
    public const string PresetVersion = "1.0.0";
    
    /// <summary>Get the preset definition.</summary>
    public static ConnectorPreset Create() => new()
    {
        PresetId = PresetId,
        PresetVersion = PresetVersion,
        Source = ConnectorSourceType.LogDirectory, // TFRT uses folder/log structure
        DisplayName = "TensorFlow-TRT Runtime",
        Description = "Inference runtime metrics for TensorFlow-TensorRT workloads. " +
                      "Enables before/after optimization comparison with latency, throughput, and memory signals.",
        IsBuiltIn = true,
        Mappings = CreateMappings(),
        MilestoneMapping = CreateMilestoneMappings(),
        Normalization = CreateNormalization()
    };
    
    private static SignalMappings CreateMappings() => new()
    {
        // Primary runtime metrics (not training signals)
        CustomSignals = new Dictionary<string, SignalMapping>
        {
            // Latency: "Time per inference request"
            ["latency"] = new SignalMapping
            {
                Primary = "latency_ms",
                Fallbacks = ["latency", "inference_latency", "avg_latency_ms", "p50_latency_ms"]
            },
            
            // Throughput: "Requests processed per second"
            ["throughput"] = new SignalMapping
            {
                Primary = "throughput_items_per_sec",
                Fallbacks = ["throughput", "items_per_sec", "samples_per_sec", "qps"]
            },
            
            // Memory: "Peak memory usage during inference"
            ["memory"] = new SignalMapping
            {
                Primary = "memory_bytes",
                Fallbacks = ["memory_mb", "peak_memory", "gpu_memory_bytes"],
                Transform = new SignalTransform
                {
                    Type = TransformType.Scale,
                    Scale = 1.0 / (1024 * 1024) // Convert bytes to MB for display
                }
            },
            
            // CPU Load: "CPU utilization"
            ["cpu_load"] = new SignalMapping
            {
                Primary = "cpu_percent",
                Fallbacks = ["cpu_utilization", "cpu_usage"]
            },
            
            // GPU Load: "GPU utilization"
            ["gpu_load"] = new SignalMapping
            {
                Primary = "gpu_percent",
                Fallbacks = ["gpu_utilization", "gpu_usage"]
            }
        }
    };
    
    private static MilestoneMappings CreateMilestoneMappings() => new()
    {
        // Runtime-specific milestone patterns
        Epoch = ["batch", "iteration"],
        Eval = ["warmup_end", "steady_state_start"],
        Checkpoint = ["snapshot"]
    };
    
    private static NormalizationSettings CreateNormalization() => new()
    {
        LossScale = LossScaleMode.None, // Not applicable to runtime metrics
        NormalizeStepStart = true,
        RemoveInvalidValues = true,
        MinDataPoints = 5 // Allow smaller runs for quick benchmarks
    };
}

#endregion

#region TFRT UI Copy

/// <summary>
/// Locked UI copy for TensorFlowRT metrics.
/// Human-readable descriptions that should not change.
/// </summary>
public static class TfrtUICopy
{
    /// <summary>Latency metric display name.</summary>
    public const string LatencyName = "Latency";
    
    /// <summary>Latency metric unit.</summary>
    public const string LatencyUnit = "ms";
    
    /// <summary>Latency metric description.</summary>
    public const string LatencyDescription = "Time per inference request";
    
    /// <summary>Throughput metric display name.</summary>
    public const string ThroughputName = "Throughput";
    
    /// <summary>Throughput metric unit.</summary>
    public const string ThroughputUnit = "items/s";
    
    /// <summary>Throughput metric description.</summary>
    public const string ThroughputDescription = "Requests processed per second";
    
    /// <summary>Memory metric display name.</summary>
    public const string MemoryName = "Memory";
    
    /// <summary>Memory metric unit.</summary>
    public const string MemoryUnit = "MB";
    
    /// <summary>Memory metric description.</summary>
    public const string MemoryDescription = "Peak memory usage during inference";
    
    /// <summary>CPU Load metric display name.</summary>
    public const string CpuLoadName = "CPU Load";
    
    /// <summary>CPU Load metric unit.</summary>
    public const string CpuLoadUnit = "%";
    
    /// <summary>CPU Load metric description.</summary>
    public const string CpuLoadDescription = "CPU utilization";
    
    /// <summary>GPU Load metric display name.</summary>
    public const string GpuLoadName = "GPU Load";
    
    /// <summary>GPU Load metric unit.</summary>
    public const string GpuLoadUnit = "%";
    
    /// <summary>GPU Load metric description.</summary>
    public const string GpuLoadDescription = "GPU utilization";
    
    /// <summary>Stability note for runtime instability indicator.</summary>
    public const string StabilityNote = 
        "Runtime instability indicates fluctuating execution behavior, not correctness issues.";
    
    /// <summary>Warning for missing steady state.</summary>
    public const string NoSteadyStateWarning = "⚠ Steady state not established";
    
    /// <summary>Warning for high warmup ratio.</summary>
    public const string HighWarmupWarning = "⚠ Warmup exceeds 50% of run";
    
    /// <summary>Warning for aggregated-only data.</summary>
    public const string AggregatedOnlyWarning = "⚠ Only aggregated stats available - time-based deltas disabled";
    
    /// <summary>Get UI info for a metric name.</summary>
    public static (string Name, string Unit, string Description) GetMetricInfo(string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "latency" or "latency_ms" => (LatencyName, LatencyUnit, LatencyDescription),
            "throughput" or "throughput_items_per_sec" => (ThroughputName, ThroughputUnit, ThroughputDescription),
            "memory" or "memory_bytes" or "memory_mb" => (MemoryName, MemoryUnit, MemoryDescription),
            "cpu_load" or "cpu_percent" => (CpuLoadName, CpuLoadUnit, CpuLoadDescription),
            "gpu_load" or "gpu_percent" => (GpuLoadName, GpuLoadUnit, GpuLoadDescription),
            _ => (metricName, "", "")
        };
    }
}

#endregion

#region TFRT Delta Mapping

/// <summary>
/// Delta signal mapping for TensorFlowRT runtime.
/// Defines which delta types use which signals.
/// </summary>
public static class TfrtDeltaMapping
{
    /// <summary>
    /// Get the primary signal for a delta type.
    /// </summary>
    /// <param name="deltaType">Delta type (e.g., "ΔTc", "ΔO", "ΔF").</param>
    /// <returns>Signal name to use, or null if suppressed.</returns>
    public static string? GetPrimarySignal(string deltaType)
    {
        return deltaType switch
        {
            "ΔTc" => "latency",       // Stabilization time
            "ΔO" => "latency",        // Oscillation / instability
            "ΔF" => "latency",        // Outliers / runtime failure
            "ΔTd" => null,            // Suppress: training descent not applicable
            "ΔĀ" => null,             // Suppress: accuracy not primary for inference
            _ => null
        };
    }
    
    /// <summary>
    /// Check if a delta type is suppressed for TFRT runtime.
    /// </summary>
    public static bool IsSuppressed(string deltaType)
    {
        return deltaType is "ΔTd" or "ΔĀ";
    }
    
    /// <summary>
    /// Get notes for a delta type.
    /// </summary>
    public static string? GetDeltaNotes(string deltaType)
    {
        return deltaType switch
        {
            "ΔTc" => "Measures time to reach stable latency (stabilization time)",
            "ΔO" => "Measures latency oscillation / instability",
            "ΔF" => "Detects latency outliers indicating runtime failures",
            "ΔTd" => "Suppressed: training descent not applicable to inference",
            "ΔĀ" => "Suppressed: accuracy tracking not primary for runtime comparison",
            _ => null
        };
    }
}

#endregion

#region TFRT Guardrails

/// <summary>
/// Preset-level guardrails for TensorFlowRT runtime analysis.
/// </summary>
public static class TfrtGuardrails
{
    /// <summary>Maximum allowed warmup ratio before warning.</summary>
    public const double MaxWarmupRatio = 0.5;
    
    /// <summary>
    /// Validate a runtime trace and return warnings.
    /// </summary>
    public static IReadOnlyList<string> ValidateForAnalysis(RuntimeRunTrace trace)
    {
        var warnings = new List<string>();
        
        // Check for steady state
        var hasSteadyState = trace.Milestones.List.Any(m => 
            m.Type == RuntimeMilestoneType.SteadyStateStart ||
            m.Type == RuntimeMilestoneType.WarmupEnd);
        
        if (!hasSteadyState)
        {
            warnings.Add(TfrtUICopy.NoSteadyStateWarning);
        }
        
        // Check warmup ratio
        var warmupEnd = trace.Milestones.WarmupEndStep;
        if (warmupEnd.HasValue)
        {
            var totalSteps = trace.Timeline.Steps.Count;
            var warmupRatio = (double)warmupEnd.Value / totalSteps;
            
            if (warmupRatio > MaxWarmupRatio)
            {
                warnings.Add(TfrtUICopy.HighWarmupWarning);
            }
        }
        
        // Check for aggregated-only data
        var hasTimeBasedData = trace.Scalars.Series.Any(s => s.Values.Count > 1);
        if (!hasTimeBasedData)
        {
            warnings.Add(TfrtUICopy.AggregatedOnlyWarning);
        }
        
        return warnings;
    }
    
    /// <summary>
    /// Check if time-based deltas should be enabled.
    /// </summary>
    public static bool ShouldEnableTimeBasedDeltas(RuntimeRunTrace trace)
    {
        // Disable if only aggregated stats
        return trace.Scalars.Series.Any(s => s.Values.Count > 1);
    }
}

#endregion

#region Preset Registration Extension

/// <summary>
/// Extension to register TFRT preset.
/// </summary>
public static class TfrtPresetExtensions
{
    /// <summary>
    /// Register the TensorFlowRT runtime preset.
    /// </summary>
    public static ConnectorPresetService RegisterTfrtPreset(this ConnectorPresetService service)
    {
        service.Register(TfrtRuntimePreset.Create());
        return service;
    }
}

#endregion
