using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Phase 3 Delta Metrics - Core Types
/// Matches TypeScript specification for cross-platform consistency.
/// </summary>
/// 
/// <summary>
/// Visual anchor for linking delta to UI regions.
/// </summary>
public record VisualAnchor
{
    /// <summary>Target view for this anchor.</summary>
    public required string TargetView { get; init; }
    
    /// <summary>Step range in Path A to highlight.</summary>
    public (int T0, int T1)? RangeA { get; init; }
    
    /// <summary>Step range in Path B to highlight.</summary>
    public (int T0, int T1)? RangeB { get; init; }
    
    /// <summary>Single step marker in Path A.</summary>
    public int? MarkerA { get; init; }
    
    /// <summary>Single step marker in Path B.</summary>
    public int? MarkerB { get; init; }
    
    /// <summary>Compare index marker.</summary>
    public int? CompareIndex { get; init; }
    
    /// <summary>Additional metadata.</summary>
    public Dictionary<string, object>? Meta { get; init; }
}

/// <summary>
/// Alignment map for comparing runs of different lengths.
/// </summary>
public record AlignmentMap
{
    public required TemporalAlignment Mode { get; init; }
    
    /// <summary>Maps compare-index -> step in run A (null if invalid).</summary>
    public required int?[] IdxToStepA { get; init; }
    
    /// <summary>Maps compare-index -> step in run B (null if invalid).</summary>
    public required int?[] IdxToStepB { get; init; }
    
    /// <summary>Common compare timeline indices (0..N-1).</summary>
    public required int[] CompareIndex { get; init; }
    
    /// <summary>Human-readable description.</summary>
    public string Description { get; init; } = "";
}

/// <summary>
/// Delta status enumeration.
/// </summary>
public enum DeltaStatus
{
    /// <summary>Delta is present and meaningful.</summary>
    Present,
    
    /// <summary>Delta suppressed (difference below threshold or not meaningful).</summary>
    Suppressed,
    
    /// <summary>Cannot determine (missing data).</summary>
    Indeterminate
}

/// <summary>
/// Extended canonical delta with full TypeScript spec fields.
/// </summary>
public record CanonicalDelta
{
    /// <summary>Unique identifier for this delta type.</summary>
    public required string Id { get; init; }
    
    /// <summary>Delta status.</summary>
    public DeltaStatus Status { get; init; } = DeltaStatus.Present;
    
    /// <summary>Plain-language name shown in Delta Zone.</summary>
    public required string Name { get; init; }
    
    /// <summary>One-line explanation (≤12 words target).</summary>
    public required string Explanation { get; init; }
    
    /// <summary>Neutral, descriptive sentence for summary.</summary>
    public string? SummarySentence { get; init; }
    
    /// <summary>Left (Path A) value.</summary>
    public double LeftValue { get; init; }
    
    /// <summary>Right (Path B) value.</summary>
    public double RightValue { get; init; }
    
    /// <summary>Numeric difference (B - A).</summary>
    public double Delta { get; init; }
    
    /// <summary>Absolute magnitude of difference.</summary>
    public double Magnitude { get; init; }
    
    /// <summary>Units for delta value.</summary>
    public string? Units { get; init; }
    
    /// <summary>0..1 confidence for display.</summary>
    public double Confidence { get; init; } = 1.0;
    
    /// <summary>Normalized time [0,1] where this difference is most visible.</summary>
    public double VisualAnchorTime { get; init; }
    
    /// <summary>Whether this delta is statistically meaningful.</summary>
    public bool IsMeaningful => Status == DeltaStatus.Present;
    
    /// <summary>Type of delta for grouping/styling.</summary>
    public DeltaType DeltaType { get; init; }
    
    /// <summary>Visual anchors for UI highlighting.</summary>
    public List<VisualAnchor> Anchors { get; init; } = [];
    
    /// <summary>Notes explaining suppression/indeterminate status.</summary>
    public List<string> Notes { get; init; } = [];
    
    // === Type-specific fields ===
    
    /// <summary>Convergence: convergence step in A.</summary>
    public int? TcA { get; init; }
    
    /// <summary>Convergence: convergence step in B.</summary>
    public int? TcB { get; init; }
    
    /// <summary>Convergence: step difference (B - A).</summary>
    public int? DeltaTcSteps { get; init; }
    
    /// <summary>
    /// Convergence: normalized delta for display.
    /// = DeltaTcSteps / max(1, min(TlenA, TlenB))
    /// Phase 3.2: Display only, never suppresses.
    /// </summary>
    public double? DeltaTcNormalized { get; init; }
    
    /// <summary>Convergence: epsilon used.</summary>
    public double? EpsilonUsed { get; init; }
    
    /// <summary>Convergence/Emergence: window used.</summary>
    public int? WindowUsed { get; init; }
    
    /// <summary>
    /// Convergence: confidence heuristic (0-1).
    /// Phase 3.2: Affects visual intensity/wording, NOT suppression.
    /// Higher = longer tail, fewer violations, lower noise.
    /// </summary>
    public double? ConvergenceConfidence { get; init; }
    
    /// <summary>Stability: oscillation score A.</summary>
    public double? ScoreA { get; init; }
    
    /// <summary>Stability: oscillation score B.</summary>
    public double? ScoreB { get; init; }
    
    /// <summary>Stability: threshold used.</summary>
    public double? ThresholdUsed { get; init; }
    
    /// <summary>Stability: min duration used.</summary>
    public int? MinDurationUsed { get; init; }
    
    /// <summary>Alignment: mean alignment A.</summary>
    public double? MeanAlignA { get; init; }
    
    /// <summary>Alignment: mean alignment B.</summary>
    public double? MeanAlignB { get; init; }
    
    /// <summary>Emergence: dominance onset step A.</summary>
    public int? TdA { get; init; }
    
    /// <summary>Emergence: dominance onset step B.</summary>
    public int? TdB { get; init; }
    
    /// <summary>Emergence: dominance ratio k.</summary>
    public double? DominanceRatioK { get; init; }
    
    /// <summary>Failure: did A fail?</summary>
    public bool? FailedA { get; init; }
    
    /// <summary>Failure: did B fail?</summary>
    public bool? FailedB { get; init; }
    
    /// <summary>Failure: failure step A.</summary>
    public int? TFailA { get; init; }
    
    /// <summary>Failure: failure step B.</summary>
    public int? TFailB { get; init; }
    
    /// <summary>Failure: kind of failure A.</summary>
    public string? FailureKindA { get; init; }
    
    /// <summary>Failure: kind of failure B.</summary>
    public string? FailureKindB { get; init; }
}

/// <summary>
/// Type of canonical delta for styling purposes.
/// </summary>
public enum DeltaType
{
    Timing,     // When something happens
    Behavior,   // How paths behave
    Structure,  // Geometric properties
    Event       // Discrete occurrences
}

/// <summary>
/// Complete delta computation result.
/// </summary>
public record DeltaComputationResult
{
    public required AlignmentMap Alignment { get; init; }
    public required List<CanonicalDelta> Deltas { get; init; }
    public string? ComparativeSummary { get; init; }
    
    /// <summary>Phase 6.1: Hash of delta outputs for reproducibility verification.</summary>
    public string? DeltaHash { get; init; }
    
    /// <summary>Phase 6.1: Input fingerprint for determinism tracking.</summary>
    public string? InputFingerprint { get; init; }
    
    /// <summary>Phase 6.1: Reproducibility metadata for exports.</summary>
    public ReproducibilityMetadata? Reproducibility { get; init; }
}

/// <summary>
/// Configuration for convergence detection.
/// </summary>
public record ConvergenceConfig
{
    /// <summary>Stability window length in steps.</summary>
    public int Window { get; init; } = 5;
    
    /// <summary>Minimum window to accept convergence.</summary>
    public int MinWindow { get; init; } = 3;
    
    /// <summary>
    /// Base epsilon for signal-level convergence.
    /// Phase 3.2: Convergence = signal stays within ε band for Window.
    /// Effective ε = max(Epsilon, EpsilonSigmaMultiplier * sigma)
    /// </summary>
    public double Epsilon { get; init; } = 0.02;
    
    /// <summary>
    /// Scale epsilon by robust sigma multiplier.
    /// Uses max() not sum() to avoid blowing up on noisy runs.
    /// </summary>
    public double EpsilonSigmaMultiplier { get; init; } = 0.5;
    
    /// <summary>
    /// Minimum step separation to treat |ΔTc| as meaningful.
    /// Phase 3.2: This is the ONLY suppression gate for ΔTc.
    /// </summary>
    public int ResolutionSteps { get; init; } = 3;
    
    /// <summary>
    /// Normalized time resolution for DISPLAY ONLY.
    /// Phase 3.2: Never used for suppression gating.
    /// Kept for human readability across different run lengths.
    /// </summary>
    public double DisplayResolutionNorm { get; init; } = 0.05;
}

/// <summary>
/// Configuration for stability/oscillation detection.
/// Phase 3.2: Area-above-θ scoring with stabilized adaptive threshold.
/// </summary>
public record StabilityConfig
{
    /// <summary>Fixed curvature magnitude threshold. 0 = adaptive.</summary>
    public double Theta { get; init; } = 0.0;
    
    /// <summary>
    /// Multiplier for median-based adaptive threshold.
    /// θ_eff = max(median×ThetaMultiplier, sigma×ThetaSigmaMultiplier)
    /// </summary>
    public double ThetaMultiplier { get; init; } = 1.5;
    
    /// <summary>
    /// Phase 3.2: Multiplier for sigma-based adaptive threshold.
    /// Prevents θ from collapsing when median is tiny.
    /// </summary>
    public double ThetaSigmaMultiplier { get; init; } = 1.0;
    
    /// <summary>
    /// Minimum sustained duration to count as an episode.
    /// Phase 3.2: Raised from 3 to 4 to cut flicker false positives.
    /// </summary>
    public int MinDuration { get; init; } = 4;
    
    /// <summary>
    /// Minimum |ΔO| to be meaningful (between-run suppression).
    /// Applied after area scoring.
    /// </summary>
    public double DeltaFloor { get; init; } = 0.05;
    
    /// <summary>
    /// Minimum episode score to count (within-run suppression).
    /// Episodes with score < NoiseFloor are ignored.
    /// </summary>
    public double NoiseFloor { get; init; } = 0.1;
    
    /// <summary>Multiplier for adaptive threshold (deprecated, use ThetaMultiplier).</summary>
    public double ThresholdMultiplier { get; init; } = 1.5;
}

/// <summary>
/// Configuration for evaluator alignment detection.
/// </summary>
public record AlignmentDetectionConfig
{
    /// <summary>Smoothing window for A(t).</summary>
    public int SmoothWindow { get; init; } = 5;
    
    /// <summary>Minimum evaluator count required.</summary>
    public int MinEvaluators { get; init; } = 2;
    
    /// <summary>Minimum persistence-weighted delta to be meaningful (Phase 3.2: lowered from 0.08).</summary>
    public double DeltaFloor { get; init; } = 0.05;
    
    /// <summary>Minimum variance threshold.</summary>
    public double MinVariance { get; init; } = 0.01;
    
    /// <summary>Minimum sustained steps for persistence-weighted delta (Phase 3.2: new parameter).</summary>
    public int MinPersistenceSteps { get; init; } = 4;
    
    /// <summary>Small epsilon for detecting sustained difference segments.</summary>
    public double SegmentEpsilon { get; init; } = 0.02;
}

/// <summary>
/// Configuration for structural emergence detection.
/// Phase 3.2: Added recurrence rule parameters.
/// </summary>
public record EmergenceConfig
{
    /// <summary>Dominance ratio k where λ1 > k * λ2.</summary>
    public double K { get; init; } = 1.5;
    
    /// <summary>Persistence window for sustained dominance (Condition A).</summary>
    public int Window { get; init; } = 3;
    
    /// <summary>Minimum delta steps to be meaningful.</summary>
    public int ResolutionSteps { get; init; } = 3;
    
    /// <summary>Phase 3.2: Rolling window for recurrence rule (Condition B).</summary>
    public int RecurrenceWindow { get; init; } = 7;
    
    /// <summary>Phase 3.2: Minimum segment length for recurrence (must be ≥2).</summary>
    public int MinSegmentLength { get; init; } = 2;
    
    /// <summary>Phase 3.2: Minimum segments required for recurrence rule.</summary>
    public int MinRecurrenceCount { get; init; } = 2;
    
    /// <summary>Dominance ratio alias (same as K).</summary>
    public double DominanceRatio => K;
    
    /// <summary>Window size alias (same as Window).</summary>
    public int WindowSize => Window;
}

/// <summary>
/// Configuration for failure detection.
/// </summary>
public record FailureConfig
{
    /// <summary>Persistent violation window to confirm.</summary>
    public int PersistenceWindow { get; init; } = 3;
    
    /// <summary>Max norm threshold (optional).</summary>
    public double? NormMax { get; init; }
    
    /// <summary>Max loss threshold (optional).</summary>
    public double? LossMax { get; init; }
}

/// <summary>
/// Complete detector configuration.
/// </summary>
public record DeltaDetectorConfig
{
    public ConvergenceConfig Convergence { get; init; } = new();
    public StabilityConfig Stability { get; init; } = new();
    public AlignmentDetectionConfig Alignment { get; init; } = new();
    public EmergenceConfig Emergence { get; init; } = new();
    public FailureConfig Failure { get; init; } = new();
}
