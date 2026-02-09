using System.Text.Json.Serialization;

namespace ScalarScope.Services.Evidence;

/// <summary>
/// Phase 3.1: Detector Diagnostics Export
/// Captures all intermediate values and decisions made during delta detection.
/// This enables scientific tuning by showing exactly how each threshold was applied.
/// </summary>
public record DetectorDiagnostics
{
    /// <summary>Pair ID this diagnostic belongs to.</summary>
    [JsonPropertyName("pair_id")]
    public required string PairId { get; init; }

    /// <summary>Alignment mode used.</summary>
    [JsonPropertyName("alignment_mode")]
    public required TemporalAlignment AlignmentMode { get; init; }

    /// <summary>Timestamp of diagnostic capture.</summary>
    [JsonPropertyName("captured_at")]
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Convergence detection diagnostics.</summary>
    [JsonPropertyName("convergence")]
    public ConvergenceDiagnostics? Convergence { get; init; }

    /// <summary>Structural emergence diagnostics.</summary>
    [JsonPropertyName("emergence")]
    public EmergenceDiagnostics? Emergence { get; init; }

    /// <summary>Evaluator alignment diagnostics.</summary>
    [JsonPropertyName("evaluator_alignment")]
    public EvaluatorAlignmentDiagnostics? EvaluatorAlignment { get; init; }

    /// <summary>Stability/oscillation diagnostics.</summary>
    [JsonPropertyName("stability")]
    public StabilityDiagnostics? Stability { get; init; }

    /// <summary>Failure detection diagnostics.</summary>
    [JsonPropertyName("failure")]
    public FailureDiagnostics? Failure { get; init; }

    /// <summary>Alignment mapping diagnostics.</summary>
    [JsonPropertyName("alignment_mapping")]
    public AlignmentMappingDiagnostics? AlignmentMapping { get; init; }
}

/// <summary>
/// Convergence (ΔTc) detection diagnostics.
/// </summary>
/// <summary>
/// Convergence Timing (ΔTc) detection diagnostics.
/// Phase 3.2: Step-based resolution primary, normalized for display only.
/// </summary>
public record ConvergenceDiagnostics
{
    // Configuration used
    [JsonPropertyName("config_epsilon")]
    public double ConfigEpsilon { get; init; }

    [JsonPropertyName("config_window")]
    public int ConfigWindow { get; init; }

    [JsonPropertyName("config_min_window")]
    public int ConfigMinWindow { get; init; }

    [JsonPropertyName("config_resolution_steps")]
    public int ConfigResolutionSteps { get; init; }

    // Phase 3.2: Display-only normalized resolution
    [JsonPropertyName("config_display_resolution_norm")]
    public double ConfigDisplayResolutionNorm { get; init; }

    // Adaptive epsilon components
    [JsonPropertyName("run_a_robust_sigma")]
    public double RunARobustSigma { get; init; }

    [JsonPropertyName("run_b_robust_sigma")]
    public double RunBRobustSigma { get; init; }

    [JsonPropertyName("run_a_epsilon_used")]
    public double RunAEpsilonUsed { get; init; }

    [JsonPropertyName("run_b_epsilon_used")]
    public double RunBEpsilonUsed { get; init; }

    [JsonPropertyName("sigma_multiplier")]
    public double SigmaMultiplier { get; init; }

    // Candidate convergence indices
    [JsonPropertyName("run_a_candidate_indices")]
    public List<int> RunACandidateIndices { get; init; } = [];

    [JsonPropertyName("run_b_candidate_indices")]
    public List<int> RunBCandidateIndices { get; init; } = [];

    // Accepted values
    [JsonPropertyName("run_a_tc")]
    public int? RunATc { get; init; }

    [JsonPropertyName("run_b_tc")]
    public int? RunBTc { get; init; }

    [JsonPropertyName("delta_tc_steps")]
    public int? DeltaTcSteps { get; init; }

    // Phase 3.2: Normalized delta for display
    [JsonPropertyName("delta_tc_normalized")]
    public double? DeltaTcNormalized { get; init; }

    // Phase 3.2: Confidence heuristics (not gating)
    [JsonPropertyName("run_a_tail_length")]
    public int RunATailLength { get; init; }

    [JsonPropertyName("run_b_tail_length")]
    public int RunBTailLength { get; init; }

    [JsonPropertyName("run_a_violations")]
    public int RunAViolations { get; init; }

    [JsonPropertyName("run_b_violations")]
    public int RunBViolations { get; init; }

    [JsonPropertyName("run_a_confidence")]
    public double RunAConfidence { get; init; }

    [JsonPropertyName("run_b_confidence")]
    public double RunBConfidence { get; init; }

    [JsonPropertyName("combined_confidence")]
    public double CombinedConfidence { get; init; }

    // Final status
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }

    // Raw velocity series (for verification)
    [JsonPropertyName("run_a_velocities")]
    public List<double>? RunAVelocities { get; init; }

    [JsonPropertyName("run_b_velocities")]
    public List<double>? RunBVelocities { get; init; }
}

/// <summary>
/// Structural Emergence (ΔTd) detection diagnostics.
/// Phase 3.2: Updated for recurrence rule.
/// </summary>
public record EmergenceDiagnostics
{
    // Configuration
    [JsonPropertyName("dominance_ratio_k")]
    public double DominanceRatioK { get; init; }

    [JsonPropertyName("persistence_window")]
    public int PersistenceWindow { get; init; }

    [JsonPropertyName("resolution_steps")]
    public int ResolutionSteps { get; init; }

    // Phase 3.2: Recurrence rule config
    [JsonPropertyName("recurrence_window")]
    public int RecurrenceWindow { get; init; }

    [JsonPropertyName("min_segment_length")]
    public int MinSegmentLength { get; init; }

    [JsonPropertyName("min_recurrence_count")]
    public int MinRecurrenceCount { get; init; }

    // Boolean dominance arrays
    [JsonPropertyName("run_a_dominance_array")]
    public List<bool> RunADominanceArray { get; init; } = [];

    [JsonPropertyName("run_b_dominance_array")]
    public List<bool> RunBDominanceArray { get; init; } = [];

    // Phase 3.2: All detected segments (before filtering)
    [JsonPropertyName("run_a_all_segments")]
    public List<(int Start, int Length)> RunAAllSegments { get; init; } = [];

    [JsonPropertyName("run_b_all_segments")]
    public List<(int Start, int Length)> RunBAllSegments { get; init; } = [];

    // Rejected transient segments (single-step spikes)
    [JsonPropertyName("run_a_rejected_transients")]
    public List<(int Start, int End)> RunARejectedTransients { get; init; } = [];

    [JsonPropertyName("run_b_rejected_transients")]
    public List<(int Start, int End)> RunBRejectedTransients { get; init; } = [];

    // Phase 3.2: Detection trigger type
    [JsonPropertyName("run_a_trigger")]
    public string? RunATrigger { get; init; } // "sustained", "recurrence", or null

    [JsonPropertyName("run_b_trigger")]
    public string? RunBTrigger { get; init; }

    // Phase 3.2: Recurrence segments (if triggered by recurrence)
    [JsonPropertyName("run_a_recurrence_segments")]
    public List<(int Start, int Length)>? RunARecurrenceSegments { get; init; }

    [JsonPropertyName("run_b_recurrence_segments")]
    public List<(int Start, int Length)>? RunBRecurrenceSegments { get; init; }

    // Accepted values
    [JsonPropertyName("run_a_td")]
    public int? RunATd { get; init; }

    [JsonPropertyName("run_b_td")]
    public int? RunBTd { get; init; }

    [JsonPropertyName("delta_td_steps")]
    public int? DeltaTdSteps { get; init; }

    // Final status
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }

    // Raw eigenvalue ratio series
    [JsonPropertyName("run_a_lambda_ratios")]
    public List<double>? RunALambdaRatios { get; init; }

    [JsonPropertyName("run_b_lambda_ratios")]
    public List<double>? RunBLambdaRatios { get; init; }
}

/// <summary>
/// Evaluator Alignment (ΔĀ) detection diagnostics.
/// Phase 3.2: Updated for persistence-weighted delta.
/// </summary>
public record EvaluatorAlignmentDiagnostics
{
    // Configuration
    [JsonPropertyName("smooth_window")]
    public int SmoothWindow { get; init; }

    [JsonPropertyName("min_evaluators")]
    public int MinEvaluators { get; init; }

    [JsonPropertyName("delta_floor")]
    public double DeltaFloor { get; init; }

    // Phase 3.2: New persistence parameters
    [JsonPropertyName("min_persistence_steps")]
    public int MinPersistenceSteps { get; init; }

    [JsonPropertyName("segment_epsilon")]
    public double SegmentEpsilon { get; init; }

    // Raw alignment series A(t)
    [JsonPropertyName("run_a_raw_alignment")]
    public List<double> RunARawAlignment { get; init; } = [];

    [JsonPropertyName("run_b_raw_alignment")]
    public List<double> RunBRawAlignment { get; init; } = [];

    // Difference series D(t) = |A_B(t) - A_A(t)|
    [JsonPropertyName("difference_series")]
    public List<double> DifferenceSeries { get; init; } = [];

    // Smoothed series
    [JsonPropertyName("run_a_smoothed_alignment")]
    public List<double>? RunASmoothedAlignment { get; init; }

    [JsonPropertyName("run_b_smoothed_alignment")]
    public List<double>? RunBSmoothedAlignment { get; init; }

    // Mean values
    [JsonPropertyName("run_a_mean_alignment")]
    public double RunAMeanAlignment { get; init; }

    [JsonPropertyName("run_b_mean_alignment")]
    public double RunBMeanAlignment { get; init; }

    [JsonPropertyName("run_a_variance")]
    public double RunAVariance { get; init; }

    [JsonPropertyName("run_b_variance")]
    public double RunBVariance { get; init; }

    // Phase 3.2: Persistence-weighted metrics
    [JsonPropertyName("persistence_score")]
    public double PersistenceScore { get; init; }

    [JsonPropertyName("sustained_segment_start")]
    public int SustainedSegmentStart { get; init; }

    [JsonPropertyName("sustained_segment_duration")]
    public int SustainedSegmentDuration { get; init; }

    [JsonPropertyName("area_under_curve")]
    public double AreaUnderCurve { get; init; }

    // Legacy: mean-based delta (for comparison)
    [JsonPropertyName("delta_alignment")]
    public double DeltaAlignment { get; init; }

    // Phase 3.2: Which gate caused suppression (for diagnostics)
    [JsonPropertyName("suppression_gate")]
    public string? SuppressionGate { get; init; } // "delta_floor", "min_persistence", or null

    // Final status
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }
}

/// <summary>
/// Stability/Oscillation (ΔO) detection diagnostics.
/// <summary>
/// Stability/Oscillation (ΔO) detection diagnostics.
/// Phase 3.2: Area-above-θ scoring with stabilized adaptive threshold.
/// </summary>
public record StabilityDiagnostics
{
    // Configuration
    [JsonPropertyName("theta_config")]
    public double ThetaConfig { get; init; }

    [JsonPropertyName("theta_adaptive")]
    public bool ThetaAdaptive { get; init; }

    [JsonPropertyName("theta_multiplier")]
    public double ThetaMultiplier { get; init; }

    // Phase 3.2: Sigma-based threshold
    [JsonPropertyName("theta_sigma_multiplier")]
    public double ThetaSigmaMultiplier { get; init; }

    [JsonPropertyName("min_duration")]
    public int MinDuration { get; init; }

    [JsonPropertyName("delta_floor")]
    public double DeltaFloor { get; init; }

    [JsonPropertyName("noise_floor")]
    public double NoiseFloor { get; init; }

    // Phase 3.2: Computed adaptive threshold components
    [JsonPropertyName("run_a_median_abs")]
    public double RunAMedianAbs { get; init; }

    [JsonPropertyName("run_b_median_abs")]
    public double RunBMedianAbs { get; init; }

    [JsonPropertyName("run_a_sigma_abs")]
    public double RunASigmaAbs { get; init; }

    [JsonPropertyName("run_b_sigma_abs")]
    public double RunBSigmaAbs { get; init; }

    // Computed effective thresholds
    [JsonPropertyName("run_a_theta_eff")]
    public double RunAThetaEff { get; init; }

    [JsonPropertyName("run_b_theta_eff")]
    public double RunBThetaEff { get; init; }

    // Boolean oscillation arrays (above θ_eff)
    [JsonPropertyName("run_a_oscillation_array")]
    public List<bool> RunAOscillationArray { get; init; } = [];

    [JsonPropertyName("run_b_oscillation_array")]
    public List<bool> RunBOscillationArray { get; init; } = [];

    // Phase 3.2: Episode details with area scores
    [JsonPropertyName("run_a_episodes")]
    public List<EpisodeDetail> RunAEpisodes { get; init; } = [];

    [JsonPropertyName("run_b_episodes")]
    public List<EpisodeDetail> RunBEpisodes { get; init; } = [];

    // Phase 3.2: Area-based scores
    [JsonPropertyName("run_a_total_score")]
    public double RunATotalScore { get; init; }

    [JsonPropertyName("run_b_total_score")]
    public double RunBTotalScore { get; init; }

    // Peak episode (highest score)
    [JsonPropertyName("run_a_peak_episode_start")]
    public int RunAPeakEpisodeStart { get; init; }

    [JsonPropertyName("run_b_peak_episode_start")]
    public int RunBPeakEpisodeStart { get; init; }

    [JsonPropertyName("run_a_peak_episode_score")]
    public double RunAPeakEpisodeScore { get; init; }

    [JsonPropertyName("run_b_peak_episode_score")]
    public double RunBPeakEpisodeScore { get; init; }

    // Delta
    [JsonPropertyName("delta_score")]
    public double DeltaScore { get; init; }

    // Final status
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }

    // Raw curvature series
    [JsonPropertyName("run_a_curvatures")]
    public List<double>? RunACurvatures { get; init; }

    [JsonPropertyName("run_b_curvatures")]
    public List<double>? RunBCurvatures { get; init; }
}

/// <summary>
/// Phase 3.2: Episode detail for oscillation diagnostics.
/// </summary>
public record EpisodeDetail
{
    [JsonPropertyName("start")]
    public int Start { get; init; }

    [JsonPropertyName("duration")]
    public int Duration { get; init; }

    [JsonPropertyName("end")]
    public int End => Start + Duration;

    [JsonPropertyName("area_score")]
    public double AreaScore { get; init; }
}

/// <summary>
/// Failure/Collapse detection diagnostics.
/// </summary>
public record FailureDiagnostics
{
    // Configuration
    [JsonPropertyName("persistence_window")]
    public int PersistenceWindow { get; init; }

    [JsonPropertyName("norm_max_threshold")]
    public double? NormMaxThreshold { get; init; }

    [JsonPropertyName("loss_max_threshold")]
    public double? LossMaxThreshold { get; init; }

    // Detection path
    [JsonPropertyName("run_a_detection_path")]
    public string RunADetectionPath { get; init; } = ""; // "event", "divergence_proxy", "collapse_proxy", "none"

    [JsonPropertyName("run_b_detection_path")]
    public string RunBDetectionPath { get; init; } = "";

    // Results
    [JsonPropertyName("run_a_failed")]
    public bool RunAFailed { get; init; }

    [JsonPropertyName("run_b_failed")]
    public bool RunBFailed { get; init; }

    [JsonPropertyName("run_a_failure_step")]
    public int? RunAFailureStep { get; init; }

    [JsonPropertyName("run_b_failure_step")]
    public int? RunBFailureStep { get; init; }

    [JsonPropertyName("run_a_failure_type")]
    public string? RunAFailureType { get; init; }

    [JsonPropertyName("run_b_failure_type")]
    public string? RunBFailureType { get; init; }

    // Final status
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }
}

/// <summary>
/// Alignment mapping diagnostics.
/// </summary>
public record AlignmentMappingDiagnostics
{
    [JsonPropertyName("mode")]
    public TemporalAlignment Mode { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    // Mapping arrays
    [JsonPropertyName("compare_index_count")]
    public int CompareIndexCount { get; init; }

    [JsonPropertyName("run_a_step_count")]
    public int RunAStepCount { get; init; }

    [JsonPropertyName("run_b_step_count")]
    public int RunBStepCount { get; init; }

    // Null/unmapped regions
    [JsonPropertyName("run_a_unmapped_count")]
    public int RunAUnmappedCount { get; init; }

    [JsonPropertyName("run_b_unmapped_count")]
    public int RunBUnmappedCount { get; init; }

    [JsonPropertyName("run_a_unmapped_ranges")]
    public List<(int Start, int End)> RunAUnmappedRanges { get; init; } = [];

    [JsonPropertyName("run_b_unmapped_ranges")]
    public List<(int Start, int End)> RunBUnmappedRanges { get; init; } = [];

    // Anchor information (for convergence/instability alignment)
    [JsonPropertyName("anchor_a_step")]
    public int? AnchorAStep { get; init; }

    [JsonPropertyName("anchor_b_step")]
    public int? AnchorBStep { get; init; }

    [JsonPropertyName("anchor_compare_index")]
    public int? AnchorCompareIndex { get; init; }

    // Full mapping arrays (for detailed analysis)
    [JsonPropertyName("idx_to_step_a")]
    public int?[]? IdxToStepA { get; init; }

    [JsonPropertyName("idx_to_step_b")]
    public int?[]? IdxToStepB { get; init; }

    // Artifacts/edge cases
    [JsonPropertyName("artifacts")]
    public List<string> Artifacts { get; init; } = [];

    [JsonPropertyName("edge_cases")]
    public List<string> EdgeCases { get; init; } = [];
}
