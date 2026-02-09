using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Phase 2: Contextual explanation service.
/// Triggers educational tooltips based on training events.
/// Each tooltip type shows only once per session.
/// </summary>
public class ContextualExplanationService
{
    private static readonly Lazy<ContextualExplanationService> _instance = new(() => new ContextualExplanationService());
    public static ContextualExplanationService Instance => _instance.Value;

    // Track which explanations have been shown this session
    private readonly HashSet<ExplanationType> _shownExplanations = new();

    /// <summary>
    /// Event fired when an explanation should be shown.
    /// </summary>
    public event Action<ContextualExplanation>? OnExplanationTriggered;

    /// <summary>
    /// Check trajectory for events and trigger appropriate explanations.
    /// Called when playback time changes.
    /// </summary>
    public void CheckForEvents(GeometryRun? run, double currentTime, TrajectoryTimestep? currentStep)
    {
        if (run == null || currentStep == null) return;

        // First curvature spike
        if (!_shownExplanations.Contains(ExplanationType.CurvatureSpike))
        {
            if (currentStep.Curvature > ConsistencyCheckService.HighCurvatureThreshold)
            {
                TriggerExplanation(new ContextualExplanation
                {
                    Type = ExplanationType.CurvatureSpike,
                    Title = "Phase Transition Detected",
                    Message = "The model is revising its internal structure. High curvature means rapid belief changes.",
                    VisualHint = "Notice the tight turn in the trajectory."
                });
            }
        }

        // First eigenvalue dominance
        if (!_shownExplanations.Contains(ExplanationType.EigenvalueDominance))
        {
            var eigenIdx = (int)(currentTime * (run.Geometry.Eigenvalues.Count - 1));
            eigenIdx = Math.Clamp(eigenIdx, 0, Math.Max(0, run.Geometry.Eigenvalues.Count - 1));
            
            if (eigenIdx < run.Geometry.Eigenvalues.Count)
            {
                var eigen = run.Geometry.Eigenvalues[eigenIdx];
                var total = eigen.Values.Sum();
                var firstFactor = total > 0 && eigen.Values.Count > 0 ? eigen.Values[0] / total : 0;

                if (firstFactor > 0.6)
                {
                    TriggerExplanation(new ContextualExplanation
                    {
                        Type = ExplanationType.EigenvalueDominance,
                        Title = "Shared Structure Emerging",
                        Message = "One evaluation dimension is becoming dominant. The model may be developing unified criteria.",
                        VisualHint = "Watch the tallest bar in the Geometry view."
                    });
                }
            }
        }

        // First oscillation (velocity direction changes)
        if (!_shownExplanations.Contains(ExplanationType.Oscillation))
        {
            // Check for oscillation by looking at recent velocity changes
            var steps = run.Trajectory.Timesteps;
            var currentIdx = (int)(currentTime * (steps.Count - 1));
            
            if (currentIdx > 10)
            {
                int directionChanges = 0;
                for (int i = currentIdx - 9; i < currentIdx; i++)
                {
                    if (i <= 0 || i >= steps.Count - 1) continue;
                    var prev = steps[i - 1].Velocity;
                    var curr = steps[i].Velocity;
                    var next = steps[i + 1].Velocity;
                    
                    if (prev.Count >= 2 && curr.Count >= 2 && next.Count >= 2)
                    {
                        var dot1 = prev[0] * curr[0] + prev[1] * curr[1];
                        var dot2 = curr[0] * next[0] + curr[1] * next[1];
                        if (dot1 < 0 || dot2 < 0) directionChanges++;
                    }
                }

                if (directionChanges >= 3)
                {
                    TriggerExplanation(new ContextualExplanation
                    {
                        Type = ExplanationType.Oscillation,
                        Title = "Learning Oscillation",
                        Message = "The model is bouncing between different solutions. This may stabilize or indicate conflicting signals.",
                        VisualHint = "See the zigzag pattern in recent motion."
                    });
                }
            }
        }

        // First stagnation (very low velocity for extended period)
        if (!_shownExplanations.Contains(ExplanationType.Stagnation))
        {
            var steps = run.Trajectory.Timesteps;
            var currentIdx = (int)(currentTime * (steps.Count - 1));
            
            if (currentIdx > 20)
            {
                double avgVelocityMag = 0;
                int count = 0;
                for (int i = Math.Max(0, currentIdx - 20); i <= currentIdx && i < steps.Count; i++)
                {
                    var vel = steps[i].Velocity;
                    if (vel.Count >= 2)
                    {
                        avgVelocityMag += Math.Sqrt(vel[0] * vel[0] + vel[1] * vel[1]);
                        count++;
                    }
                }
                
                if (count > 0)
                {
                    avgVelocityMag /= count;
                    if (avgVelocityMag < 0.01)
                    {
                        TriggerExplanation(new ContextualExplanation
                        {
                            Type = ExplanationType.Stagnation,
                            Title = "Learning Plateau",
                            Message = "Progress has slowed significantly. The model may have reached a local optimum or needs different training signals.",
                            VisualHint = "Notice how the trajectory has nearly stopped moving."
                        });
                    }
                }
            }
        }
    }

    /// <summary>
    /// Manually trigger an explanation (for demo mode).
    /// </summary>
    public void ShowExplanation(ExplanationType type)
    {
        if (_shownExplanations.Contains(type)) return;

        var explanation = type switch
        {
            ExplanationType.CurvatureSpike => new ContextualExplanation
            {
                Type = type,
                Title = "Phase Transition",
                Message = "High curvature indicates rapid internal changes.",
                VisualHint = "Look for tight turns in the trajectory."
            },
            ExplanationType.EigenvalueDominance => new ContextualExplanation
            {
                Type = type,
                Title = "Shared Structure",
                Message = "A dominant eigenvalue suggests unified evaluation criteria.",
                VisualHint = "Watch the Geometry view for tall bars."
            },
            ExplanationType.Oscillation => new ContextualExplanation
            {
                Type = type,
                Title = "Oscillation",
                Message = "Zigzag motion indicates competing solutions.",
                VisualHint = "Notice the back-and-forth pattern."
            },
            ExplanationType.Stagnation => new ContextualExplanation
            {
                Type = type,
                Title = "Plateau",
                Message = "Progress has slowed, possibly at a local optimum.",
                VisualHint = "The trajectory appears stationary."
            },
            _ => null
        };

        if (explanation != null)
        {
            TriggerExplanation(explanation);
        }
    }

    /// <summary>
    /// Reset shown explanations (for new session or testing).
    /// </summary>
    public void Reset()
    {
        _shownExplanations.Clear();
    }

    private void TriggerExplanation(ContextualExplanation explanation)
    {
        _shownExplanations.Add(explanation.Type);
        OnExplanationTriggered?.Invoke(explanation);
    }
}

public enum ExplanationType
{
    CurvatureSpike,
    EigenvalueDominance,
    Oscillation,
    Stagnation
}

public record ContextualExplanation
{
    public required ExplanationType Type { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string VisualHint { get; init; }
}
