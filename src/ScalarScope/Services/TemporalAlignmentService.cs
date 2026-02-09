using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Handles temporal alignment between two runs.
/// Comparison without alignment is noise.
/// Phase 3: Make Comparison the Star
/// </summary>
public static class TemporalAlignmentService
{
    /// <summary>
    /// Get the default alignment mode.
    /// </summary>
    public static TemporalAlignment DefaultAlignment => TemporalAlignment.ByStep;

    /// <summary>
    /// Get all supported alignment modes with descriptions.
    /// </summary>
    public static IReadOnlyList<AlignmentOption> GetAlignmentOptions() =>
    [
        new("ByStep", "By Step", "Align by training step (epoch)"),
        new("ByConvergence", "By Convergence", "Align when paths stabilize"),
        new("ByFirstInstability", "By First Change", "Align at first major shift")
    ];

    /// <summary>
    /// Map a time from one run to aligned time in another run.
    /// </summary>
    public static double MapTime(
        double sourceTime,
        GeometryRun? sourceRun,
        GeometryRun? targetRun,
        TemporalAlignment alignment)
    {
        if (sourceRun == null || targetRun == null)
            return sourceTime;

        return alignment switch
        {
            TemporalAlignment.ByStep => MapByStep(sourceTime, sourceRun, targetRun),
            TemporalAlignment.ByConvergence => MapByConvergence(sourceTime, sourceRun, targetRun),
            TemporalAlignment.ByFirstInstability => MapByFirstInstability(sourceTime, sourceRun, targetRun),
            _ => sourceTime
        };
    }

    /// <summary>
    /// Get alignment anchors for visualization.
    /// </summary>
    public static AlignmentAnchors GetAnchors(
        GeometryRun? leftRun,
        GeometryRun? rightRun,
        TemporalAlignment alignment)
    {
        if (leftRun == null || rightRun == null)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = 0,
                RightAnchor = 0,
                AnchorDescription = "No data"
            };
        }

        return alignment switch
        {
            TemporalAlignment.ByStep => new AlignmentAnchors
            {
                LeftAnchor = 0,
                RightAnchor = 0,
                AnchorDescription = "Aligned by training step"
            },
            TemporalAlignment.ByConvergence => GetConvergenceAnchors(leftRun, rightRun),
            TemporalAlignment.ByFirstInstability => GetInstabilityAnchors(leftRun, rightRun),
            _ => new AlignmentAnchors
            {
                LeftAnchor = 0,
                RightAnchor = 0,
                AnchorDescription = "Unknown alignment"
            }
        };
    }

    // === Private Methods ===

    /// <summary>
    /// Direct step-to-step mapping (normalized time).
    /// </summary>
    private static double MapByStep(double time, GeometryRun source, GeometryRun target)
    {
        // Both runs use normalized time [0, 1], so no transformation needed
        return Math.Clamp(time, 0, 1);
    }

    /// <summary>
    /// Map times relative to convergence point.
    /// </summary>
    private static double MapByConvergence(double time, GeometryRun source, GeometryRun target)
    {
        var sourceConvergence = FindConvergenceTime(source);
        var targetConvergence = FindConvergenceTime(target);

        // If neither converges, fall back to step alignment
        if (sourceConvergence < 0 || targetConvergence < 0)
            return time;

        // Map time relative to convergence
        // If source time is at convergence, target time should be at target convergence
        var relativeToConvergence = time - sourceConvergence;
        var mappedTime = targetConvergence + relativeToConvergence;

        return Math.Clamp(mappedTime, 0, 1);
    }

    /// <summary>
    /// Map times relative to first instability.
    /// </summary>
    private static double MapByFirstInstability(double time, GeometryRun source, GeometryRun target)
    {
        var sourceInstability = FindFirstInstabilityTime(source);
        var targetInstability = FindFirstInstabilityTime(target);

        // If neither has instability, fall back to step alignment
        if (sourceInstability < 0 || targetInstability < 0)
            return time;

        // Map time relative to first instability
        var relativeToInstability = time - sourceInstability;
        var mappedTime = targetInstability + relativeToInstability;

        return Math.Clamp(mappedTime, 0, 1);
    }

    private static AlignmentAnchors GetConvergenceAnchors(GeometryRun left, GeometryRun right)
    {
        var leftConvergence = FindConvergenceTime(left);
        var rightConvergence = FindConvergenceTime(right);

        if (leftConvergence < 0 && rightConvergence < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = 0.5,
                RightAnchor = 0.5,
                AnchorDescription = "Neither path converged; using midpoint"
            };
        }

        if (leftConvergence < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = rightConvergence,
                RightAnchor = rightConvergence,
                AnchorDescription = $"Path B converges at {rightConvergence:P0}; Path A did not converge"
            };
        }

        if (rightConvergence < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = leftConvergence,
                RightAnchor = leftConvergence,
                AnchorDescription = $"Path A converges at {leftConvergence:P0}; Path B did not converge"
            };
        }

        return new AlignmentAnchors
        {
            LeftAnchor = leftConvergence,
            RightAnchor = rightConvergence,
            AnchorDescription = $"Aligned at convergence (A: {leftConvergence:P0}, B: {rightConvergence:P0})"
        };
    }

    private static AlignmentAnchors GetInstabilityAnchors(GeometryRun left, GeometryRun right)
    {
        var leftInstability = FindFirstInstabilityTime(left);
        var rightInstability = FindFirstInstabilityTime(right);

        if (leftInstability < 0 && rightInstability < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = 0.1,
                RightAnchor = 0.1,
                AnchorDescription = "Neither path showed instability; using early point"
            };
        }

        if (leftInstability < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = rightInstability,
                RightAnchor = rightInstability,
                AnchorDescription = $"Path B shifts at {rightInstability:P0}; Path A remained stable"
            };
        }

        if (rightInstability < 0)
        {
            return new AlignmentAnchors
            {
                LeftAnchor = leftInstability,
                RightAnchor = leftInstability,
                AnchorDescription = $"Path A shifts at {leftInstability:P0}; Path B remained stable"
            };
        }

        return new AlignmentAnchors
        {
            LeftAnchor = leftInstability,
            RightAnchor = rightInstability,
            AnchorDescription = $"Aligned at first shift (A: {leftInstability:P0}, B: {rightInstability:P0})"
        };
    }

    /// <summary>
    /// Find when trajectory velocity stabilizes below threshold.
    /// </summary>
    private static double FindConvergenceTime(GeometryRun run)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count < 10)
            return -1;

        const double velocityThreshold = 0.05;
        const int stableWindowSize = 5;
        int stableCount = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            // Use velocity magnitude, not the vector
            if (steps[i].VelocityMagnitude < velocityThreshold)
            {
                stableCount++;
                if (stableCount >= stableWindowSize)
                {
                    return (i - stableWindowSize + 1) / (double)(steps.Count - 1);
                }
            }
            else
            {
                stableCount = 0;
            }
        }

        return -1;
    }

    /// <summary>
    /// Find first significant curvature spike.
    /// </summary>
    private static double FindFirstInstabilityTime(GeometryRun run)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count < 3)
            return -1;

        // Compute mean curvature for threshold
        var meanCurvature = steps.Average(s => s.Curvature);
        var threshold = Math.Max(meanCurvature * 2, 0.3);

        for (int i = 1; i < steps.Count; i++)
        {
            if (steps[i].Curvature > threshold)
            {
                return i / (double)(steps.Count - 1);
            }
        }

        return -1;
    }
}

/// <summary>
/// Temporal alignment modes for comparison.
/// </summary>
public enum TemporalAlignment
{
    /// <summary>Align by training step (epoch). Default.</summary>
    ByStep,
    
    /// <summary>Align when paths stabilize (convergence onset).</summary>
    ByConvergence,
    
    /// <summary>Align at first major direction change.</summary>
    ByFirstInstability
}

/// <summary>
/// Describes an alignment option for the UI.
/// </summary>
public record AlignmentOption(string Value, string Label, string Description);

/// <summary>
/// Anchor points for alignment visualization.
/// </summary>
public record AlignmentAnchors
{
    public double LeftAnchor { get; init; }
    public double RightAnchor { get; init; }
    public string AnchorDescription { get; init; } = "";
}
