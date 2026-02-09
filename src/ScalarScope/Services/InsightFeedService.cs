using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Phase 4: Unified insight feed service.
/// Collects and publishes teaching signals from both Phase 2 (training events) 
/// and Phase 3 (delta detections). Ensures "show once per session" semantics.
/// </summary>
public class InsightFeedService
{
    private static readonly Lazy<InsightFeedService> _instance = new(() => new InsightFeedService());
    public static InsightFeedService Instance => _instance.Value;

    private readonly List<InsightEvent> _insights = new();
    private readonly HashSet<string> _shownThisSession = new();
    private readonly object _lock = new();

    /// <summary>
    /// Fired when a new insight is published.
    /// </summary>
    public event Action<InsightEvent>? OnInsightPublished;

    /// <summary>
    /// Fired when the insights list changes.
    /// </summary>
    public event Action? OnInsightsChanged;

    /// <summary>
    /// All insights collected this session (reverse chronological).
    /// </summary>
    public IReadOnlyList<InsightEvent> Insights
    {
        get
        {
            lock (_lock)
            {
                return _insights.AsReadOnly();
            }
        }
    }

    /// <summary>
    /// Count of insights for UI binding.
    /// </summary>
    public int InsightCount => _insights.Count;

    /// <summary>
    /// Debug mode: logs all emitted insights.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Publish an insight event.
    /// Returns true if shown (first time), false if suppressed (already shown).
    /// </summary>
    public bool Publish(InsightEvent insight)
    {
        lock (_lock)
        {
            // Check session dismissal
            if (_shownThisSession.Contains(insight.Id))
            {
                if (DebugMode)
                    System.Diagnostics.Debug.WriteLine($"[InsightFeed] Suppressed (session): {insight.Id}");
                return false;
            }

            // Check permanent dismissal
            if (UserPreferencesService.IsHintDismissed(insight.Id))
            {
                if (DebugMode)
                    System.Diagnostics.Debug.WriteLine($"[InsightFeed] Suppressed (dismissed): {insight.Id}");
                return false;
            }

            _shownThisSession.Add(insight.Id);
            _insights.Insert(0, insight); // Most recent first

            if (DebugMode)
                System.Diagnostics.Debug.WriteLine($"[InsightFeed] Published: {insight.Id} - {insight.Title}");
        }

        OnInsightPublished?.Invoke(insight);
        OnInsightsChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Publish a delta insight from a CanonicalDelta.
    /// </summary>
    public bool PublishDelta(CanonicalDelta delta, string? triggerType = null)
    {
        var insight = CreateInsightFromDelta(delta, triggerType);
        return Publish(insight);
    }

    /// <summary>
    /// Publish a Phase 2 training event.
    /// </summary>
    public bool PublishTrainingEvent(ContextualExplanation explanation)
    {
        var insight = CreateInsightFromExplanation(explanation);
        return Publish(insight);
    }

    /// <summary>
    /// Dismiss an insight permanently.
    /// </summary>
    public void Dismiss(string insightId)
    {
        UserPreferencesService.DismissHint(insightId);
        if (DebugMode)
            System.Diagnostics.Debug.WriteLine($"[InsightFeed] Dismissed permanently: {insightId}");
    }

    /// <summary>
    /// Clear session state (for new comparison).
    /// </summary>
    public void ClearSession()
    {
        lock (_lock)
        {
            _insights.Clear();
            _shownThisSession.Clear();
        }
        OnInsightsChanged?.Invoke();
    }

    /// <summary>
    /// Reset all teaching hints (for Settings > Reset).
    /// </summary>
    public void ResetAllHints()
    {
        UserPreferencesService.ResetHints();
        lock (_lock)
        {
            _shownThisSession.Clear();
        }
    }

    /// <summary>
    /// Get insights filtered by category.
    /// </summary>
    public IReadOnlyList<InsightEvent> GetByCategory(InsightCategory? category)
    {
        lock (_lock)
        {
            if (category == null)
                return _insights.AsReadOnly();
            return _insights.Where(i => i.Category == category).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get deltas only.
    /// </summary>
    public IReadOnlyList<InsightEvent> GetDeltas()
    {
        lock (_lock)
        {
            return _insights.Where(i => i.Category != InsightCategory.TrainingEvent).ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// Get training events only.
    /// </summary>
    public IReadOnlyList<InsightEvent> GetTrainingEvents()
    {
        lock (_lock)
        {
            return _insights.Where(i => i.Category == InsightCategory.TrainingEvent).ToList().AsReadOnly();
        }
    }

    private static InsightEvent CreateInsightFromDelta(CanonicalDelta delta, string? triggerType)
    {
        var category = delta.Id switch
        {
            "delta_f" => InsightCategory.DeltaFailure,
            "delta_tc" => InsightCategory.DeltaConvergence,
            "delta_td" => InsightCategory.DeltaEmergence,
            "delta_a" => InsightCategory.DeltaAlignment,
            "delta_o" => InsightCategory.DeltaStability,
            _ => InsightCategory.TrainingEvent
        };

        var parameters = new Dictionary<string, string>();
        var whyFired = BuildWhyFired(delta, triggerType, parameters);
        var guardrail = GetGuardrail(delta.Id);

        return new InsightEvent
        {
            Id = $"insight.delta.{delta.Id}.{triggerType ?? "default"}",
            Category = category,
            Title = delta.Name,
            Description = delta.Explanation,
            WhyFired = whyFired,
            TriggerType = triggerType,
            Parameters = parameters,
            Confidence = delta.Confidence,
            VisualHint = GetVisualHint(delta),
            AnchorTime = delta.VisualAnchorTime,
            TargetView = "compare",
            DeltaId = delta.Id,
            Guardrail = guardrail
        };
    }

    private static string BuildWhyFired(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        return delta.Id switch
        {
            "delta_f" => BuildFailureWhy(delta, triggerType, parameters),
            "delta_tc" => BuildConvergenceWhy(delta, triggerType, parameters),
            "delta_td" => BuildEmergenceWhy(delta, triggerType, parameters),
            "delta_a" => BuildAlignmentWhy(delta, triggerType, parameters),
            "delta_o" => BuildStabilityWhy(delta, triggerType, parameters),
            _ => delta.Explanation
        };
    }

    private static string BuildFailureWhy(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        var sb = new System.Text.StringBuilder();
        
        if (delta.FailedA == true && delta.FailedB != true)
        {
            sb.Append($"Path A failed at step {delta.TFailA}");
            if (!string.IsNullOrEmpty(delta.FailureKindA))
                sb.Append($" ({delta.FailureKindA})");
            sb.Append(", Path B completed normally.");
        }
        else if (delta.FailedB == true && delta.FailedA != true)
        {
            sb.Append($"Path B failed at step {delta.TFailB}");
            if (!string.IsNullOrEmpty(delta.FailureKindB))
                sb.Append($" ({delta.FailureKindB})");
            sb.Append(", Path A completed normally.");
        }
        else if (delta.FailedA == true && delta.FailedB == true)
        {
            sb.Append($"Both paths failed (A: {delta.TFailA}, B: {delta.TFailB})");
        }
        else
        {
            sb.Append("Failure detected via proxy (divergence or collapse pattern).");
        }

        if (triggerType != null)
            parameters["Detection"] = triggerType;
        if (delta.TFailA.HasValue)
            parameters["FailStepA"] = delta.TFailA.Value.ToString();
        if (delta.TFailB.HasValue)
            parameters["FailStepB"] = delta.TFailB.Value.ToString();

        return sb.ToString();
    }

    private static string BuildConvergenceWhy(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        var stepDiff = delta.DeltaTcSteps ?? 0;
        var direction = stepDiff > 0 ? "later" : "earlier";
        var sb = new System.Text.StringBuilder();

        if (delta.TcA.HasValue && delta.TcB.HasValue)
        {
            sb.Append($"Path A converged at step {delta.TcA}, Path B at step {delta.TcB}. ");
            sb.Append($"Path B converged {Math.Abs(stepDiff)} steps {direction}.");
        }
        else if (delta.TcA.HasValue && !delta.TcB.HasValue)
        {
            sb.Append($"Path A converged at step {delta.TcA}. Path B did not converge.");
        }
        else if (!delta.TcA.HasValue && delta.TcB.HasValue)
        {
            sb.Append($"Path A did not converge. Path B converged at step {delta.TcB}.");
        }

        parameters["ResolutionSteps"] = "3";
        if (delta.TcA.HasValue)
            parameters["TcA"] = delta.TcA.Value.ToString();
        if (delta.TcB.HasValue)
            parameters["TcB"] = delta.TcB.Value.ToString();
        if (delta.EpsilonUsed.HasValue)
            parameters["Epsilon"] = delta.EpsilonUsed.Value.ToString("F4");
        if (delta.ConvergenceConfidence.HasValue)
            parameters["Confidence"] = $"{delta.ConvergenceConfidence.Value:P0}";

        return sb.ToString();
    }

    private static string BuildEmergenceWhy(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        var sb = new System.Text.StringBuilder();

        if (delta.TdA.HasValue && delta.TdB.HasValue)
        {
            var diff = delta.TdB.Value - delta.TdA.Value;
            var direction = diff > 0 ? "later" : "earlier";
            sb.Append($"Dominant structure emerged at step {delta.TdA} (A) vs {delta.TdB} (B). ");
            sb.Append($"Path B: {Math.Abs(diff)} steps {direction}.");
        }
        else if (delta.TdA.HasValue && !delta.TdB.HasValue)
        {
            sb.Append($"Path A developed dominant structure at step {delta.TdA}. Path B did not.");
        }
        else if (!delta.TdA.HasValue && delta.TdB.HasValue)
        {
            sb.Append($"Path A did not develop dominant structure. Path B did at step {delta.TdB}.");
        }

        if (triggerType != null)
        {
            parameters["TriggerType"] = triggerType;
            sb.Append(triggerType == "recurrence" ? " (Recurrent dominance pattern)" : " (Sustained dominance)");
        }

        if (delta.DominanceRatioK.HasValue)
            parameters["DominanceK"] = $"{delta.DominanceRatioK.Value:F2}";
        parameters["RecurrenceWindow"] = "7";

        return sb.ToString();
    }

    private static string BuildAlignmentWhy(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        var sb = new System.Text.StringBuilder();

        if (delta.MeanAlignA.HasValue && delta.MeanAlignB.HasValue)
        {
            var diff = delta.MeanAlignB.Value - delta.MeanAlignA.Value;
            var better = diff > 0 ? "B" : "A";
            sb.Append($"Mean evaluator alignment: A={delta.MeanAlignA.Value:F3}, B={delta.MeanAlignB.Value:F3}. ");
            sb.Append($"Path {better} showed higher agreement.");
        }

        if (triggerType == "persistence_weighted")
        {
            sb.Append(" (Persistence-weighted: final 25% at 2× importance)");
            parameters["Weighting"] = "persistence_weighted";
        }

        parameters["DualGate"] = "persist<0.05 AND raw<0.10";
        if (delta.MeanAlignA.HasValue)
            parameters["AlignA"] = delta.MeanAlignA.Value.ToString("F3");
        if (delta.MeanAlignB.HasValue)
            parameters["AlignB"] = delta.MeanAlignB.Value.ToString("F3");

        return sb.ToString();
    }

    private static string BuildStabilityWhy(CanonicalDelta delta, string? triggerType, Dictionary<string, string> parameters)
    {
        var sb = new System.Text.StringBuilder();

        if (delta.ScoreA.HasValue && delta.ScoreB.HasValue)
        {
            var higher = delta.ScoreB > delta.ScoreA ? "B" : "A";
            sb.Append($"Oscillation score: A={delta.ScoreA.Value:F2}, B={delta.ScoreB.Value:F2}. ");
            sb.Append($"Path {higher} showed more instability.");
        }

        if (triggerType == "area_episode")
        {
            sb.Append(" (Area-above-θ scoring)");
            parameters["Scoring"] = "area_episode";
        }

        parameters["MinDuration"] = "4";
        parameters["ThetaSigmaMultiplier"] = "1.0";
        if (delta.ThresholdUsed.HasValue)
            parameters["Threshold"] = delta.ThresholdUsed.Value.ToString("F3");

        return sb.ToString();
    }

    private static string GetVisualHint(CanonicalDelta delta)
    {
        return delta.Id switch
        {
            "delta_f" => "Look for the failure marker (⚠) on the trajectory",
            "delta_tc" => "Compare convergence markers (◆) on both paths",
            "delta_td" => "Watch for dominance bands in the Geometry view",
            "delta_a" => "Check the alignment drift segments on the overlay",
            "delta_o" => "Notice oscillation episodes highlighted on the path",
            _ => "Hover over highlighted regions for details"
        };
    }

    private static string? GetGuardrail(string deltaId)
    {
        return deltaId switch
        {
            "delta_a" => "Agreement ≠ correctness.",
            "delta_td" => "Dominance ≠ collapse.",
            "delta_o" => "Instability ≠ failure.",
            _ => null
        };
    }

    private static InsightEvent CreateInsightFromExplanation(ContextualExplanation explanation)
    {
        return new InsightEvent
        {
            Id = $"insight.training.{explanation.Type.ToString().ToLowerInvariant()}",
            Category = InsightCategory.TrainingEvent,
            Title = explanation.Title,
            Description = explanation.Message,
            VisualHint = explanation.VisualHint,
            TargetView = "trajectory"
        };
    }
}
