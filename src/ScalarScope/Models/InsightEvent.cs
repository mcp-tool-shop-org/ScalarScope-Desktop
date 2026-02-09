namespace ScalarScope.Models;

/// <summary>
/// Phase 4: Unified insight event model.
/// Represents both Phase 2 training events and Phase 3 delta detections.
/// Used by InsightFeedService for consistent teaching across the app.
/// </summary>
public record InsightEvent
{
    /// <summary>Unique stable ID for persistence (e.g., "insight.delta.td.recurrence").</summary>
    public required string Id { get; init; }
    
    /// <summary>Category of insight.</summary>
    public required InsightCategory Category { get; init; }
    
    /// <summary>Short title for display (≤5 words).</summary>
    public required string Title { get; init; }
    
    /// <summary>One-line description (≤12 words).</summary>
    public required string Description { get; init; }
    
    /// <summary>Why this insight fired (condition met).</summary>
    public string? WhyFired { get; init; }
    
    /// <summary>Trigger type for categorization (e.g., "recurrence", "persistence_weighted").</summary>
    public string? TriggerType { get; init; }
    
    /// <summary>Key parameter values at runtime (for Why? panel).</summary>
    public Dictionary<string, string> Parameters { get; init; } = new();
    
    /// <summary>Confidence level (0-1) if applicable.</summary>
    public double? Confidence { get; init; }
    
    /// <summary>Visual hint for locating in UI.</summary>
    public string? VisualHint { get; init; }
    
    /// <summary>Normalized time [0,1] for anchor.</summary>
    public double? AnchorTime { get; init; }
    
    /// <summary>Target view for navigation (e.g., "compare", "trajectory").</summary>
    public string? TargetView { get; init; }
    
    /// <summary>Delta ID if this is a delta insight.</summary>
    public string? DeltaId { get; init; }
    
    /// <summary>Timestamp when this insight was generated.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    /// <summary>Misinterpretation guardrail text (shown on first encounter).</summary>
    public string? Guardrail { get; init; }
    
    /// <summary>Icon for tray display.</summary>
    public string Icon => Category switch
    {
        InsightCategory.DeltaFailure => "⚠",
        InsightCategory.DeltaConvergence => "⏱",
        InsightCategory.DeltaEmergence => "◈",
        InsightCategory.DeltaAlignment => "↔",
        InsightCategory.DeltaStability => "〰",
        InsightCategory.TrainingEvent => "💡",
        _ => "•"
    };
    
    /// <summary>Color accent for display.</summary>
    public string AccentColor => Category switch
    {
        InsightCategory.DeltaFailure => "#ff6b6b",
        InsightCategory.DeltaConvergence => "#4ecdc4",
        InsightCategory.DeltaEmergence => "#a29bfe",
        InsightCategory.DeltaAlignment => "#ffd93d",
        InsightCategory.DeltaStability => "#74b9ff",
        InsightCategory.TrainingEvent => "#00d9ff",
        _ => "#888"
    };
}

/// <summary>
/// Category of insight for filtering and presentation.
/// </summary>
public enum InsightCategory
{
    /// <summary>Phase 2 training event (curvature, eigenvalue, etc.).</summary>
    TrainingEvent,
    
    /// <summary>ΔF - Failure detection.</summary>
    DeltaFailure,
    
    /// <summary>ΔTc - Convergence timing.</summary>
    DeltaConvergence,
    
    /// <summary>ΔTd - Structural emergence.</summary>
    DeltaEmergence,
    
    /// <summary>ΔĀ - Evaluator alignment.</summary>
    DeltaAlignment,
    
    /// <summary>ΔO - Stability/oscillation.</summary>
    DeltaStability
}
