// ComparisonBundle v1.0.0 Models
// Strict record models matching the field-by-field manifest spec.
// Compatible with System.Text.Json serialization with camelCase naming.

using System.Text.Json.Serialization;

namespace ScalarScope.Services.Bundles;

#region Enums

/// <summary>Bundle export profile (share|review|audit).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BundleProfile
{
    [JsonPropertyName("share")] Share,
    [JsonPropertyName("review")] Review,
    [JsonPropertyName("audit")] Audit
}

/// <summary>Build channel.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BuildChannel
{
    [JsonPropertyName("stable")] Stable,
    [JsonPropertyName("dev")] Dev,
    [JsonPropertyName("ci")] Ci
}

/// <summary>Temporal alignment mode.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AlignmentMode
{
    [JsonPropertyName("step")] Step,
    [JsonPropertyName("convergenceOnset")] ConvergenceOnset,
    [JsonPropertyName("firstInstability")] FirstInstability
}

/// <summary>Reproducibility status.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReproStatus
{
    [JsonPropertyName("reproducible")] Reproducible,
    [JsonPropertyName("modified")] Modified,
    [JsonPropertyName("nondeterministic")] Nondeterministic
}

/// <summary>Reproducibility reason flags.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReproReason
{
    [JsonPropertyName("inputs_changed")] InputsChanged,
    [JsonPropertyName("preset_changed")] PresetChanged,
    [JsonPropertyName("seed_changed")] SeedChanged,
    [JsonPropertyName("delta_spec_changed")] DeltaSpecChanged,
    [JsonPropertyName("unknown")] Unknown
}

/// <summary>Hash algorithm (currently only SHA-256).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HashAlgorithm
{
    [JsonPropertyName("SHA-256")] Sha256
}

/// <summary>Privacy redaction types.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PrivacyRedaction
{
    [JsonPropertyName("none")] None,
    [JsonPropertyName("labels_only")] LabelsOnly,
    [JsonPropertyName("paths_removed")] PathsRemoved,
    [JsonPropertyName("other")] Other
}

/// <summary>Run source type.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RunSourceType
{
    [JsonPropertyName("file")] File,
    [JsonPropertyName("bundle")] Bundle,
    [JsonPropertyName("demo")] Demo,
    [JsonPropertyName("unknown")] Unknown
}

/// <summary>Delta status.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeltaStatus
{
    [JsonPropertyName("present")] Present,
    [JsonPropertyName("suppressed")] Suppressed,
    [JsonPropertyName("indeterminate")] Indeterminate
}

/// <summary>Delta trigger type (Phase 3.2 spec).</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TriggerType
{
    [JsonPropertyName("design_verified")] DesignVerified,
    [JsonPropertyName("sustained")] Sustained,
    [JsonPropertyName("recurrence")] Recurrence,
    [JsonPropertyName("persistence_weighted")] PersistenceWeighted,
    [JsonPropertyName("area_episode")] AreaEpisode,
    [JsonPropertyName("confidence_heuristic")] ConfidenceHeuristic,
    [JsonPropertyName("event")] Event,
    [JsonPropertyName("none")] None
}

/// <summary>Target view for anchors.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TargetView
{
    [JsonPropertyName("ComparePaths")] ComparePaths,
    [JsonPropertyName("LearningPath")] LearningPath,
    [JsonPropertyName("PerformanceSignals")] PerformanceSignals,
    [JsonPropertyName("EvaluatorAlignment")] EvaluatorAlignment,
    [JsonPropertyName("Global")] Global
}

/// <summary>Insight event type.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InsightEventType
{
    [JsonPropertyName("delta")] Delta,
    [JsonPropertyName("training_event")] TrainingEvent
}

#endregion

#region manifest.json - Root Manifest

/// <summary>
/// Root manifest.json schema (ComparisonBundle v1.0.0).
/// Single source of truth for bundle contents, integrity, and metadata.
/// </summary>
public sealed record ComparisonBundleManifest
{
    public required string BundleVersion { get; init; }         // "1.0.0"
    public required Guid BundleId { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required BundleProfile Profile { get; init; }
    
    public required AppInfo App { get; init; }
    public required ComparisonInfo Comparison { get; init; }
    public required ReproducibilityInfo Reproducibility { get; init; }
    public required ContentsInfo Contents { get; init; }
    public required IntegrityInfo Integrity { get; init; }
    public required PrivacyInfo Privacy { get; init; }
}

/// <summary>Application info block.</summary>
public sealed record AppInfo
{
    public required string Name { get; init; }                  // "ScalarScope"
    public required string AppVersion { get; init; }            // e.g. "1.5.0"
    public required string DeltaSpecVersion { get; init; }      // e.g. "3.2.0"
    public required BuildInfo Build { get; init; }
}

/// <summary>Build metadata.</summary>
public sealed record BuildInfo
{
    public string? GitCommit { get; init; }
    public required BuildChannel Channel { get; init; }
    public required int WarningsCount { get; init; }
}

/// <summary>Comparison session info.</summary>
public sealed record ComparisonInfo
{
    public required Guid ComparisonId { get; init; }
    public required string LabelA { get; init; }
    public required string LabelB { get; init; }
    public required AlignmentMode AlignmentMode { get; init; }
    public required int CompareLength { get; init; }
    public string? Notes { get; init; }
}

/// <summary>Reproducibility status block.</summary>
public sealed record ReproducibilityInfo
{
    public required ReproStatus Status { get; init; }
    public required IReadOnlyList<ReproReason> Reasons { get; init; }
}

/// <summary>Contents manifest (what's in the bundle).</summary>
public sealed record ContentsInfo
{
    public required IReadOnlyList<string> Required { get; init; }
    public required IReadOnlyList<string> Optional { get; init; }
}

/// <summary>Integrity verification block.</summary>
public sealed record IntegrityInfo
{
    public required HashAlgorithm HashAlgorithm { get; init; }
    public required IReadOnlyList<FileIntegrityEntry> Files { get; init; }
    public required string BundleHash { get; init; }            // 64 hex chars
    public required string BundleHashDefinition { get; init; }
}

/// <summary>Per-file integrity entry.</summary>
public sealed record FileIntegrityEntry
{
    public required string Path { get; init; }                  // zip-relative path
    public required string Sha256 { get; init; }                // 64 hex chars
    public required long Bytes { get; init; }
    public required string ContentType { get; init; }           // e.g. "application/json"
}

/// <summary>Privacy disclosure block.</summary>
public sealed record PrivacyInfo
{
    public required bool ContainsRawRunData { get; init; }
    public required bool ContainsPII { get; init; }
    public required IReadOnlyList<PrivacyRedaction> Redactions { get; init; }
}

#endregion

#region repro/repro.json - Reproducibility Payload

/// <summary>
/// repro/repro.json schema.
/// Sufficient info to prove what inputs were used and reproduce results.
/// </summary>
public sealed record ReproPayload
{
    public required Guid ComparisonId { get; init; }
    public required string DeltaSpecVersion { get; init; }
    
    public required InputsInfo Inputs { get; init; }
    public required PresetInfo Preset { get; init; }
    public required DeterminismInfo Determinism { get; init; }
    public required ResultsInfo Results { get; init; }
    public required EnvironmentInfo Environment { get; init; }
}

/// <summary>Input runs info.</summary>
public sealed record InputsInfo
{
    public required RunInputInfo RunA { get; init; }
    public required RunInputInfo RunB { get; init; }
}

/// <summary>Single run input metadata.</summary>
public sealed record RunInputInfo
{
    public required RunSourceType SourceType { get; init; }
    public required string SchemaVersion { get; init; }
    public required string Fingerprint { get; init; }           // hex
    public required string NormalizedFingerprint { get; init; } // hex
    public string? FileName { get; init; }
    public long? Bytes { get; init; }
}

/// <summary>Preset/normalization config.</summary>
public sealed record PresetInfo
{
    public required string PresetId { get; init; }
    public required string PresetVersion { get; init; }
    public required string PresetHash { get; init; }            // hex
    public required NormalizationInfo Normalization { get; init; }
    public required AlignmentDefaultsInfo AlignmentDefaults { get; init; }
}

/// <summary>Normalization rules applied.</summary>
public sealed record NormalizationInfo
{
    public required string InputNormalizerVersion { get; init; }
    public required IReadOnlyList<string> Rules { get; init; }
}

/// <summary>Default alignment settings.</summary>
public sealed record AlignmentDefaultsInfo
{
    public required AlignmentMode Mode { get; init; }
}

/// <summary>Determinism configuration.</summary>
public sealed record DeterminismInfo
{
    public int? Seed { get; init; }
    public string? DeterminismFingerprint { get; init; }
    public string? DeterminismNotes { get; init; }
}

/// <summary>Computation results summary.</summary>
public sealed record ResultsInfo
{
    public required string DeltaHash { get; init; }             // hex
    public required int DeltasCount { get; init; }
    public required DateTimeOffset ComputedUtc { get; init; }
}

/// <summary>Runtime environment info.</summary>
public sealed record EnvironmentInfo
{
    public required string Os { get; init; }
    public required string Dotnet { get; init; }
    public required string AppVersion { get; init; }
}

#endregion

#region findings/deltas.json - Delta Results Payload

/// <summary>
/// findings/deltas.json schema.
/// Render Compare mode findings without computation.
/// </summary>
public sealed record DeltasPayload
{
    public required Guid ComparisonId { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public required DateTimeOffset GeneratedUtc { get; init; }
    public required IReadOnlyList<DeltaEntry> Deltas { get; init; }
}

/// <summary>Single delta entry.</summary>
public sealed record DeltaEntry
{
    public required string Id { get; init; }
    public required DeltaStatus Status { get; init; }
    public required string Name { get; init; }
    public required string Explanation { get; init; }
    public string? SummarySentence { get; init; }
    public required TriggerType TriggerType { get; init; }
    public required double Confidence { get; init; }
    public double? DeltaValue { get; init; }
    public string? Units { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }
    
    public IReadOnlyList<DeltaAnchor>? Anchors { get; init; }
    public DeltaDebugInfo? Debug { get; init; }
}

/// <summary>Delta visual anchor for "Show me" behavior.</summary>
public sealed record DeltaAnchor
{
    public required TargetView TargetView { get; init; }
    public int? MarkerA { get; init; }
    public int? MarkerB { get; init; }
    public TimeRange? RangeA { get; init; }
    public TimeRange? RangeB { get; init; }
    public int? CompareIndex { get; init; }
    public Dictionary<string, string>? Meta { get; init; }
}

/// <summary>Time range (t0-t1).</summary>
public sealed record TimeRange
{
    public required int T0 { get; init; }
    public required int T1 { get; init; }
}

/// <summary>Debug info for delta (optional).</summary>
public sealed record DeltaDebugInfo
{
    public string? DeltaHashFragment { get; init; }
    public string? DetectorVersion { get; init; }
}

#endregion

#region findings/why.json - Why Panel Payload

/// <summary>
/// findings/why.json schema.
/// Render "Why?" panel without recomputing detector internals.
/// </summary>
public sealed record WhyPayload
{
    public required Guid ComparisonId { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public required IReadOnlyList<WhyEntry> Why { get; init; }
}

/// <summary>Single Why explanation entry.</summary>
public sealed record WhyEntry
{
    public required string DeltaId { get; init; }
    public required string WhyFired { get; init; }
    public required string ConditionSummary { get; init; }
    public required IReadOnlyList<string> Guardrails { get; init; }
    public required IReadOnlyList<ParameterChip> ParameterChips { get; init; }
    public required WhyConfidenceInfo Confidence { get; init; }
    public ShowMeInfo? ShowMe { get; init; }
}

/// <summary>Parameter key-value chip.</summary>
public sealed record ParameterChip
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}

/// <summary>Confidence info with breakdown.</summary>
public sealed record WhyConfidenceInfo
{
    public required double Value { get; init; }
    public required string Label { get; init; }
    public IReadOnlyList<ConfidenceComponent>? Components { get; init; }
}

/// <summary>Confidence component breakdown.</summary>
public sealed record ConfidenceComponent
{
    public required string Key { get; init; }
    public required double Value { get; init; }
}

/// <summary>Show me navigation info.</summary>
public sealed record ShowMeInfo
{
    public required TargetView PreferredTargetView { get; init; }
    public int? AnchorIndex { get; init; }
    public int? MarkerA { get; init; }
    public TimeRange? RangeA { get; init; }
}

#endregion

#region insights/insights.json - Insight Feed Payload

/// <summary>
/// insights/insights.json schema.
/// Seed the Insights tray in Review Mode.
/// </summary>
public sealed record InsightsPayload
{
    public required Guid ComparisonId { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public required IReadOnlyList<InsightEntry> Events { get; init; }
}

/// <summary>Single insight event.</summary>
public sealed record InsightEntry
{
    public required string Id { get; init; }
    public required InsightEventType Type { get; init; }
    public required string Subtype { get; init; }               // e.g. "DeltaTc", "CurvatureSpike"
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public bool Dismissible { get; init; } = true;
    public ShowMeInfo? ShowMe { get; init; }
    public InsightMeta? Meta { get; init; }
}

/// <summary>Insight metadata.</summary>
public sealed record InsightMeta
{
    public TriggerType? TriggerType { get; init; }
    public double? Confidence { get; init; }
    public string? DeltaId { get; init; }
}

#endregion

#region Bundle Assembly Helper

/// <summary>
/// Complete bundle payload container for export.
/// </summary>
public sealed record ComparisonBundlePayload
{
    public required ComparisonBundleManifest Manifest { get; init; }
    public required ReproPayload Repro { get; init; }
    public required DeltasPayload Deltas { get; init; }
    public required WhyPayload Why { get; init; }
    public required string SummaryMarkdown { get; init; }
    public InsightsPayload? Insights { get; init; }
    public IReadOnlyList<BundleAsset>? Assets { get; init; }
    public object? AuditPayload { get; init; }                  // ReproAuditBundle or raw JSON
}

/// <summary>Asset to include in bundle.</summary>
public sealed record BundleAsset
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required byte[] Data { get; init; }
}

#endregion
