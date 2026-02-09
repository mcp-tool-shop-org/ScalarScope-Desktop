using System.Text.Json.Serialization;

namespace ScalarScope.Services.Evidence;

/// <summary>
/// Phase 3.1: Raw Signal Export
/// Contains all raw signals used by delta detectors for a single run.
/// </summary>
public record RawSignalExport
{
    /// <summary>Run identifier.</summary>
    [JsonPropertyName("run_id")]
    public required string RunId { get; init; }

    /// <summary>Source file path.</summary>
    [JsonPropertyName("source_path")]
    public string SourcePath { get; init; } = "";

    /// <summary>Total number of steps.</summary>
    [JsonPropertyName("step_count")]
    public int StepCount { get; init; }

    /// <summary>Export timestamp.</summary>
    [JsonPropertyName("exported_at")]
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;

    // Time indices
    [JsonPropertyName("step_indices")]
    public required List<int> StepIndices { get; init; }

    [JsonPropertyName("timestamps")]
    public required List<double> Timestamps { get; init; }

    // Learning signal (velocity magnitude as convergence proxy)
    [JsonPropertyName("velocity_magnitude")]
    public required List<double?> VelocityMagnitude { get; init; }

    [JsonPropertyName("velocity_magnitude_units")]
    public string VelocityMagnitudeUnits { get; init; } = "unitless (L2 norm)";

    // Curvature / oscillation proxy
    [JsonPropertyName("curvature")]
    public required List<double?> Curvature { get; init; }

    [JsonPropertyName("curvature_units")]
    public string CurvatureUnits { get; init; } = "radians/step (approx)";

    // Effective dimensionality
    [JsonPropertyName("effective_dim")]
    public List<double?>? EffectiveDim { get; init; }

    [JsonPropertyName("effective_dim_units")]
    public string EffectiveDimUnits { get; init; } = "participation ratio";

    // Eigenvalue spectrum (full λ vector per step)
    [JsonPropertyName("eigenvalues")]
    public List<List<double>?>? Eigenvalues { get; init; }

    [JsonPropertyName("eigenvalue_count")]
    public int? EigenvalueCount { get; init; }

    [JsonPropertyName("eigenvalue_units")]
    public string EigenvalueUnits { get; init; } = "variance explained";

    // Derived: first eigenvalue ratio (λ1/Σλ)
    [JsonPropertyName("first_eigen_ratio")]
    public List<double?>? FirstEigenRatio { get; init; }

    // Derived: dominance ratio (λ1/λ2)
    [JsonPropertyName("dominance_ratio")]
    public List<double?>? DominanceRatio { get; init; }

    // Evaluator vectors (per evaluator, per step) - flattened
    [JsonPropertyName("evaluator_names")]
    public List<string>? EvaluatorNames { get; init; }

    [JsonPropertyName("evaluator_vectors")]
    public List<List<List<double>>?>? EvaluatorVectors { get; init; } // [step][evaluator][dim]

    // Scalar metrics
    [JsonPropertyName("correctness")]
    public List<double?>? Correctness { get; init; }

    [JsonPropertyName("coherence")]
    public List<double?>? Coherence { get; init; }

    [JsonPropertyName("calibration")]
    public List<double?>? Calibration { get; init; }

    // Failure events
    [JsonPropertyName("failure_events")]
    public List<FailureEventExport>? FailureEvents { get; init; }

    // State vectors (2D projection)
    [JsonPropertyName("state_2d")]
    public List<List<double>?>? State2D { get; init; }

    // Missing data documentation
    [JsonPropertyName("missing_data_indices")]
    public Dictionary<string, List<int>> MissingDataIndices { get; init; } = new();

    [JsonPropertyName("missing_data_policy")]
    public string MissingDataPolicy { get; init; } = "null values indicate missing data";
}

/// <summary>
/// Exported failure event.
/// </summary>
public record FailureEventExport
{
    [JsonPropertyName("step")]
    public int Step { get; init; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; init; }

    [JsonPropertyName("category")]
    public string Category { get; init; } = "";

    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";
}

/// <summary>
/// Phase 3.1: Visual Anchor Verification Record
/// Documents whether visual anchors match detector episodes truthfully.
/// </summary>
public record VisualAnchorVerification
{
    [JsonPropertyName("delta_id")]
    public required string DeltaId { get; init; }

    [JsonPropertyName("delta_status")]
    public DeltaStatus DeltaStatus { get; init; }

    // Anchor existence
    [JsonPropertyName("anchor_exists")]
    public bool AnchorExists { get; init; }

    [JsonPropertyName("anchor_visible")]
    public bool AnchorVisible { get; init; }

    // Region matching
    [JsonPropertyName("detector_range_a")]
    public (int Start, int End)? DetectorRangeA { get; init; }

    [JsonPropertyName("detector_range_b")]
    public (int Start, int End)? DetectorRangeB { get; init; }

    [JsonPropertyName("visual_range_a")]
    public (int Start, int End)? VisualRangeA { get; init; }

    [JsonPropertyName("visual_range_b")]
    public (int Start, int End)? VisualRangeB { get; init; }

    [JsonPropertyName("range_match_a")]
    public bool RangeMatchA { get; init; }

    [JsonPropertyName("range_match_b")]
    public bool RangeMatchB { get; init; }

    // Bounds checks
    [JsonPropertyName("exceeds_detector_bounds")]
    public bool ExceedsDetectorBounds { get; init; }

    [JsonPropertyName("under_represents_duration")]
    public bool UnderRepresentsDuration { get; init; }

    // Interaction checks
    [JsonPropertyName("survives_zoom")]
    public bool? SurvivesZoom { get; init; }

    [JsonPropertyName("survives_resize")]
    public bool? SurvivesResize { get; init; }

    [JsonPropertyName("hover_maps_correctly")]
    public bool? HoverMapsCorrectly { get; init; }

    [JsonPropertyName("click_maps_correctly")]
    public bool? ClickMapsCorrectly { get; init; }

    // Suppression checks
    [JsonPropertyName("suppressed_but_shown")]
    public bool SuppressedButShown { get; init; }

    [JsonPropertyName("present_but_hidden")]
    public bool PresentButHidden { get; init; }

    // Magnitude proportionality
    [JsonPropertyName("magnitude")]
    public double Magnitude { get; init; }

    [JsonPropertyName("visual_intensity")]
    public double VisualIntensity { get; init; }

    [JsonPropertyName("intensity_proportional")]
    public bool IntensityProportional { get; init; }

    // Notes
    [JsonPropertyName("verification_notes")]
    public List<string> VerificationNotes { get; init; } = [];
}

/// <summary>
/// Phase 3.1: UX Interpretation Assessment
/// Captures qualitative user understanding data.
/// </summary>
public record UXInterpretationAssessment
{
    [JsonPropertyName("pair_id")]
    public required string PairId { get; init; }

    [JsonPropertyName("assessor_id")]
    public string AssessorId { get; init; } = "";

    [JsonPropertyName("assessed_at")]
    public DateTime AssessedAt { get; init; } = DateTime.UtcNow;

    // Time-to-understand
    [JsonPropertyName("time_to_understand_seconds")]
    public double? TimeToUnderstandSeconds { get; init; }

    [JsonPropertyName("first_correct_verbalization")]
    public string FirstCorrectVerbalization { get; init; } = "";

    // User interpretation
    [JsonPropertyName("user_interpretation")]
    public string UserInterpretation { get; init; } = "";

    [JsonPropertyName("interpretation_correct")]
    public bool? InterpretationCorrect { get; init; }

    // Misinterpretations
    [JsonPropertyName("misinterpretations")]
    public List<string> Misinterpretations { get; init; } = [];

    // Flags
    [JsonPropertyName("surprising_deltas")]
    public List<string> SurprisingDeltas { get; init; } = [];

    [JsonPropertyName("too_loud_visuals")]
    public List<string> TooLoudVisuals { get; init; } = [];

    [JsonPropertyName("too_quiet_visuals")]
    public List<string> TooQuietVisuals { get; init; } = [];

    // Confusion tracking
    [JsonPropertyName("confused_agreement_with_quality")]
    public bool ConfusedAgreementWithQuality { get; init; }

    [JsonPropertyName("confused_convergence_with_collapse")]
    public bool ConfusedConvergenceWithCollapse { get; init; }

    // Comprehension checks
    [JsonPropertyName("delta_zone_comprehension")]
    public ComprehensionLevel DeltaZoneComprehension { get; init; }

    [JsonPropertyName("summary_sentence_accuracy")]
    public ComprehensionLevel SummarySentenceAccuracy { get; init; }

    // Free-form notes
    [JsonPropertyName("notes")]
    public string Notes { get; init; } = "";
}

/// <summary>
/// Comprehension assessment level.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComprehensionLevel
{
    NotAssessed,
    FullyUnderstood,
    PartiallyUnderstood,
    Misunderstood,
    DidNotEngage
}

/// <summary>
/// Phase 3.1: Suppression & Noise Audit Record
/// Documents threshold sensitivity and false positive/negative analysis.
/// </summary>
public record SuppressionAuditRecord
{
    [JsonPropertyName("pair_id")]
    public required string PairId { get; init; }

    [JsonPropertyName("delta_id")]
    public required string DeltaId { get; init; }

    [JsonPropertyName("audit_type")]
    public SuppressionAuditType AuditType { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    // Threshold info
    [JsonPropertyName("threshold_name")]
    public string ThresholdName { get; init; } = "";

    [JsonPropertyName("threshold_value")]
    public double ThresholdValue { get; init; }

    [JsonPropertyName("actual_value")]
    public double ActualValue { get; init; }

    [JsonPropertyName("margin")]
    public double Margin { get; init; }

    // Sensitivity sweep (if performed)
    [JsonPropertyName("sensitivity_sweep")]
    public List<SensitivitySweepPoint>? SensitivitySweep { get; init; }

    // Classification
    [JsonPropertyName("is_false_positive")]
    public bool? IsFalsePositive { get; init; }

    [JsonPropertyName("is_false_negative")]
    public bool? IsFalseNegative { get; init; }

    [JsonPropertyName("is_borderline")]
    public bool IsBorderline { get; init; }

    // Recommendations
    [JsonPropertyName("recommendation")]
    public string Recommendation { get; init; } = "";

    [JsonPropertyName("suggested_threshold")]
    public double? SuggestedThreshold { get; init; }
}

/// <summary>
/// Type of suppression audit finding.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuppressionAuditType
{
    /// <summary>Delta should have suppressed but didn't.</summary>
    ShouldSuppressButDidnt,

    /// <summary>Delta suppressed but shouldn't have.</summary>
    SuppressedButShouldnt,

    /// <summary>Borderline case requiring review.</summary>
    Borderline,

    /// <summary>Threshold sensitivity finding.</summary>
    ThresholdSensitivity,

    /// <summary>Noise floor validation.</summary>
    NoiseFloorValidation
}

/// <summary>
/// Point in a threshold sensitivity sweep.
/// </summary>
public record SensitivitySweepPoint
{
    [JsonPropertyName("threshold")]
    public double Threshold { get; init; }

    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("delta_value")]
    public double DeltaValue { get; init; }
}
