// ConnectorPreset - Signal Mapping Configuration
// Maps external metric names to ScalarScope signal roles.

using System.Text.Json.Serialization;

namespace ScalarScope.Services.Connectors;

/// <summary>
/// Configuration for mapping external metrics to ScalarScope signals.
/// Each connector source has default presets; users can customize.
/// </summary>
public sealed record ConnectorPreset
{
    /// <summary>Preset identifier.</summary>
    public required string PresetId { get; init; }
    
    /// <summary>Preset version.</summary>
    public required string PresetVersion { get; init; }
    
    /// <summary>Source this preset is designed for.</summary>
    public required ConnectorSourceType Source { get; init; }
    
    /// <summary>Human-readable name.</summary>
    public required string DisplayName { get; init; }
    
    /// <summary>Description of this preset.</summary>
    public string? Description { get; init; }
    
    /// <summary>Signal role mappings.</summary>
    public required SignalMappings Mappings { get; init; }
    
    /// <summary>Milestone type mappings.</summary>
    public MilestoneMappings? MilestoneMapping { get; init; }
    
    /// <summary>Normalization settings.</summary>
    public NormalizationSettings? Normalization { get; init; }
    
    /// <summary>Whether this is a built-in preset.</summary>
    public bool IsBuiltIn { get; init; }
}

/// <summary>
/// Signal role mappings from external metrics to ScalarScope.
/// </summary>
public sealed record SignalMappings
{
    /// <summary>Primary learning/loss signal.</summary>
    public SignalMapping? LearningSignal { get; init; }
    
    /// <summary>Evaluation/validation signal.</summary>
    public SignalMapping? EvaluationSignal { get; init; }
    
    /// <summary>Curvature proxy signal.</summary>
    public SignalMapping? CurvatureSignal { get; init; }
    
    /// <summary>Eigenvalue spectrum signal.</summary>
    public SignalMapping? SpectrumSignal { get; init; }
    
    /// <summary>Learning rate signal.</summary>
    public SignalMapping? LearningRateSignal { get; init; }
    
    /// <summary>Gradient norm signal.</summary>
    public SignalMapping? GradientNormSignal { get; init; }
    
    /// <summary>Custom signal mappings (key = ScalarScope signal name).</summary>
    public IReadOnlyDictionary<string, SignalMapping>? CustomSignals { get; init; }
}

/// <summary>
/// Mapping from external metric name(s) to a ScalarScope signal.
/// </summary>
public sealed record SignalMapping
{
    /// <summary>Primary metric name to look for.</summary>
    public required string Primary { get; init; }
    
    /// <summary>Fallback metric names if primary not found.</summary>
    public IReadOnlyList<string>? Fallbacks { get; init; }
    
    /// <summary>Optional transformation to apply.</summary>
    public SignalTransform? Transform { get; init; }
    
    /// <summary>Resolve the metric name from available metrics.</summary>
    public string? Resolve(IEnumerable<string> availableMetrics)
    {
        var metrics = availableMetrics.ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        if (metrics.Contains(Primary))
            return Primary;
        
        if (Fallbacks is not null)
        {
            foreach (var fallback in Fallbacks)
            {
                if (metrics.Contains(fallback))
                    return fallback;
            }
        }
        
        return null;
    }
}

/// <summary>
/// Transformation to apply to a signal.
/// </summary>
public sealed record SignalTransform
{
    /// <summary>Type of transformation.</summary>
    public required TransformType Type { get; init; }
    
    /// <summary>Scale factor (for Scale transform).</summary>
    public double? Scale { get; init; }
    
    /// <summary>Offset to add (for Offset transform).</summary>
    public double? Offset { get; init; }
    
    /// <summary>Whether to invert the signal.</summary>
    public bool Invert { get; init; }
    
    /// <summary>Smoothing window size.</summary>
    public int? SmoothingWindow { get; init; }
}

/// <summary>
/// Types of signal transformations.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransformType
{
    /// <summary>No transformation.</summary>
    None,
    
    /// <summary>Multiply by scale factor.</summary>
    Scale,
    
    /// <summary>Add offset.</summary>
    Offset,
    
    /// <summary>Negate values.</summary>
    Negate,
    
    /// <summary>Take logarithm.</summary>
    Log,
    
    /// <summary>Take exponential.</summary>
    Exp,
    
    /// <summary>Apply smoothing.</summary>
    Smooth,
    
    /// <summary>Compute gradient.</summary>
    Gradient
}

/// <summary>
/// Milestone type mappings from external names.
/// </summary>
public sealed record MilestoneMappings
{
    /// <summary>Names that indicate epoch boundaries.</summary>
    public IReadOnlyList<string>? Epoch { get; init; }
    
    /// <summary>Names that indicate evaluation points.</summary>
    public IReadOnlyList<string>? Eval { get; init; }
    
    /// <summary>Names that indicate checkpoints.</summary>
    public IReadOnlyList<string>? Checkpoint { get; init; }
    
    /// <summary>Names that indicate LR schedule steps.</summary>
    public IReadOnlyList<string>? LrStep { get; init; }
}

/// <summary>
/// Normalization settings for signal processing.
/// </summary>
public sealed record NormalizationSettings
{
    /// <summary>How to scale loss values.</summary>
    public LossScaleMode LossScale { get; init; } = LossScaleMode.Auto;
    
    /// <summary>Step offset to apply (shift all steps by this amount).</summary>
    public int StepOffset { get; init; }
    
    /// <summary>Whether to align steps to start at 0.</summary>
    public bool NormalizeStepStart { get; init; } = true;
    
    /// <summary>Whether to remove NaN/Inf values.</summary>
    public bool RemoveInvalidValues { get; init; } = true;
    
    /// <summary>Minimum number of data points required.</summary>
    public int MinDataPoints { get; init; } = 10;
}

/// <summary>
/// Loss scaling modes.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LossScaleMode
{
    /// <summary>Automatic scaling based on value range.</summary>
    Auto,
    
    /// <summary>No scaling.</summary>
    None,
    
    /// <summary>Logarithmic scaling.</summary>
    Log,
    
    /// <summary>Min-max normalization to [0,1].</summary>
    MinMax,
    
    /// <summary>Z-score normalization.</summary>
    ZScore
}

/// <summary>
/// Service for managing and applying connector presets.
/// </summary>
public sealed class ConnectorPresetService
{
    /// <summary>Singleton instance.</summary>
    public static readonly ConnectorPresetService Instance = new();
    
    private readonly Dictionary<string, ConnectorPreset> _presets = new(StringComparer.OrdinalIgnoreCase);
    
    private ConnectorPresetService()
    {
        RegisterBuiltInPresets();
    }
    
    /// <summary>Get all registered presets.</summary>
    public IReadOnlyCollection<ConnectorPreset> GetAll() => _presets.Values;
    
    /// <summary>Get presets for a specific source.</summary>
    public IEnumerable<ConnectorPreset> GetForSource(ConnectorSourceType source) 
        => _presets.Values.Where(p => p.Source == source);
    
    /// <summary>Get a preset by ID.</summary>
    public ConnectorPreset? Get(string presetId) 
        => _presets.TryGetValue(presetId, out var preset) ? preset : null;
    
    /// <summary>Get the default preset for a source.</summary>
    public ConnectorPreset? GetDefault(ConnectorSourceType source)
        => _presets.Values.FirstOrDefault(p => p.Source == source && p.IsBuiltIn);
    
    /// <summary>Register a custom preset.</summary>
    public void Register(ConnectorPreset preset)
    {
        _presets[preset.PresetId] = preset;
    }
    
    private void RegisterBuiltInPresets()
    {
        // TensorBoard default preset
        _presets["tensorboard-default"] = new ConnectorPreset
        {
            PresetId = "tensorboard-default",
            PresetVersion = "1.0.0",
            Source = ConnectorSourceType.TensorBoard,
            DisplayName = "TensorBoard Default",
            Description = "Standard mapping for TensorBoard event files",
            IsBuiltIn = true,
            Mappings = new SignalMappings
            {
                LearningSignal = new SignalMapping
                {
                    Primary = "train/loss",
                    Fallbacks = ["loss", "training_loss", "train_loss", "Loss/train"]
                },
                EvaluationSignal = new SignalMapping
                {
                    Primary = "eval/loss",
                    Fallbacks = ["val/loss", "validation_loss", "eval_loss", "Loss/val", "val_loss"]
                },
                LearningRateSignal = new SignalMapping
                {
                    Primary = "learning_rate",
                    Fallbacks = ["lr", "LearningRate", "train/lr"]
                },
                GradientNormSignal = new SignalMapping
                {
                    Primary = "grad_norm",
                    Fallbacks = ["gradient_norm", "gradients/global_norm"]
                }
            },
            MilestoneMapping = new MilestoneMappings
            {
                Epoch = ["epoch", "epochs"],
                Eval = ["eval", "evaluation", "validation", "val"],
                Checkpoint = ["checkpoint", "ckpt", "save"]
            },
            Normalization = new NormalizationSettings
            {
                LossScale = LossScaleMode.Auto,
                NormalizeStepStart = true,
                RemoveInvalidValues = true
            }
        };
        
        // MLflow default preset
        _presets["mlflow-default"] = new ConnectorPreset
        {
            PresetId = "mlflow-default",
            PresetVersion = "1.0.0",
            Source = ConnectorSourceType.MLflow,
            DisplayName = "MLflow Default",
            Description = "Standard mapping for MLflow tracking",
            IsBuiltIn = true,
            Mappings = new SignalMappings
            {
                LearningSignal = new SignalMapping
                {
                    Primary = "training_loss",
                    Fallbacks = ["loss", "train_loss", "train/loss"]
                },
                EvaluationSignal = new SignalMapping
                {
                    Primary = "val_loss",
                    Fallbacks = ["validation_loss", "eval_loss", "val/loss"]
                },
                LearningRateSignal = new SignalMapping
                {
                    Primary = "learning_rate",
                    Fallbacks = ["lr"]
                }
            },
            MilestoneMapping = new MilestoneMappings
            {
                Epoch = ["epoch"],
                Eval = ["eval_step", "validation_step"],
                Checkpoint = ["checkpoint"]
            },
            Normalization = new NormalizationSettings
            {
                LossScale = LossScaleMode.Auto,
                NormalizeStepStart = true
            }
        };
        
        // PyTorch Lightning preset
        _presets["lightning-default"] = new ConnectorPreset
        {
            PresetId = "lightning-default",
            PresetVersion = "1.0.0",
            Source = ConnectorSourceType.TensorBoard,
            DisplayName = "PyTorch Lightning",
            Description = "Standard mapping for PyTorch Lightning logs",
            IsBuiltIn = true,
            Mappings = new SignalMappings
            {
                LearningSignal = new SignalMapping
                {
                    Primary = "train_loss_step",
                    Fallbacks = ["train_loss", "train/loss", "loss"]
                },
                EvaluationSignal = new SignalMapping
                {
                    Primary = "val_loss",
                    Fallbacks = ["val_loss_epoch", "validation_loss"]
                },
                LearningRateSignal = new SignalMapping
                {
                    Primary = "lr-Adam",
                    Fallbacks = ["lr", "learning_rate"]
                }
            },
            MilestoneMapping = new MilestoneMappings
            {
                Epoch = ["epoch"],
                Eval = ["validation"],
                Checkpoint = ["checkpoint"]
            },
            Normalization = new NormalizationSettings
            {
                LossScale = LossScaleMode.Auto,
                NormalizeStepStart = true
            }
        };
        
        // HuggingFace Trainer preset
        _presets["huggingface-default"] = new ConnectorPreset
        {
            PresetId = "huggingface-default",
            PresetVersion = "1.0.0",
            Source = ConnectorSourceType.HuggingFace,
            DisplayName = "HuggingFace Trainer",
            Description = "Standard mapping for HuggingFace Trainer logs",
            IsBuiltIn = true,
            Mappings = new SignalMappings
            {
                LearningSignal = new SignalMapping
                {
                    Primary = "train/loss",
                    Fallbacks = ["loss", "train_loss"]
                },
                EvaluationSignal = new SignalMapping
                {
                    Primary = "eval/loss",
                    Fallbacks = ["eval_loss", "validation_loss"]
                },
                LearningRateSignal = new SignalMapping
                {
                    Primary = "train/learning_rate",
                    Fallbacks = ["learning_rate", "lr"]
                }
            },
            MilestoneMapping = new MilestoneMappings
            {
                Epoch = ["epoch"],
                Eval = ["eval"],
                Checkpoint = ["checkpoint"]
            },
            Normalization = new NormalizationSettings
            {
                LossScale = LossScaleMode.Auto,
                NormalizeStepStart = true
            }
        };
        
        // Generic CSV/JSON logs preset
        _presets["logs-default"] = new ConnectorPreset
        {
            PresetId = "logs-default",
            PresetVersion = "1.0.0",
            Source = ConnectorSourceType.LogDirectory,
            DisplayName = "Generic Logs",
            Description = "Flexible mapping for CSV/JSON log files",
            IsBuiltIn = true,
            Mappings = new SignalMappings
            {
                LearningSignal = new SignalMapping
                {
                    Primary = "loss",
                    Fallbacks = ["train_loss", "training_loss", "Loss"]
                },
                EvaluationSignal = new SignalMapping
                {
                    Primary = "val_loss",
                    Fallbacks = ["eval_loss", "validation_loss", "test_loss"]
                }
            },
            Normalization = new NormalizationSettings
            {
                LossScale = LossScaleMode.Auto,
                NormalizeStepStart = true
            }
        };
    }
}
