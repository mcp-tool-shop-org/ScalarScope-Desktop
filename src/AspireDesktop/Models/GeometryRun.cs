using System.Text.Json.Serialization;

namespace AspireDesktop.Models;

/// <summary>
/// Root model for a geometry export from ASPIRE.
/// One file = one run.
/// </summary>
public record GeometryRun
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("run_metadata")]
    public RunMetadata Metadata { get; init; } = new();

    [JsonPropertyName("reduction")]
    public DimensionalityReduction Reduction { get; init; } = new();

    [JsonPropertyName("trajectory")]
    public Trajectory Trajectory { get; init; } = new();

    [JsonPropertyName("scalars")]
    public ScalarTimeSeries Scalars { get; init; } = new();

    [JsonPropertyName("geometry")]
    public GeometryMetrics Geometry { get; init; } = new();

    [JsonPropertyName("evaluators")]
    public EvaluatorGeometry Evaluators { get; init; } = new();

    [JsonPropertyName("failures")]
    public List<FailureEvent> Failures { get; init; } = [];
}

public record RunMetadata
{
    [JsonPropertyName("run_id")]
    public string RunId { get; init; } = "";

    [JsonPropertyName("condition")]
    public string Condition { get; init; } = "";

    [JsonPropertyName("seed")]
    public int Seed { get; init; }

    [JsonPropertyName("training_items")]
    public int TrainingItems { get; init; }

    [JsonPropertyName("cycles")]
    public int Cycles { get; init; }

    [JsonPropertyName("holdout_professor")]
    public string? HoldoutProfessor { get; init; }

    [JsonPropertyName("conscience_tier")]
    public string ConscienceTier { get; init; } = "UNKNOWN";
}

public record DimensionalityReduction
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = "PCA";

    [JsonPropertyName("input_dim")]
    public int InputDim { get; init; }

    [JsonPropertyName("output_dim")]
    public int OutputDim { get; init; } = 2;

    [JsonPropertyName("explained_variance")]
    public List<double> ExplainedVariance { get; init; } = [];

    [JsonPropertyName("components")]
    public List<List<double>> Components { get; init; } = [];
}

public record Trajectory
{
    [JsonPropertyName("timesteps")]
    public List<TrajectoryTimestep> Timesteps { get; init; } = [];
}

public record TrajectoryTimestep
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("state_2d")]
    public List<double> State2D { get; init; } = [];

    [JsonPropertyName("velocity")]
    public List<double> Velocity { get; init; } = [];

    [JsonPropertyName("curvature")]
    public double Curvature { get; init; }

    [JsonPropertyName("effective_dim")]
    public double EffectiveDim { get; init; }
}

public record ScalarTimeSeries
{
    [JsonPropertyName("dimensions")]
    public List<string> Dimensions { get; init; } = [];

    [JsonPropertyName("values")]
    public List<ScalarTimestep> Values { get; init; } = [];
}

public record ScalarTimestep
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("correctness")]
    public double Correctness { get; init; }

    [JsonPropertyName("coherence")]
    public double Coherence { get; init; }

    [JsonPropertyName("calibration")]
    public double Calibration { get; init; }

    [JsonPropertyName("tradeoffs")]
    public double Tradeoffs { get; init; }

    [JsonPropertyName("clarity")]
    public double Clarity { get; init; }

    public double[] ToArray() => [Correctness, Coherence, Calibration, Tradeoffs, Clarity];
}

public record GeometryMetrics
{
    [JsonPropertyName("eigenvalues")]
    public List<EigenTimestep> Eigenvalues { get; init; } = [];

    [JsonPropertyName("anisotropy")]
    public List<AnisotropyTimestep> Anisotropy { get; init; } = [];
}

public record EigenTimestep
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("values")]
    public List<double> Values { get; init; } = [];
}

public record AnisotropyTimestep
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("ratio")]
    public double Ratio { get; init; }
}

public record EvaluatorGeometry
{
    [JsonPropertyName("latent_dim")]
    public int LatentDim { get; init; }

    [JsonPropertyName("professors")]
    public List<ProfessorVector> Professors { get; init; } = [];
}

public record ProfessorVector
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("vector")]
    public List<double> Vector { get; init; } = [];

    [JsonPropertyName("holdout")]
    public bool Holdout { get; init; }
}

public record FailureEvent
{
    [JsonPropertyName("t")]
    public double T { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
}
