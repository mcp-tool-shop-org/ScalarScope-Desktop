using System.Text.Json.Serialization;

namespace ScalarScope.Services.Evidence;

/// <summary>
/// Phase 3.1: Complete Evidence Report
/// A reproducible bundle containing all evidence for one comparison pair.
/// This is the top-level artifact produced by Phase 3.1.
/// </summary>
public record ComparisonEvidenceReport
{
    // Identity section
    [JsonPropertyName("report_id")]
    public required string ReportId { get; init; }

    [JsonPropertyName("report_version")]
    public string ReportVersion { get; init; } = "1.0.0";

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("generator_version")]
    public string GeneratorVersion { get; init; } = "";

    // Pair info
    [JsonPropertyName("pair_definition")]
    public required ComparisonPairDefinition PairDefinition { get; init; }

    [JsonPropertyName("config_snapshot")]
    public required ConfigSnapshot ConfigSnapshot { get; init; }

    // Raw signals section (§2 in checklist)
    [JsonPropertyName("run_a_signals")]
    public required RawSignalExport RunASignals { get; init; }

    [JsonPropertyName("run_b_signals")]
    public required RawSignalExport RunBSignals { get; init; }

    // Detector diagnostics section (§3 in checklist)
    [JsonPropertyName("detector_diagnostics")]
    public required DetectorDiagnostics DetectorDiagnostics { get; init; }

    // Alignment mapping section (§4 in checklist)
    [JsonPropertyName("alignment_diagnostics")]
    public required AlignmentMappingDiagnostics AlignmentDiagnostics { get; init; }

    // Visual anchor verification section (§5 in checklist)
    [JsonPropertyName("visual_verifications")]
    public required List<VisualAnchorVerification> VisualVerifications { get; init; }

    // UX interpretation section (§6 in checklist)
    [JsonPropertyName("ux_assessments")]
    public List<UXInterpretationAssessment> UXAssessments { get; init; } = [];

    // Suppression audit section (§7 in checklist)
    [JsonPropertyName("suppression_audits")]
    public List<SuppressionAuditRecord> SuppressionAudits { get; init; } = [];

    // Summary section
    [JsonPropertyName("summary")]
    public required ReportSummary Summary { get; init; }

    // Exit gate checks
    [JsonPropertyName("exit_gate")]
    public required ExitGateStatus ExitGate { get; init; }
}

/// <summary>
/// Snapshot of configuration at report generation time.
/// </summary>
public record ConfigSnapshot
{
    [JsonPropertyName("config_hash")]
    public required string ConfigHash { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // Convergence thresholds
    [JsonPropertyName("convergence_epsilon")]
    public double ConvergenceEpsilon { get; init; }

    [JsonPropertyName("convergence_window")]
    public int ConvergenceWindow { get; init; }

    // Emergence thresholds
    [JsonPropertyName("emergence_dominance_k")]
    public double EmergenceDominanceK { get; init; }

    [JsonPropertyName("emergence_persistence")]
    public int EmergencePersistence { get; init; }

    // Alignment thresholds
    [JsonPropertyName("alignment_noise_floor")]
    public double AlignmentNoiseFloor { get; init; }

    // Stability thresholds
    [JsonPropertyName("stability_theta")]
    public double StabilityTheta { get; init; }

    [JsonPropertyName("stability_window")]
    public int StabilityWindow { get; init; }

    // Suppression config
    [JsonPropertyName("suppression_enabled")]
    public bool SuppressionEnabled { get; init; }

    [JsonPropertyName("delta_floor")]
    public double DeltaFloor { get; init; }

    // Visual config
    [JsonPropertyName("visual_intensity_scale")]
    public double VisualIntensityScale { get; init; }

    // Full config JSON for audit trail
    [JsonPropertyName("full_config_json")]
    public string FullConfigJson { get; init; } = "";
}

/// <summary>
/// High-level summary of the report findings.
/// </summary>
public record ReportSummary
{
    [JsonPropertyName("pair_id")]
    public required string PairId { get; init; }

    [JsonPropertyName("category")]
    public ComparisonCategory Category { get; init; }

    // Delta counts
    [JsonPropertyName("total_deltas")]
    public int TotalDeltas { get; init; }

    [JsonPropertyName("significant_deltas")]
    public int SignificantDeltas { get; init; }

    [JsonPropertyName("suppressed_deltas")]
    public int SuppressedDeltas { get; init; }

    // By type
    [JsonPropertyName("delta_breakdown")]
    public required Dictionary<string, DeltaSummaryEntry> DeltaBreakdown { get; init; }

    // Expectation matching
    [JsonPropertyName("expected_deltas_found")]
    public int ExpectedDeltasFound { get; init; }

    [JsonPropertyName("expected_deltas_missing")]
    public int ExpectedDeltasMissing { get; init; }

    [JsonPropertyName("unexpected_deltas_found")]
    public int UnexpectedDeltasFound { get; init; }

    // Quality metrics
    [JsonPropertyName("alignment_coverage")]
    public double AlignmentCoverage { get; init; }

    [JsonPropertyName("anchor_verification_pass_rate")]
    public double AnchorVerificationPassRate { get; init; }

    // Recommendations
    [JsonPropertyName("threshold_recommendations")]
    public List<ThresholdRecommendation> ThresholdRecommendations { get; init; } = [];

    // One-sentence summary
    [JsonPropertyName("narrative")]
    public string Narrative { get; init; } = "";
}

/// <summary>
/// Summary entry for a single delta type.
/// </summary>
public record DeltaSummaryEntry
{
    [JsonPropertyName("status")]
    public DeltaStatus Status { get; init; }

    [JsonPropertyName("magnitude")]
    public double Magnitude { get; init; }

    [JsonPropertyName("expected")]
    public bool Expected { get; init; }

    [JsonPropertyName("suppressed")]
    public bool Suppressed { get; init; }

    [JsonPropertyName("suppression_reason")]
    public string? SuppressionReason { get; init; }
}

/// <summary>
/// Threshold tuning recommendation based on evidence.
/// </summary>
public record ThresholdRecommendation
{
    [JsonPropertyName("threshold_name")]
    public required string ThresholdName { get; init; }

    [JsonPropertyName("current_value")]
    public double CurrentValue { get; init; }

    [JsonPropertyName("suggested_value")]
    public double SuggestedValue { get; init; }

    [JsonPropertyName("rationale")]
    public string Rationale { get; init; } = "";

    [JsonPropertyName("evidence_source")]
    public string EvidenceSource { get; init; } = "";

    [JsonPropertyName("confidence")]
    public RecommendationConfidence Confidence { get; init; }
}

/// <summary>
/// Confidence level for a threshold recommendation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
/// Exit gate status — must all pass for Phase 3.1 completion.
/// </summary>
public record ExitGateStatus
{
    [JsonPropertyName("all_passed")]
    public bool AllPassed => RawSignalsExported && DetectorDiagnosticsComplete &&
                             AlignmentDiagnosticsValid && VisualAnchorsVerified &&
                             SuppressionAuditComplete && ReportReproducible;

    [JsonPropertyName("raw_signals_exported")]
    public bool RawSignalsExported { get; init; }

    [JsonPropertyName("raw_signals_notes")]
    public string RawSignalsNotes { get; init; } = "";

    [JsonPropertyName("detector_diagnostics_complete")]
    public bool DetectorDiagnosticsComplete { get; init; }

    [JsonPropertyName("detector_diagnostics_notes")]
    public string DetectorDiagnosticsNotes { get; init; } = "";

    [JsonPropertyName("alignment_diagnostics_valid")]
    public bool AlignmentDiagnosticsValid { get; init; }

    [JsonPropertyName("alignment_diagnostics_notes")]
    public string AlignmentDiagnosticsNotes { get; init; } = "";

    [JsonPropertyName("visual_anchors_verified")]
    public bool VisualAnchorsVerified { get; init; }

    [JsonPropertyName("visual_anchors_notes")]
    public string VisualAnchorsNotes { get; init; } = "";

    [JsonPropertyName("suppression_audit_complete")]
    public bool SuppressionAuditComplete { get; init; }

    [JsonPropertyName("suppression_audit_notes")]
    public string SuppressionAuditNotes { get; init; } = "";

    [JsonPropertyName("report_reproducible")]
    public bool ReportReproducible { get; init; }

    [JsonPropertyName("reproducibility_notes")]
    public string ReproducibilityNotes { get; init; } = "";

    // Overall notes
    [JsonPropertyName("gate_notes")]
    public List<string> GateNotes { get; init; } = [];
}

/// <summary>
/// Collection of evidence reports for a full comparison set.
/// </summary>
public record ComparisonSetEvidenceBundle
{
    [JsonPropertyName("bundle_id")]
    public required string BundleId { get; init; }

    [JsonPropertyName("set_definition")]
    public required ComparisonSetDefinition SetDefinition { get; init; }

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("reports")]
    public required List<ComparisonEvidenceReport> Reports { get; init; }

    // Aggregate stats
    [JsonPropertyName("total_pairs")]
    public int TotalPairs => Reports.Count;

    [JsonPropertyName("all_gates_passed")]
    public bool AllGatesPassed => Reports.All(r => r.ExitGate.AllPassed);

    [JsonPropertyName("aggregate_recommendations")]
    public List<ThresholdRecommendation> AggregateRecommendations { get; init; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; init; } = "";
}
