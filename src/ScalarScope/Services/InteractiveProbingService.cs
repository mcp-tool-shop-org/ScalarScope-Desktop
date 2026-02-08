using ScalarScope.Models;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Service for interactive probing features.
/// Phase 3.3 - Time Travel, Gradient Inspector, What-If Markers
/// </summary>
public class InteractiveProbingService
{
    /// <summary>
    /// Get full state information at a specific trajectory point.
    /// Provides comprehensive analysis for time travel inspection.
    /// </summary>
    public TimeTravelState? GetStateAt(GeometryRun run, int timestepIndex)
    {
        var trajectory = run.Trajectory?.Timesteps;
        if (trajectory == null || timestepIndex < 0 || timestepIndex >= trajectory.Count)
            return null;

        var current = trajectory[timestepIndex];
        var prev = timestepIndex > 0 ? trajectory[timestepIndex - 1] : null;
        var next = timestepIndex < trajectory.Count - 1 ? trajectory[timestepIndex + 1] : null;

        // Compute velocity (direction of movement)
        (double vx, double vy) = (0, 0);
        if (prev != null && current.State2D?.Count >= 2 && prev.State2D?.Count >= 2)
        {
            var dt = current.T - prev.T;
            if (dt > 0)
            {
                vx = (current.State2D[0] - prev.State2D[0]) / dt;
                vy = (current.State2D[1] - prev.State2D[1]) / dt;
            }
        }

        // Compute acceleration (change in velocity)
        (double ax, double ay) = (0, 0);
        if (prev != null && next != null &&
            prev.State2D?.Count >= 2 && current.State2D?.Count >= 2 && next.State2D?.Count >= 2)
        {
            var dt1 = current.T - prev.T;
            var dt2 = next.T - current.T;
            if (dt1 > 0 && dt2 > 0)
            {
                var vx1 = (current.State2D[0] - prev.State2D[0]) / dt1;
                var vy1 = (current.State2D[1] - prev.State2D[1]) / dt1;
                var vx2 = (next.State2D[0] - current.State2D[0]) / dt2;
                var vy2 = (next.State2D[1] - current.State2D[1]) / dt2;

                ax = (vx2 - vx1) / ((dt1 + dt2) / 2);
                ay = (vy2 - vy1) / ((dt1 + dt2) / 2);
            }
        }

        // Get eigenvalue info at this time
        var eigenStep = run.Geometry.Eigenvalues
            .FirstOrDefault(e => Math.Abs(e.T - current.T) < 0.01);

        // Get scalar metrics at this time
        var scalarStep = run.Scalars.Values
            .FirstOrDefault(s => Math.Abs(s.T - current.T) < 0.01);

        // Extract eigenvalues from Values array
        double? lambda1 = eigenStep?.Values.Count > 0 ? eigenStep.Values[0] : null;
        double? lambda2 = eigenStep?.Values.Count > 1 ? eigenStep.Values[1] : null;
        double? effectiveDim = current.EffectiveDim;

        return new TimeTravelState
        {
            TimestepIndex = timestepIndex,
            Time = current.T,
            Position = current.State2D?.Count >= 2 ? (current.State2D[0], current.State2D[1]) : (0, 0),
            Velocity = (vx, vy),
            Speed = Math.Sqrt(vx * vx + vy * vy),
            Acceleration = (ax, ay),
            Curvature = current.Curvature,
            Lambda1 = lambda1,
            Lambda2 = lambda2,
            EffectiveDimension = effectiveDim,
            Loss = scalarStep != null ? (1.0 - scalarStep.Correctness) : null, // Approximate loss from correctness
            Accuracy = scalarStep?.Correctness,
            LearningRate = null, // Not available in current model
            GradientNorm = null, // Not available in current model
            IsAtBifurcation = current.Curvature > 0.5, // High curvature indicates phase transition
            DistanceFromOrigin = current.State2D?.Count >= 2 
                ? Math.Sqrt(current.State2D[0] * current.State2D[0] + current.State2D[1] * current.State2D[1])
                : 0
        };
    }

    /// <summary>
    /// Find the nearest trajectory point to a position.
    /// Used for click-to-jump time travel.
    /// </summary>
    public int? FindNearestTimestep(GeometryRun run, double x, double y, double maxDistance = double.MaxValue)
    {
        var trajectory = run.Trajectory?.Timesteps;
        if (trajectory == null || trajectory.Count == 0)
            return null;

        int? nearestIndex = null;
        var minDist = maxDistance;

        for (int i = 0; i < trajectory.Count; i++)
        {
            var pt = trajectory[i];
            if (pt.State2D?.Count < 2) continue;

            var dx = pt.State2D[0] - x;
            var dy = pt.State2D[1] - y;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < minDist)
            {
                minDist = dist;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    /// <summary>
    /// Get gradient information at a specific point.
    /// Shows intended vs actual direction of movement.
    /// </summary>
    public GradientInspection? InspectGradient(GeometryRun run, int timestepIndex)
    {
        var trajectory = run.Trajectory?.Timesteps;
        if (trajectory == null || timestepIndex < 0 || timestepIndex >= trajectory.Count - 1)
            return null;

        var current = trajectory[timestepIndex];
        var next = trajectory[timestepIndex + 1];

        if (current.State2D?.Count < 2 || next.State2D?.Count < 2)
            return null;

        // Actual movement direction
        var actualDx = next.State2D[0] - current.State2D[0];
        var actualDy = next.State2D[1] - current.State2D[1];
        var actualMag = Math.Sqrt(actualDx * actualDx + actualDy * actualDy);

        // Get professors to estimate intended direction
        var professors = run.Evaluators?.Professors;
        double intendedDx = 0, intendedDy = 0;

        if (professors != null && professors.Count > 0)
        {
            // Professor vectors point toward targets - average them for intended direction
            foreach (var prof in professors.Where(p => !p.Holdout && p.Vector.Count >= 2))
            {
                intendedDx += prof.Vector[0];
                intendedDy += prof.Vector[1];
            }

            if (professors.Count > 0)
            {
                intendedDx /= professors.Count;
                intendedDy /= professors.Count;
            }
        }

        var intendedMag = Math.Sqrt(intendedDx * intendedDx + intendedDy * intendedDy);

        // Compute alignment (cosine of angle)
        var alignment = 0.0;
        if (actualMag > 0.001 && intendedMag > 0.001)
        {
            var dot = actualDx * intendedDx + actualDy * intendedDy;
            alignment = dot / (actualMag * intendedMag);
        }

        return new GradientInspection
        {
            TimestepIndex = timestepIndex,
            Time = current.T,
            Position = (current.State2D[0], current.State2D[1]),
            ActualDirection = (actualDx, actualDy),
            ActualMagnitude = actualMag,
            IntendedDirection = (intendedDx, intendedDy),
            IntendedMagnitude = intendedMag,
            Alignment = alignment,
            AlignmentCategory = alignment switch
            {
                > 0.9 => "Well Aligned",
                > 0.5 => "Moderately Aligned",
                > 0.0 => "Weakly Aligned",
                > -0.5 => "Diverging",
                _ => "Opposing"
            },
            DeviationAngle = Math.Acos(Math.Clamp(alignment, -1, 1)) * 180 / Math.PI
        };
    }

    /// <summary>
    /// Project a hypothetical "what-if" trajectory from a given point.
    /// Shows where training might have gone with different conditions.
    /// </summary>
    public WhatIfProjection ProjectHypothetical(
        GeometryRun run,
        int startIndex,
        WhatIfScenario scenario,
        int projectionSteps = 20)
    {
        var trajectory = run.Trajectory?.Timesteps;
        if (trajectory == null || startIndex < 0 || startIndex >= trajectory.Count)
            return new WhatIfProjection { IsValid = false };

        var start = trajectory[startIndex];
        if (start.State2D?.Count < 2)
            return new WhatIfProjection { IsValid = false };

        var projectedPoints = new List<(double t, double x, double y)>();
        var x = start.State2D[0];
        var y = start.State2D[1];
        var t = start.T;

        // Estimate step size from actual trajectory
        var avgDt = 0.01;
        if (startIndex > 0)
        {
            var prev = trajectory[startIndex - 1];
            avgDt = start.T - prev.T;
        }

        // Apply scenario modifications
        var lr = scenario.LearningRateMultiplier;
        var momentum = scenario.MomentumFactor;
        var noise = scenario.NoiseLevel;
        var directionBias = scenario.DirectionBias;

        // Get base velocity from actual trajectory
        var baseVx = 0.0;
        var baseVy = 0.0;
        if (startIndex < trajectory.Count - 1)
        {
            var next = trajectory[startIndex + 1];
            if (next.State2D?.Count >= 2)
            {
                baseVx = (next.State2D[0] - x) / avgDt;
                baseVy = (next.State2D[1] - y) / avgDt;
            }
        }

        var vx = baseVx * lr;
        var vy = baseVy * lr;

        // Apply direction bias (rotate velocity)
        if (Math.Abs(directionBias) > 0.01)
        {
            var cos = Math.Cos(directionBias * Math.PI / 180);
            var sin = Math.Sin(directionBias * Math.PI / 180);
            var newVx = vx * cos - vy * sin;
            var newVy = vx * sin + vy * cos;
            vx = newVx;
            vy = newVy;
        }

        var random = new Random(42); // Deterministic for reproducibility

        for (int i = 0; i < projectionSteps; i++)
        {
            projectedPoints.Add((t, x, y));

            // Update with momentum
            vx = vx * momentum;
            vy = vy * momentum;

            // Add noise
            if (noise > 0)
            {
                vx += (random.NextDouble() - 0.5) * 2 * noise;
                vy += (random.NextDouble() - 0.5) * 2 * noise;
            }

            x += vx * avgDt;
            y += vy * avgDt;
            t += avgDt;
        }

        return new WhatIfProjection
        {
            IsValid = true,
            StartIndex = startIndex,
            Scenario = scenario,
            ProjectedPath = projectedPoints,
            FinalPosition = (x, y),
            TotalDisplacement = Math.Sqrt(
                Math.Pow(x - start.State2D[0], 2) + 
                Math.Pow(y - start.State2D[1], 2))
        };
    }
}

#region Result Types

public record TimeTravelState
{
    public int TimestepIndex { get; init; }
    public double Time { get; init; }
    public (double X, double Y) Position { get; init; }
    public (double Vx, double Vy) Velocity { get; init; }
    public double Speed { get; init; }
    public (double Ax, double Ay) Acceleration { get; init; }
    public double Curvature { get; init; }
    public double? Lambda1 { get; init; }
    public double? Lambda2 { get; init; }
    public double? EffectiveDimension { get; init; }
    public double? Loss { get; init; }
    public double? Accuracy { get; init; }
    public double? LearningRate { get; init; }
    public double? GradientNorm { get; init; }
    public bool IsAtBifurcation { get; init; }
    public double DistanceFromOrigin { get; init; }
}

public record GradientInspection
{
    public int TimestepIndex { get; init; }
    public double Time { get; init; }
    public (double X, double Y) Position { get; init; }
    public (double Dx, double Dy) ActualDirection { get; init; }
    public double ActualMagnitude { get; init; }
    public (double Dx, double Dy) IntendedDirection { get; init; }
    public double IntendedMagnitude { get; init; }
    public double Alignment { get; init; }
    public string AlignmentCategory { get; init; } = "";
    public double DeviationAngle { get; init; }
}

public record WhatIfScenario
{
    public string Name { get; init; } = "Custom";
    public double LearningRateMultiplier { get; init; } = 1.0;
    public double MomentumFactor { get; init; } = 0.9;
    public double NoiseLevel { get; init; } = 0;
    public double DirectionBias { get; init; } = 0; // Degrees

    public static WhatIfScenario Default => new();
    public static WhatIfScenario HigherLearningRate => new() { Name = "2x Learning Rate", LearningRateMultiplier = 2.0 };
    public static WhatIfScenario LowerLearningRate => new() { Name = "0.5x Learning Rate", LearningRateMultiplier = 0.5 };
    public static WhatIfScenario MoreMomentum => new() { Name = "High Momentum", MomentumFactor = 0.99 };
    public static WhatIfScenario LessMomentum => new() { Name = "Low Momentum", MomentumFactor = 0.5 };
    public static WhatIfScenario WithNoise => new() { Name = "With Noise", NoiseLevel = 0.1 };
    public static WhatIfScenario TurnLeft => new() { Name = "Turn Left 30°", DirectionBias = 30 };
    public static WhatIfScenario TurnRight => new() { Name = "Turn Right 30°", DirectionBias = -30 };
}

public record WhatIfProjection
{
    public bool IsValid { get; init; }
    public int StartIndex { get; init; }
    public WhatIfScenario Scenario { get; init; } = WhatIfScenario.Default;
    public List<(double t, double x, double y)> ProjectedPath { get; init; } = [];
    public (double X, double Y) FinalPosition { get; init; }
    public double TotalDisplacement { get; init; }
}

public record WhatIfMarker
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public int TimestepIndex { get; init; }
    public WhatIfScenario Scenario { get; init; } = WhatIfScenario.Default;
    public WhatIfProjection? Projection { get; init; }
    public string Note { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.Now;
}

#endregion
