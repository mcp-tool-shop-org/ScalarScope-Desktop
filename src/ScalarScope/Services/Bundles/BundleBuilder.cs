// ComparisonBundle v1.0.0 Builder
// Constructs bundle payloads from delta computation results.

using System.Text;
using ScalarScope.Models;

namespace ScalarScope.Services.Bundles;

/// <summary>
/// Builds ComparisonBundle v1.0.0 payloads from computation results.
/// </summary>
public static class BundleBuilder
{
    /// <summary>Current bundle spec version.</summary>
    public const string BundleVersion = "1.0.0";
    
    /// <summary>Bundle file extension.</summary>
    public const string BundleExtension = ".scbundle";
    
    /// <summary>Required files for all profiles.</summary>
    public static readonly IReadOnlyList<string> RequiredFiles = new[]
    {
        "manifest.json",
        "repro/repro.json",
        "findings/deltas.json",
        "findings/why.json",
        "findings/summary.md"
    };
    
    /// <summary>
    /// Build a complete bundle payload from computation results.
    /// </summary>
    public static ComparisonBundlePayload Build(
        DeltaComputationResult result,
        BundleProfile profile,
        string labelA,
        string labelB,
        ImportPreset? preset = null,
        List<InsightEvent>? insights = null,
        List<BundleAsset>? assets = null,
        object? auditPayload = null,
        string? notes = null)
    {
        var bundleId = Guid.NewGuid();
        var comparisonId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        
        // Build all payloads
        var deltas = BuildDeltasPayload(comparisonId, result);
        var why = BuildWhyPayload(comparisonId, result);
        var repro = BuildReproPayload(comparisonId, result, preset);
        var insightsPayload = profile != BundleProfile.Share && insights?.Count > 0
            ? BuildInsightsPayload(comparisonId, insights)
            : null;
        
        // Determine optional files
        var optionalFiles = new List<string>();
        if (insightsPayload != null) optionalFiles.Add("insights/insights.json");
        if (profile != BundleProfile.Share && assets?.Count > 0) optionalFiles.Add("assets/");
        if (profile == BundleProfile.Audit && auditPayload != null)
        {
            optionalFiles.Add("audit/audit.json");
        }
        
        // Build manifest (integrity will be populated during export)
        var manifest = new ComparisonBundleManifest
        {
            BundleVersion = BundleVersion,
            BundleId = bundleId,
            CreatedUtc = now,
            Profile = profile,
            
            App = new AppInfo
            {
                Name = "ScalarScope",
                AppVersion = AppSession.Version,
                DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
                Build = new BuildInfo
                {
                    GitCommit = null, // Could populate from build info
                    Channel = BuildChannel.Stable,
                    WarningsCount = 0
                }
            },
            
            Comparison = new ComparisonInfo
            {
                ComparisonId = comparisonId,
                LabelA = labelA,
                LabelB = labelB,
                AlignmentMode = MapAlignmentMode(result.Alignment.Mode),
                CompareLength = result.Alignment.CompareIndex.Length,
                Notes = notes
            },
            
            Reproducibility = new ReproducibilityInfo
            {
                Status = MapReproStatus(result.GetReproducibilityStatus()),
                Reasons = DetermineReproReasons(result)
            },
            
            Contents = new ContentsInfo
            {
                Required = RequiredFiles,
                Optional = optionalFiles
            },
            
            // Integrity populated during export
            Integrity = new IntegrityInfo
            {
                HashAlgorithm = HashAlgorithm.Sha256,
                Files = Array.Empty<FileIntegrityEntry>(),
                BundleHash = "",
                BundleHashDefinition = BundleHashAlgorithm.BundleHashDefinition
            },
            
            Privacy = new PrivacyInfo
            {
                ContainsRawRunData = false,
                ContainsPII = profile == BundleProfile.Audit,
                Redactions = new[] { PrivacyRedaction.None }
            }
        };
        
        // Generate summary markdown
        var summary = GenerateSummaryMarkdown(manifest, deltas, repro);
        
        return new ComparisonBundlePayload
        {
            Manifest = manifest,
            Repro = repro,
            Deltas = deltas,
            Why = why,
            SummaryMarkdown = summary,
            Insights = insightsPayload,
            Assets = profile != BundleProfile.Share ? assets : null,
            AuditPayload = profile == BundleProfile.Audit ? auditPayload : null
        };
    }
    
    #region Payload Builders
    
    private static DeltasPayload BuildDeltasPayload(Guid comparisonId, DeltaComputationResult result)
    {
        var deltas = result.Deltas?.Select(d => new DeltaEntry
        {
            Id = d.Id,
            Status = MapDeltaStatus(d.Status),
            Name = d.Name,
            Explanation = d.Explanation,
            SummarySentence = d.SummarySentence,
            TriggerType = MapTriggerType(d),
            Confidence = d.Confidence,
            DeltaValue = d.Delta,
            Units = d.Units,
            Notes = d.Notes?.Count > 0 ? d.Notes : null,
            Anchors = BuildAnchors(d),
            Debug = null // Optional debug info
        }).ToList() ?? new List<DeltaEntry>();
        
        return new DeltasPayload
        {
            ComparisonId = comparisonId,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            GeneratedUtc = DateTimeOffset.UtcNow,
            Deltas = deltas
        };
    }
    
    private static WhyPayload BuildWhyPayload(Guid comparisonId, DeltaComputationResult result)
    {
        var whyEntries = result.Deltas?
            .Where(d => d.Status == Services.DeltaStatus.Present)
            .Select(d => new WhyEntry
            {
                DeltaId = d.Id,
                WhyFired = d.Explanation,
                ConditionSummary = d.SummarySentence ?? d.Explanation,
                Guardrails = GetGuardrails(d),
                ParameterChips = GetParameterChips(d),
                Confidence = new WhyConfidenceInfo
                {
                    Value = d.Confidence,
                    Label = GetConfidenceLabel(d.Confidence),
                    Components = GetConfidenceComponents(d)
                },
                ShowMe = new ShowMeInfo
                {
                    PreferredTargetView = TargetView.ComparePaths,
                    AnchorIndex = 0
                }
            }).ToList() ?? new List<WhyEntry>();
        
        return new WhyPayload
        {
            ComparisonId = comparisonId,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            Why = whyEntries
        };
    }
    
    private static ReproPayload BuildReproPayload(
        Guid comparisonId, 
        DeltaComputationResult result,
        ImportPreset? preset)
    {
        return new ReproPayload
        {
            ComparisonId = comparisonId,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            
            Inputs = new InputsInfo
            {
                RunA = new RunInputInfo
                {
                    SourceType = RunSourceType.File,
                    SchemaVersion = "1.0.0",
                    Fingerprint = result.InputFingerprint ?? "",
                    NormalizedFingerprint = result.InputFingerprint ?? "",
                    FileName = null,
                    Bytes = null
                },
                RunB = new RunInputInfo
                {
                    SourceType = RunSourceType.File,
                    SchemaVersion = "1.0.0",
                    Fingerprint = result.InputFingerprint ?? "",
                    NormalizedFingerprint = result.InputFingerprint ?? "",
                    FileName = null,
                    Bytes = null
                }
            },
            
            Preset = new PresetInfo
            {
                PresetId = preset?.Id ?? "default",
                PresetVersion = "1.0.0",
                PresetHash = preset?.GetSettingsFingerprint() ?? "",
                Normalization = new NormalizationInfo
                {
                    InputNormalizerVersion = "1.0.0",
                    Rules = new[] { "stable_sort_keys", "float_rounding", "nan_normalization" }
                },
                AlignmentDefaults = new AlignmentDefaultsInfo
                {
                    Mode = MapAlignmentMode(result.Alignment.Mode)
                }
            },
            
            Determinism = new DeterminismInfo
            {
                Seed = DeterminismService.IsDeterministic ? DeterminismService.Seed : null,
                DeterminismFingerprint = DeterminismService.IsDeterministic 
                    ? BundleHashAlgorithm.ComputeSha256Hex($"seed:{DeterminismService.Seed}")
                    : null,
                DeterminismNotes = DeterminismService.IsDeterministic 
                    ? "Deterministic mode enabled" 
                    : null
            },
            
            Results = new ResultsInfo
            {
                DeltaHash = result.DeltaHash ?? "",
                DeltasCount = result.Deltas?.Count ?? 0,
                ComputedUtc = DateTimeOffset.UtcNow
            },
            
            Environment = new EnvironmentInfo
            {
                Os = Environment.OSVersion.ToString(),
                Dotnet = Environment.Version.ToString(),
                AppVersion = AppSession.Version
            }
        };
    }
    
    private static InsightsPayload BuildInsightsPayload(Guid comparisonId, List<InsightEvent> insights)
    {
        var events = insights.Select(i => new InsightEntry
        {
            Id = i.Id,
            Type = i.Category == InsightCategory.TrainingEvent 
                ? InsightEventType.TrainingEvent 
                : InsightEventType.Delta,
            Subtype = MapInsightSubtype(i),
            Title = i.Title,
            Body = i.Description,
            CreatedUtc = i.Timestamp,
            Dismissible = true,
            ShowMe = i.AnchorTime.HasValue ? new ShowMeInfo
            {
                PreferredTargetView = MapTargetView(i.TargetView),
                MarkerA = (int?)(i.AnchorTime * 100),
                RangeA = null
            } : null,
            Meta = new InsightMeta
            {
                TriggerType = !string.IsNullOrEmpty(i.TriggerType) 
                    ? ParseTriggerType(i.TriggerType) 
                    : null,
                Confidence = i.Confidence,
                DeltaId = i.DeltaId
            }
        }).ToList();
        
        return new InsightsPayload
        {
            ComparisonId = comparisonId,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            Events = events
        };
    }
    
    #endregion
    
    #region Helper Methods
    
    private static IReadOnlyList<DeltaAnchor>? BuildAnchors(CanonicalDelta d)
    {
        if (d.VisualAnchorTime <= 0) return null;
        
        return new[]
        {
            new DeltaAnchor
            {
                TargetView = TargetView.ComparePaths,
                MarkerA = (int)(d.VisualAnchorTime * 100),
                MarkerB = (int)(d.VisualAnchorTime * 100),
                CompareIndex = (int)(d.VisualAnchorTime * 100),
                RangeA = null,
                RangeB = null,
                Meta = null
            }
        };
    }
    
    private static IReadOnlyList<string> GetGuardrails(CanonicalDelta d)
    {
        return d.Id switch
        {
            "delta_a" => new[] { "Agreement ≠ correctness", "Persistence varies by domain" },
            "delta_td" => new[] { "Dominance ≠ collapse", "Factor emergence is expected" },
            "delta_tc" => new[] { "Speed ≠ quality", "Convergence timing varies" },
            "delta_o" => new[] { "Instability ≠ failure", "Some oscillation is normal" },
            "delta_f" => new[] { "Failure detection is heuristic", "Check underlying metrics" },
            _ => new[] { "Interpret in context" }
        };
    }
    
    private static IReadOnlyList<ParameterChip> GetParameterChips(CanonicalDelta d)
    {
        var chips = new List<ParameterChip>
        {
            new() { Key = "Left", Value = d.LeftValue.ToString("G4") },
            new() { Key = "Right", Value = d.RightValue.ToString("G4") },
            new() { Key = "Delta", Value = d.Delta.ToString("G4") }
        };
        
        if (!string.IsNullOrEmpty(d.Units))
        {
            chips.Add(new ParameterChip { Key = "Units", Value = d.Units });
        }
        
        return chips;
    }
    
    private static IReadOnlyList<ConfidenceComponent>? GetConfidenceComponents(CanonicalDelta d)
    {
        // Add delta-specific confidence components
        return d.Id switch
        {
            "delta_tc" when d.TcA.HasValue && d.TcB.HasValue => new[]
            {
                new ConfidenceComponent { Key = "TcA", Value = d.TcA.Value },
                new ConfidenceComponent { Key = "TcB", Value = d.TcB.Value },
                new ConfidenceComponent { Key = "DeltaSteps", Value = (double)(d.DeltaTcSteps ?? 0) }
            },
            "delta_td" when d.DominanceRatioK > 0 => new[]
            {
                new ConfidenceComponent { Key = "DominanceK", Value = d.DominanceRatioK ?? 0.0 },
                new ConfidenceComponent { Key = "Window", Value = d.WindowUsed ?? 0.0 }
            },
            _ => null
        };
    }
    
    private static string GetConfidenceLabel(double confidence)
    {
        return confidence switch
        {
            >= 0.8 => "High",
            >= 0.5 => "Medium",
            _ => "Low"
        };
    }
    
    private static AlignmentMode MapAlignmentMode(TemporalAlignment mode)
    {
        return mode switch
        {
            TemporalAlignment.ByStep => AlignmentMode.Step,
            TemporalAlignment.ByConvergence => AlignmentMode.ConvergenceOnset,
            TemporalAlignment.ByFirstInstability => AlignmentMode.FirstInstability,
            _ => AlignmentMode.Step
        };
    }
    
    private static DeltaStatus MapDeltaStatus(Services.DeltaStatus status)
    {
        return status switch
        {
            Services.DeltaStatus.Present => DeltaStatus.Present,
            Services.DeltaStatus.Suppressed => DeltaStatus.Suppressed,
            Services.DeltaStatus.Indeterminate => DeltaStatus.Indeterminate,
            _ => DeltaStatus.Indeterminate
        };
    }
    
    private static ReproStatus MapReproStatus(ReproducibilityStatus status)
    {
        return status.Level switch
        {
            ReproducibilityLevel.Reproducible => ReproStatus.Reproducible,
            ReproducibilityLevel.Modified => ReproStatus.Modified,
            ReproducibilityLevel.NonDeterministic => ReproStatus.Nondeterministic,
            _ => ReproStatus.Nondeterministic
        };
    }
    
    private static IReadOnlyList<ReproReason> DetermineReproReasons(DeltaComputationResult result)
    {
        var status = result.GetReproducibilityStatus();
        if (status.Level == ReproducibilityLevel.Reproducible)
        {
            return Array.Empty<ReproReason>();
        }
        
        // Parse reasons from status factors
        var reasons = new List<ReproReason>();
        var description = status.Description ?? "";
        foreach (var factor in status.Factors)
        {
            if (factor.Status != FactorStatus.Present)
            {
                if (factor.Name.Contains("input", StringComparison.OrdinalIgnoreCase))
                    reasons.Add(ReproReason.InputsChanged);
                else if (factor.Name.Contains("preset", StringComparison.OrdinalIgnoreCase))
                    reasons.Add(ReproReason.PresetChanged);
                else if (factor.Name.Contains("seed", StringComparison.OrdinalIgnoreCase))
                    reasons.Add(ReproReason.SeedChanged);
                else if (factor.Name.Contains("spec", StringComparison.OrdinalIgnoreCase))
                    reasons.Add(ReproReason.DeltaSpecChanged);
            }
        }
        
        if (reasons.Count == 0)
            reasons.Add(ReproReason.Unknown);
        
        return reasons;
    }
    
    private static TriggerType MapTriggerType(CanonicalDelta d)
    {
        return d.Id switch
        {
            "delta_f" => TriggerType.DesignVerified,
            "delta_td" => d.DominanceRatioK > 0.5 ? TriggerType.Sustained : TriggerType.Recurrence,
            "delta_a" => TriggerType.PersistenceWeighted,
            "delta_o" => TriggerType.AreaEpisode,
            "delta_tc" => TriggerType.ConfidenceHeuristic,
            _ => TriggerType.None
        };
    }
    
    private static string MapInsightSubtype(InsightEvent i)
    {
        return i.Category switch
        {
            InsightCategory.DeltaConvergence => "DeltaTc",
            InsightCategory.DeltaEmergence => "DeltaTd",
            InsightCategory.DeltaAlignment => "DeltaA",
            InsightCategory.DeltaStability => "DeltaO",
            InsightCategory.DeltaFailure => "DeltaF",
            InsightCategory.TrainingEvent => i.TriggerType ?? "Generic",
            _ => "Unknown"
        };
    }
    
    private static TargetView MapTargetView(string? view)
    {
        return view?.ToLowerInvariant() switch
        {
            "compare" or "comparepaths" => TargetView.ComparePaths,
            "trajectory" or "learningpath" => TargetView.LearningPath,
            "scalars" or "performancesignals" => TargetView.PerformanceSignals,
            "geometry" or "evaluatoralignment" => TargetView.EvaluatorAlignment,
            _ => TargetView.Global
        };
    }
    
    private static TriggerType? ParseTriggerType(string triggerType)
    {
        return triggerType.ToLowerInvariant() switch
        {
            "sustained" => TriggerType.Sustained,
            "recurrence" => TriggerType.Recurrence,
            "persistence_weighted" => TriggerType.PersistenceWeighted,
            "area_episode" => TriggerType.AreaEpisode,
            "confidence_heuristic" => TriggerType.ConfidenceHeuristic,
            "design_verified" => TriggerType.DesignVerified,
            "event" => TriggerType.Event,
            _ => null
        };
    }
    
    #endregion
    
    #region Summary Generation
    
    private static string GenerateSummaryMarkdown(
        ComparisonBundleManifest manifest,
        DeltasPayload deltas,
        ReproPayload repro)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"**{manifest.Comparison.LabelA}** vs **{manifest.Comparison.LabelB}**");
        sb.AppendLine();
        sb.AppendLine("| Property | Value |");
        sb.AppendLine("|----------|-------|");
        sb.AppendLine($"| Alignment | {manifest.Comparison.AlignmentMode} |");
        sb.AppendLine($"| Compare Length | {manifest.Comparison.CompareLength} timesteps |");
        sb.AppendLine($"| Delta Spec | {manifest.App.DeltaSpecVersion} |");
        sb.AppendLine($"| Profile | {manifest.Profile} |");
        sb.AppendLine();
        
        var meaningful = deltas.Deltas.Where(d => d.Status == DeltaStatus.Present).ToList();
        
        sb.AppendLine("## Deltas Found");
        sb.AppendLine();
        
        if (meaningful.Count > 0)
        {
            foreach (var delta in meaningful)
            {
                sb.AppendLine($"### {delta.Name}");
                sb.AppendLine();
                sb.AppendLine($"*{delta.Explanation}*");
                sb.AppendLine();
                sb.AppendLine($"- **Confidence:** {delta.Confidence:P0}");
                if (delta.DeltaValue.HasValue)
                {
                    sb.AppendLine($"- **Delta:** {delta.DeltaValue:G4} {delta.Units ?? ""}");
                }
                sb.AppendLine($"- **Trigger:** {delta.TriggerType}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(delta.SummarySentence))
                {
                    sb.AppendLine($"> {delta.SummarySentence}");
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("No meaningful deltas detected.");
            sb.AppendLine();
        }
        
        sb.AppendLine("## Reproducibility");
        sb.AppendLine();
        sb.AppendLine($"- **Status:** {repro.Results.DeltaHash[..Math.Min(12, repro.Results.DeltaHash.Length)]}...");
        sb.AppendLine($"- **Determinism:** {(repro.Determinism.Seed.HasValue ? $"Seed {repro.Determinism.Seed}" : "Disabled")}");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(manifest.Comparison.Notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine(manifest.Comparison.Notes);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine($"*Generated by ScalarScope v{manifest.App.AppVersion}*");
        
        return sb.ToString();
    }
    
    #endregion
}
