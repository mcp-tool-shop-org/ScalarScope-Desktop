namespace ScalarScope.Services;

/// <summary>
/// Provides timed demo annotations that explain what's happening during the guided demo.
/// Annotations are minimal, non-intrusive, and focus on the key theoretical insights.
/// </summary>
public static class DemoAnnotationService
{
    /// <summary>
    /// Demo annotations keyed by time threshold.
    /// Each annotation appears once when playback crosses its threshold.
    /// </summary>
    private static readonly List<DemoAnnotation> Annotations =
    [
        new DemoAnnotation
        {
            Id = "demo_start",
            TimeThreshold = 0.0,
            Title = "Demo Starting",
            Message = "Both runs starting. Same training loop. Different evaluator geometry.",
            Duration = TimeSpan.FromSeconds(5),
            Emphasis = DemoEmphasis.Introduction
        },
        new DemoAnnotation
        {
            Id = "demo_diverging",
            TimeThreshold = 0.25,
            Title = "Paths Diverging",
            Message = "Notice: Path B is converging toward an attractor. Path A is wandering.",
            Duration = TimeSpan.FromSeconds(4),
            Emphasis = DemoEmphasis.Observation
        },
        new DemoAnnotation
        {
            Id = "demo_eigenvalues",
            TimeThreshold = 0.50,
            Title = "Eigenvalue Structure",
            Message = "Watch the eigenvalue bars. Path B shows dominance. Path A stays distributed.",
            Duration = TimeSpan.FromSeconds(4),
            Emphasis = DemoEmphasis.Observation
        },
        new DemoAnnotation
        {
            Id = "demo_convergence",
            TimeThreshold = 0.75,
            Title = "Convergence Emerging",
            Message = "λ₁ dominance = shared evaluative axis. Path B has found the manifold.",
            Duration = TimeSpan.FromSeconds(4),
            Emphasis = DemoEmphasis.KeyInsight
        },
        new DemoAnnotation
        {
            Id = "demo_complete",
            TimeThreshold = 0.95,
            Title = "Demo Complete",
            Message = "Path B converged (correlated professors). Path A stayed distributed (orthogonal professors).",
            Duration = TimeSpan.FromSeconds(6),
            Emphasis = DemoEmphasis.Conclusion
        }
    ];

    /// <summary>
    /// Track which annotations have been shown in the current demo session.
    /// </summary>
    private static readonly HashSet<string> ShownAnnotations = new();

    /// <summary>
    /// Currently displayed annotation (if any).
    /// </summary>
    public static DemoAnnotation? CurrentAnnotation { get; private set; }

    /// <summary>
    /// Event fired when a new annotation should be displayed.
    /// </summary>
    public static event Action<DemoAnnotation>? AnnotationTriggered;

    /// <summary>
    /// Event fired when the current annotation should be dismissed.
    /// </summary>
    public static event Action? AnnotationDismissed;

    /// <summary>
    /// Reset the annotation state for a new demo session.
    /// </summary>
    public static void ResetForNewDemo()
    {
        ShownAnnotations.Clear();
        CurrentAnnotation = null;
    }

    /// <summary>
    /// Check if any annotation should be triggered at the given playback time.
    /// Call this from the playback timer.
    /// </summary>
    public static void CheckTimeThreshold(double t)
    {
        if (!DemoService.IsDemoActive) return;

        foreach (var annotation in Annotations)
        {
            // Skip if already shown
            if (ShownAnnotations.Contains(annotation.Id)) continue;

            // Check if we've crossed the threshold
            if (t >= annotation.TimeThreshold)
            {
                TriggerAnnotation(annotation);
                break; // Only one annotation at a time
            }
        }
    }

    /// <summary>
    /// Trigger a specific annotation.
    /// </summary>
    private static void TriggerAnnotation(DemoAnnotation annotation)
    {
        ShownAnnotations.Add(annotation.Id);
        CurrentAnnotation = annotation;
        AnnotationTriggered?.Invoke(annotation);

        // Fire demo completed event when we show the final annotation
        if (annotation.Id == "demo_complete")
        {
            DemoCompleted?.Invoke();
        }

        // Auto-dismiss after duration
        Task.Delay(annotation.Duration).ContinueWith(_ =>
        {
            if (CurrentAnnotation?.Id == annotation.Id)
            {
                CurrentAnnotation = null;
                AnnotationDismissed?.Invoke();
            }
        });
    }

    /// <summary>
    /// Manually dismiss the current annotation.
    /// </summary>
    public static void DismissCurrent()
    {
        CurrentAnnotation = null;
        AnnotationDismissed?.Invoke();
    }

    /// <summary>
    /// Get all annotations for display in a timeline or list.
    /// </summary>
    public static IReadOnlyList<DemoAnnotation> GetAllAnnotations() => Annotations;

    /// <summary>
    /// Check if the demo has completed (all annotations shown and past final threshold).
    /// </summary>
    public static bool IsDemoComplete => ShownAnnotations.Contains("demo_complete");

    /// <summary>
    /// Event fired when the demo reaches completion (final annotation shown).
    /// </summary>
    public static event Action? DemoCompleted;
}

/// <summary>
/// A single demo annotation with timing and display information.
/// </summary>
public record DemoAnnotation
{
    public required string Id { get; init; }
    public required double TimeThreshold { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required TimeSpan Duration { get; init; }
    public DemoEmphasis Emphasis { get; init; } = DemoEmphasis.Observation;
}

/// <summary>
/// Visual emphasis for demo annotations.
/// </summary>
public enum DemoEmphasis
{
    Introduction,   // Welcome / setup context
    Observation,    // "Notice this..."
    KeyInsight,     // Core theoretical claim
    Conclusion      // Summary / wrap-up
}
