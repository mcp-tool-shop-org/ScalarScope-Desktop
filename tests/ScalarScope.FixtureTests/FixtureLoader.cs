// Golden Fixture Loader for RunTrace Testing
// Provides utilities for loading and deserializing test fixtures.

using System.Text.Json;
using System.Text.Json.Serialization;
using ScalarScope.Services.Connectors;

namespace ScalarScope.FixtureTests;

/// <summary>
/// Helper class for loading golden fixtures.
/// </summary>
public static class FixtureLoader
{
    private static readonly string FixturesRoot = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "InferenceOptimization");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Load a RuntimeRunTrace from a fixture file.
    /// </summary>
    public static RuntimeRunTrace LoadRunTrace(string fileName)
    {
        var path = Path.Combine(FixturesRoot, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture not found: {path}");
        }

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<RunTraceDto>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize fixture: {fileName}");
        
        return dto.ToRuntimeRunTrace();
    }

    /// <summary>
    /// Load expected assertions from a fixture file.
    /// </summary>
    public static ExpectedAssertions LoadAssertions(string fileName)
    {
        var path = Path.Combine(FixturesRoot, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Assertions file not found: {path}");
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ExpectedAssertions>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize assertions: {fileName}");
    }

    /// <summary>
    /// Get the path to a fixture file.
    /// </summary>
    public static string GetFixturePath(string fileName)
    {
        return Path.Combine(FixturesRoot, fileName);
    }

    /// <summary>
    /// Check if a fixture file exists.
    /// </summary>
    public static bool FixtureExists(string fileName)
    {
        return File.Exists(Path.Combine(FixturesRoot, fileName));
    }
}

#region DTOs for JSON Deserialization

/// <summary>
/// DTO for RunTrace JSON deserialization.
/// </summary>
public class RunTraceDto
{
    public string SchemaVersion { get; set; } = "1.0.0";
    public string RunId { get; set; } = "";
    public string RunType { get; set; } = "inference";
    public string Framework { get; set; } = "tensorflowrt";
    public DateTimeOffset CreatedUtc { get; set; }
    public string? Label { get; set; }
    public MetadataDto Metadata { get; set; } = new();
    public TimelineDto Timeline { get; set; } = new();
    public ScalarsDto Scalars { get; set; } = new();
    public MilestonesDto Milestones { get; set; } = new();
    public List<ArtifactDto>? Artifacts { get; set; }
    public CapabilitiesDto Capabilities { get; set; } = new();
    public ProvenanceDto? Provenance { get; set; }

    public RuntimeRunTrace ToRuntimeRunTrace()
    {
        return new RuntimeRunTrace
        {
            SchemaVersion = SchemaVersion,
            RunId = RunId,
            RunType = ParseRunType(RunType),
            Framework = ParseFramework(Framework),
            CreatedUtc = CreatedUtc,
            Label = Label,
            Metadata = Metadata.ToRuntimeMetadata(),
            Timeline = Timeline.ToRuntimeTimeline(),
            Scalars = Scalars.ToRuntimeScalars(),
            Milestones = Milestones.ToRuntimeMilestones(),
            Artifacts = Artifacts?.Select(a => a.ToRuntimeArtifact()).ToList(),
            Capabilities = Capabilities.ToRuntimeCapabilities(),
            Provenance = Provenance?.ToRuntimeProvenance()
        };
    }

    private static RunType ParseRunType(string value) => value.ToLowerInvariant() switch
    {
        "training" => ScalarScope.Services.Connectors.RunType.Training,
        "inference" => ScalarScope.Services.Connectors.RunType.Inference,
        "evaluation" => ScalarScope.Services.Connectors.RunType.Evaluation,
        _ => ScalarScope.Services.Connectors.RunType.Inference
    };

    private static FrameworkType ParseFramework(string value) => value.ToLowerInvariant() switch
    {
        "tensorflowrt" => FrameworkType.TensorFlowRT,
        "tensorflow" => FrameworkType.TensorFlow,
        "pytorch" => FrameworkType.PyTorch,
        "jax" => FrameworkType.Jax,
        "mlflow" => FrameworkType.MLflow,
        "wandb" => FrameworkType.WandB,
        "tensorboard" => FrameworkType.TensorBoard,
        _ => FrameworkType.Unknown
    };
}

public class MetadataDto
{
    public string ModelFingerprint { get; set; } = "";
    public string DatasetFingerprint { get; set; } = "";
    public string CodeFingerprint { get; set; } = "";
    public string EnvironmentFingerprint { get; set; } = "";
    public int? Seed { get; set; }
    public List<string>? Tags { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, object>? FrameworkDetails { get; set; }

    public RuntimeMetadata ToRuntimeMetadata() => new()
    {
        ModelFingerprint = ModelFingerprint,
        DatasetFingerprint = DatasetFingerprint,
        CodeFingerprint = CodeFingerprint,
        EnvironmentFingerprint = EnvironmentFingerprint,
        Seed = Seed,
        Tags = Tags,
        Notes = Notes,
        FrameworkDetails = FrameworkDetails
    };
}

public class TimelineDto
{
    public List<int> Steps { get; set; } = new();
    public List<double>? WallTimeSeconds { get; set; }
    public List<int>? Epoch { get; set; }

    public RuntimeTimeline ToRuntimeTimeline() => new()
    {
        Steps = Steps,
        WallTimeSeconds = WallTimeSeconds,
        Epoch = Epoch
    };
}

public class ScalarsDto
{
    public List<ScalarSeriesDto> Series { get; set; } = new();

    public RuntimeScalars ToRuntimeScalars() => new()
    {
        Series = Series.Select(s => s.ToRuntimeScalarSeries()).ToList()
    };
}

public class ScalarSeriesDto
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "none";
    public string? SourceKey { get; set; }
    public string? Aggregation { get; set; }
    public string? Description { get; set; }
    public List<double?> Values { get; set; } = new();

    public RuntimeScalarSeries ToRuntimeScalarSeries() => new()
    {
        Name = Name,
        Unit = ParseUnit(Unit),
        SourceKey = SourceKey,
        Aggregation = ParseAggregation(Aggregation),
        Description = Description,
        Values = Values
    };

    private static ScalarUnit ParseUnit(string value) => value.ToLowerInvariant() switch
    {
        "milliseconds" => ScalarUnit.Milliseconds,
        "seconds" => ScalarUnit.Seconds,
        "microseconds" => ScalarUnit.Microseconds,
        "items_per_second" => ScalarUnit.ItemsPerSecond,
        "bytes" => ScalarUnit.Bytes,
        "megabytes" => ScalarUnit.Megabytes,
        "gigabytes" => ScalarUnit.Gigabytes,
        "percent" => ScalarUnit.Percent,
        "loss" => ScalarUnit.Loss,
        "accuracy" => ScalarUnit.Accuracy,
        "count" => ScalarUnit.Count,
        _ => ScalarUnit.None
    };

    private static ScalarAggregation? ParseAggregation(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.ToLowerInvariant() switch
        {
            "none" => ScalarAggregation.None,
            "mean" => ScalarAggregation.Mean,
            "median" => ScalarAggregation.Median,
            "p50" => ScalarAggregation.P50,
            "p90" => ScalarAggregation.P90,
            "p95" => ScalarAggregation.P95,
            "p99" => ScalarAggregation.P99,
            _ => null
        };
    }
}

public class MilestonesDto
{
    public List<MilestoneDto> List { get; set; } = new();

    public RuntimeMilestones ToRuntimeMilestones() => new()
    {
        List = List.Select(m => m.ToRuntimeMilestone()).ToList()
    };
}

public class MilestoneDto
{
    public string Type { get; set; } = "";
    public int Step { get; set; }
    public string? Label { get; set; }
    public Dictionary<string, object>? Meta { get; set; }

    public RuntimeMilestone ToRuntimeMilestone() => new()
    {
        Type = ParseMilestoneType(Type),
        Step = Step,
        Label = Label,
        Meta = Meta
    };

    private static RuntimeMilestoneType ParseMilestoneType(string value) => value.ToLowerInvariant() switch
    {
        "warmup_end" => RuntimeMilestoneType.WarmupEnd,
        "steady_state_start" => RuntimeMilestoneType.SteadyStateStart,
        "steady_state_end" => RuntimeMilestoneType.SteadyStateEnd,
        "epoch_start" => RuntimeMilestoneType.EpochStart,
        "epoch_end" => RuntimeMilestoneType.EpochEnd,
        "eval" => RuntimeMilestoneType.Eval,
        "checkpoint" => RuntimeMilestoneType.Checkpoint,
        _ => RuntimeMilestoneType.Custom
    };
}

public class ArtifactDto
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public string Sha256 { get; set; } = "";
    public long? Bytes { get; set; }
    public string? ContentType { get; set; }

    public RuntimeArtifact ToRuntimeArtifact() => new()
    {
        Type = ParseArtifactType(Type),
        Name = Name,
        Path = Path,
        Sha256 = Sha256,
        Bytes = Bytes,
        ContentType = ContentType
    };

    private static RuntimeArtifactType ParseArtifactType(string value) => value.ToLowerInvariant() switch
    {
        "model" => RuntimeArtifactType.Model,
        "checkpoint" => RuntimeArtifactType.Checkpoint,
        "log" => RuntimeArtifactType.Log,
        "profile" => RuntimeArtifactType.Profile,
        _ => RuntimeArtifactType.Other
    };
}

public class CapabilitiesDto
{
    public bool HasLoss { get; set; }
    public bool HasAccuracy { get; set; }
    public bool HasLatency { get; set; }
    public bool HasThroughput { get; set; }
    public bool HasMemory { get; set; }
    public bool HasCheckpoints { get; set; }
    public bool HasProfiler { get; set; }
    public bool HasEvaluatorVectors { get; set; }
    public bool HasEigenSpectrum { get; set; }

    public RuntimeCapabilities ToRuntimeCapabilities() => new()
    {
        HasLoss = HasLoss,
        HasAccuracy = HasAccuracy,
        HasLatency = HasLatency,
        HasThroughput = HasThroughput,
        HasMemory = HasMemory,
        HasCheckpoints = HasCheckpoints,
        HasProfiler = HasProfiler,
        HasEvaluatorVectors = HasEvaluatorVectors,
        HasEigenSpectrum = HasEigenSpectrum
    };
}

public class ProvenanceDto
{
    public string Source { get; set; } = "";
    public string? SourceVersion { get; set; }
    public DateTimeOffset? IngestedUtc { get; set; }
    public string? ConnectorId { get; set; }
    public string? ConnectorVersion { get; set; }

    public RuntimeProvenance ToRuntimeProvenance() => new()
    {
        Source = Source,
        SourceVersion = SourceVersion,
        IngestedUtc = IngestedUtc,
        ConnectorId = ConnectorId,
        ConnectorVersion = ConnectorVersion
    };
}

#endregion

#region Expected Assertions DTOs

/// <summary>
/// Root assertion object from expected_assertions.json.
/// </summary>
public class ExpectedAssertions
{
    public string? FixtureSet { get; set; }
    public string? Fixture { get; set; }
    public string? Description { get; set; }
    public ExpectedSection? Expected { get; set; }
    public ExpectedValidationSection? ExpectedValidation { get; set; }
    public ExpectedBehaviorSection? ExpectedBehavior { get; set; }
}

public class ExpectedSection
{
    public ValidationExpectation? Validation { get; set; }
    public Dictionary<string, CapabilityExpectation>? Capabilities { get; set; }
    public Dictionary<string, MilestoneExpectation>? Milestones { get; set; }
    public FingerprintsExpectation? Fingerprints { get; set; }
    public ComparisonIntentExpectation? ComparisonIntent { get; set; }
    public ExpectedDeltasSection? ExpectedDeltas { get; set; }
    public SteadyStateMetricsExpectation? SteadyStateMetrics { get; set; }
    public BundleExportExpectation? BundleExport { get; set; }
    public ExpectedOutcomeSection? ExpectedOutcome { get; set; }
}

public class ValidationExpectation
{
    public bool? BaselineValid { get; set; }
    public bool? OptimizedValid { get; set; }
    public bool? NoErrors { get; set; }
    public string? BaselineRunId { get; set; }
    public string? OptimizedRunId { get; set; }
}

public class CapabilityExpectation
{
    public bool? HasLatency { get; set; }
    public bool? HasThroughput { get; set; }
    public bool? HasMemory { get; set; }
    public bool? HasLoss { get; set; }
    public bool? HasAccuracy { get; set; }
}

public class MilestoneExpectation
{
    [JsonPropertyName("warmup_end")]
    public int? WarmupEnd { get; set; }
    [JsonPropertyName("steady_state_start")]
    public int? SteadyStateStart { get; set; }
    [JsonPropertyName("steady_state_end")]
    public int? SteadyStateEnd { get; set; }
}

public class FingerprintsExpectation
{
    public bool? DatasetMustMatch { get; set; }
    public bool? CodeMustMatch { get; set; }
    public bool? EnvironmentMustMatch { get; set; }
    public bool? ModelMayDiffer { get; set; }
    public bool? ModelIdentical { get; set; }
    public bool? DatasetIdentical { get; set; }
    public bool? CodeIdentical { get; set; }
    public bool? EnvironmentIdentical { get; set; }
    public FingerprintValues? Baseline { get; set; }
    public FingerprintValues? Optimized { get; set; }
}

public class FingerprintValues
{
    public string? Model { get; set; }
    public string? Dataset { get; set; }
    public string? Code { get; set; }
    public string? Environment { get; set; }
}

public class ComparisonIntentExpectation
{
    public string? PresetId { get; set; }
    public string? AlignmentMode { get; set; }
    public string? PrimaryMilestone { get; set; }
    public string? FallbackMilestone { get; set; }
    public string? LabelA { get; set; }
    public string? LabelB { get; set; }
}

public class ExpectedDeltasSection
{
    public DeltaExpectation? DeltaTc { get; set; }
    public DeltaExpectation? DeltaO { get; set; }
    public DeltaExpectation? DeltaF { get; set; }
    public DeltaExpectation? DeltaTd { get; set; }
    public DeltaExpectation? DeltaA { get; set; }
}

public class DeltaExpectation
{
    public bool? ShouldBePresent { get; set; }
    public bool? Fired { get; set; }
    public bool? ShouldBeSuppressed { get; set; }
    public string? Direction { get; set; }
    public int? MinDeltaSteps { get; set; }
    public string? Notes { get; set; }
    public string? Reason { get; set; }
}

public class SteadyStateMetricsExpectation
{
    public MetricValues? Baseline { get; set; }
    public MetricValues? Optimized { get; set; }
    public ImprovementValues? Improvement { get; set; }
}

public class MetricValues
{
    public double? LatencyMean { get; set; }
    public double? LatencyStdDev { get; set; }
    public double? ThroughputMean { get; set; }
}

public class ImprovementValues
{
    public string? LatencyReduction { get; set; }
    public string? ThroughputIncrease { get; set; }
}

public class BundleExportExpectation
{
    public bool? ShouldSucceed { get; set; }
    public List<string>? MustInclude { get; set; }
    public bool? ReviewModeEnabled { get; set; }
    public bool? RecomputeDisabled { get; set; }
}

public class ExpectedOutcomeSection
{
    public string? Summary { get; set; }
    public int? DeltasFireCount { get; set; }
    public int? SuppressedCount { get; set; }
    public string? Reason { get; set; }
}

public class ExpectedValidationSection
{
    public bool? IsValid { get; set; }
    public int? ErrorCount { get; set; }
    public List<ErrorExpectation>? Errors { get; set; }
    public List<object>? Warnings { get; set; }
    public List<object>? Infos { get; set; }
}

public class ErrorExpectation
{
    public string? Code { get; set; }
    public string? Severity { get; set; }
    public string? MessageContains { get; set; }
    public string? PathContains { get; set; }
    public Dictionary<string, object>? Context { get; set; }
}

public class ExpectedBehaviorSection
{
    public bool? ComparisonBlocked { get; set; }
    public bool? DeltasComputed { get; set; }
    public bool? BundleExportDisabled { get; set; }
    public bool? ErrorExplanationShown { get; set; }
    public bool? PresetApplicationBlocked { get; set; }
}

#endregion
