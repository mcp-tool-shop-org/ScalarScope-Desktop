using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Alignment mapping utilities for Phase 3 delta detection.
/// Creates mapping between compare indices and run steps.
/// </summary>
public static class AlignmentMapper
{
    /// <summary>
    /// Create alignment map between two runs.
    /// </summary>
    public static AlignmentMap CreateAlignmentMap(
        GeometryRun? runA,
        GeometryRun? runB,
        TemporalAlignment mode)
    {
        var stepsA = runA?.Trajectory?.Timesteps?.Count ?? 0;
        var stepsB = runB?.Trajectory?.Timesteps?.Count ?? 0;

        if (stepsA == 0 || stepsB == 0)
        {
            return new AlignmentMap
            {
                Mode = mode,
                IdxToStepA = [],
                IdxToStepB = [],
                CompareIndex = [],
                Description = "No data available"
            };
        }

        return mode switch
        {
            TemporalAlignment.ByStep => CreateStepAlignment(stepsA, stepsB),
            TemporalAlignment.ByConvergence => CreateConvergenceAlignment(runA!, runB!, stepsA, stepsB),
            TemporalAlignment.ByFirstInstability => CreateInstabilityAlignment(runA!, runB!, stepsA, stepsB),
            _ => CreateStepAlignment(stepsA, stepsB)
        };
    }

    /// <summary>
    /// Direct step-to-step alignment (normalized).
    /// </summary>
    private static AlignmentMap CreateStepAlignment(int stepsA, int stepsB)
    {
        var maxSteps = Math.Max(stepsA, stepsB);
        var idxToStepA = new int?[maxSteps];
        var idxToStepB = new int?[maxSteps];
        var compareIndex = new int[maxSteps];

        for (int i = 0; i < maxSteps; i++)
        {
            compareIndex[i] = i;
            
            // Map normalized position
            var normalizedPos = i / (double)(maxSteps - 1);
            
            idxToStepA[i] = (int)(normalizedPos * (stepsA - 1));
            idxToStepB[i] = (int)(normalizedPos * (stepsB - 1));
        }

        return new AlignmentMap
        {
            Mode = TemporalAlignment.ByStep,
            IdxToStepA = idxToStepA,
            IdxToStepB = idxToStepB,
            CompareIndex = compareIndex,
            Description = "Aligned by training step"
        };
    }

    /// <summary>
    /// Align by convergence onset.
    /// </summary>
    private static AlignmentMap CreateConvergenceAlignment(
        GeometryRun runA, GeometryRun runB, int stepsA, int stepsB)
    {
        var tcA = FindConvergenceStep(runA);
        var tcB = FindConvergenceStep(runB);

        // If neither converges, fall back to step alignment
        if (tcA < 0 && tcB < 0)
        {
            var fallback = CreateStepAlignment(stepsA, stepsB);
            return fallback with { Description = "Neither path converged; using step alignment" };
        }

        // Use convergence points as anchors
        var anchorA = tcA >= 0 ? tcA : stepsA / 2;
        var anchorB = tcB >= 0 ? tcB : stepsB / 2;

        return CreateAnchoredAlignment(stepsA, stepsB, anchorA, anchorB,
            $"Aligned at convergence (A: step {anchorA}, B: step {anchorB})");
    }

    /// <summary>
    /// Align by first instability.
    /// </summary>
    private static AlignmentMap CreateInstabilityAlignment(
        GeometryRun runA, GeometryRun runB, int stepsA, int stepsB)
    {
        var tiA = FindFirstInstabilityStep(runA);
        var tiB = FindFirstInstabilityStep(runB);

        // If neither has instability, fall back to step alignment
        if (tiA < 0 && tiB < 0)
        {
            var fallback = CreateStepAlignment(stepsA, stepsB);
            return fallback with { Description = "Neither path showed instability; using step alignment" };
        }

        var anchorA = tiA >= 0 ? tiA : stepsA / 4;
        var anchorB = tiB >= 0 ? tiB : stepsB / 4;

        return CreateAnchoredAlignment(stepsA, stepsB, anchorA, anchorB,
            $"Aligned at first change (A: step {anchorA}, B: step {anchorB})");
    }

    /// <summary>
    /// Create alignment map with specific anchor points.
    /// </summary>
    private static AlignmentMap CreateAnchoredAlignment(
        int stepsA, int stepsB, int anchorA, int anchorB, string description)
    {
        // Determine compare range: start at 0, anchor at same compare index, extend to max
        var preAnchorA = anchorA;
        var preAnchorB = anchorB;
        var postAnchorA = stepsA - 1 - anchorA;
        var postAnchorB = stepsB - 1 - anchorB;

        var preCompare = Math.Max(preAnchorA, preAnchorB);
        var postCompare = Math.Max(postAnchorA, postAnchorB);
        var totalCompare = preCompare + 1 + postCompare;

        var idxToStepA = new int?[totalCompare];
        var idxToStepB = new int?[totalCompare];
        var compareIndex = new int[totalCompare];

        for (int i = 0; i < totalCompare; i++)
        {
            compareIndex[i] = i;

            // Distance from anchor in compare space
            var distFromAnchor = i - preCompare;

            // Map to A
            var stepA = anchorA + distFromAnchor;
            idxToStepA[i] = (stepA >= 0 && stepA < stepsA) ? stepA : null;

            // Map to B
            var stepB = anchorB + distFromAnchor;
            idxToStepB[i] = (stepB >= 0 && stepB < stepsB) ? stepB : null;
        }

        return new AlignmentMap
        {
            Mode = TemporalAlignment.ByConvergence, // or ByFirstInstability
            IdxToStepA = idxToStepA,
            IdxToStepB = idxToStepB,
            CompareIndex = compareIndex,
            Description = description
        };
    }

    /// <summary>
    /// Find convergence step (velocity stabilizes).
    /// </summary>
    private static int FindConvergenceStep(GeometryRun run)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count < 10) return -1;

        // Use velocity magnitude, not vector
        var velocities = steps.Select(s => s.VelocityMagnitude).ToList();
        var sigma = RobustSigma(velocities);
        var epsilon = Math.Max(0.02, sigma * 0.5);

        const int window = 5;

        for (int i = window; i < steps.Count - window; i++)
        {
            var baseValue = velocities[i];
            bool stable = true;

            for (int j = 1; j <= window && i + j < steps.Count; j++)
            {
                if (Math.Abs(velocities[i + j] - baseValue) >= epsilon)
                {
                    stable = false;
                    break;
                }
            }

            if (stable) return i;
        }

        return -1;
    }
    /// <summary>
    /// Find first instability step (curvature spike).
    /// </summary>
    private static int FindFirstInstabilityStep(GeometryRun run)
    {
        var steps = run.Trajectory?.Timesteps;
        if (steps == null || steps.Count < 3) return -1;

        var curvatures = steps.Select(s => s.Curvature).ToList();
        var mean = curvatures.Average();
        var threshold = Math.Max(mean * 2, 0.3);

        for (int i = 1; i < steps.Count; i++)
        {
            if (curvatures[i] > threshold)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Compute robust sigma using MAD (Median Absolute Deviation).
    /// </summary>
    public static double RobustSigma(IList<double> values)
    {
        if (values.Count < 5) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var median = sorted[sorted.Count / 2];
        
        var deviations = values.Select(v => Math.Abs(v - median)).OrderBy(d => d).ToList();
        var mad = deviations[deviations.Count / 2];
        
        return 1.4826 * mad; // Scale factor for normal distribution
    }

    /// <summary>
    /// Extract aligned series from a run's data.
    /// </summary>
    public static double?[] ExtractAlignedSeries(
        IList<double> values,
        int?[] idxToStep)
    {
        var result = new double?[idxToStep.Length];
        
        for (int i = 0; i < idxToStep.Length; i++)
        {
            var step = idxToStep[i];
            if (step.HasValue && step.Value >= 0 && step.Value < values.Count)
            {
                result[i] = values[step.Value];
            }
            else
            {
                result[i] = null;
            }
        }
        
        return result;
    }

    /// <summary>
    /// Find sustained segments where condition is true.
    /// </summary>
    public static List<(int Start, int End)> FindSustained(bool?[] condition, int minLen)
    {
        var segments = new List<(int, int)>();
        int i = 0;

        while (i < condition.Length)
        {
            if (condition[i] == true)
            {
                int start = i;
                while (i < condition.Length && condition[i] == true) i++;
                int end = i - 1;
                
                if (end - start + 1 >= minLen)
                {
                    segments.Add((start, end));
                }
            }
            else
            {
                i++;
            }
        }

        return segments;
    }

    /// <summary>
    /// Apply moving average smoothing.
    /// </summary>
    public static double?[] MovingAverage(double?[] values, int window)
    {
        var result = new double?[values.Length];
        var halfWindow = window / 2;

        for (int i = 0; i < values.Length; i++)
        {
            double sum = 0;
            int count = 0;

            for (int j = Math.Max(0, i - halfWindow); j <= Math.Min(values.Length - 1, i + halfWindow); j++)
            {
                if (values[j].HasValue)
                {
                    sum += values[j]!.Value;
                    count++;
                }
            }

            result[i] = count > 0 ? sum / count : null;
        }

        return result;
    }
}
