using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Service for computing quantitative analysis metrics from trajectory data.
/// Provides eigenvalue analysis, Lyapunov exponent estimation, bifurcation detection,
/// and dimension collapse alerts for Phase 3.1 Quantitative Overlays.
/// </summary>
public class AnalysisService
{
    #region Eigenvalue Analysis

    /// <summary>
    /// Extract eigenvalue timeline from a geometry run.
    /// Returns paired eigenvalues (λ₁, λ₂) over time for visualization.
    /// </summary>
    public EigenvalueTimeline GetEigenvalueTimeline(GeometryRun run)
    {
        var eigenvalues = run.Geometry?.Eigenvalues ?? [];
        
        if (eigenvalues.Count == 0)
        {
            // Compute from trajectory if not available
            return ComputeEigenvaluesFromTrajectory(run);
        }

        var points = eigenvalues
            .Where(e => e.Values.Count >= 2)
            .Select(e => new EigenvaluePoint
            {
                Time = e.T,
                Lambda1 = e.Values[0],
                Lambda2 = e.Values[1],
                Ratio = e.Values[1] != 0 ? e.Values[0] / e.Values[1] : double.PositiveInfinity
            })
            .ToList();

        return new EigenvalueTimeline
        {
            Points = points,
            MinLambda1 = points.Count > 0 ? points.Min(p => p.Lambda1) : 0,
            MaxLambda1 = points.Count > 0 ? points.Max(p => p.Lambda1) : 1,
            MinLambda2 = points.Count > 0 ? points.Min(p => p.Lambda2) : 0,
            MaxLambda2 = points.Count > 0 ? points.Max(p => p.Lambda2) : 1
        };
    }

    /// <summary>
    /// Compute eigenvalues from trajectory using local covariance.
    /// </summary>
    private EigenvalueTimeline ComputeEigenvaluesFromTrajectory(GeometryRun run)
    {
        var trajectory = run.Trajectory?.Timesteps ?? [];
        if (trajectory.Count < 10)
            return new EigenvalueTimeline();

        var points = new List<EigenvaluePoint>();
        const int windowSize = 5;

        for (int i = windowSize; i < trajectory.Count - windowSize; i++)
        {
            var window = trajectory.Skip(i - windowSize).Take(windowSize * 2 + 1).ToList();
            var (lambda1, lambda2) = ComputeLocalEigenvalues(window);

            points.Add(new EigenvaluePoint
            {
                Time = trajectory[i].T,
                Lambda1 = lambda1,
                Lambda2 = lambda2,
                Ratio = lambda2 != 0 ? lambda1 / lambda2 : double.PositiveInfinity
            });
        }

        return new EigenvalueTimeline
        {
            Points = points,
            MinLambda1 = points.Count > 0 ? points.Min(p => p.Lambda1) : 0,
            MaxLambda1 = points.Count > 0 ? points.Max(p => p.Lambda1) : 1,
            MinLambda2 = points.Count > 0 ? points.Min(p => p.Lambda2) : 0,
            MaxLambda2 = points.Count > 0 ? points.Max(p => p.Lambda2) : 1
        };
    }

    /// <summary>
    /// Compute 2D eigenvalues from covariance matrix of points in window.
    /// </summary>
    private static (double lambda1, double lambda2) ComputeLocalEigenvalues(List<TrajectoryTimestep> window)
    {
        if (window.Count < 2) return (0, 0);

        var points = window
            .Where(t => t.State2D?.Count >= 2)
            .Select(t => (x: t.State2D![0], y: t.State2D![1]))
            .ToList();

        if (points.Count < 2) return (0, 0);

        // Compute mean
        var meanX = points.Average(p => p.x);
        var meanY = points.Average(p => p.y);

        // Compute covariance matrix elements
        double cov00 = 0, cov01 = 0, cov11 = 0;
        foreach (var (x, y) in points)
        {
            var dx = x - meanX;
            var dy = y - meanY;
            cov00 += dx * dx;
            cov01 += dx * dy;
            cov11 += dy * dy;
        }

        var n = points.Count - 1;
        cov00 /= n;
        cov01 /= n;
        cov11 /= n;

        // Eigenvalues of 2x2 symmetric matrix
        // λ = (trace ± sqrt(trace² - 4*det)) / 2
        var trace = cov00 + cov11;
        var det = cov00 * cov11 - cov01 * cov01;
        var discriminant = trace * trace - 4 * det;

        if (discriminant < 0) discriminant = 0;

        var sqrtDisc = Math.Sqrt(discriminant);
        var lambda1 = (trace + sqrtDisc) / 2;
        var lambda2 = (trace - sqrtDisc) / 2;

        return (Math.Max(lambda1, 0), Math.Max(lambda2, 0));
    }

    #endregion

    #region Lyapunov Exponent Estimation

    /// <summary>
    /// Estimate Lyapunov exponent from trajectory.
    /// Positive values indicate chaos, negative indicate stability.
    /// </summary>
    public LyapunovAnalysis EstimateLyapunovExponent(GeometryRun run)
    {
        var trajectory = run.Trajectory?.Timesteps ?? [];
        if (trajectory.Count < 20)
            return new LyapunovAnalysis { IsValid = false, ErrorMessage = "Insufficient data" };

        var points = new List<LyapunovPoint>();
        const int windowSize = 10;
        const double epsilon = 1e-6;

        for (int i = windowSize; i < trajectory.Count - windowSize; i++)
        {
            var localLyapunov = EstimateLocalLyapunov(trajectory, i, windowSize, epsilon);
            points.Add(new LyapunovPoint
            {
                Time = trajectory[i].T,
                Value = localLyapunov,
                IsChaotic = localLyapunov > 0.01
            });
        }

        var avgLyapunov = points.Count > 0 ? points.Average(p => p.Value) : 0;

        return new LyapunovAnalysis
        {
            IsValid = true,
            Points = points,
            AverageExponent = avgLyapunov,
            Classification = avgLyapunov > 0.1 ? "Chaotic"
                           : avgLyapunov > 0.01 ? "Edge of Chaos"
                           : avgLyapunov > -0.01 ? "Neutral"
                           : "Stable"
        };
    }

    /// <summary>
    /// Estimate local Lyapunov exponent using divergence of nearby trajectories.
    /// </summary>
    private static double EstimateLocalLyapunov(
        List<TrajectoryTimestep> trajectory,
        int centerIndex,
        int windowSize,
        double epsilon)
    {
        // Find nearby point (within epsilon distance)
        var center = trajectory[centerIndex];
        if (center.State2D?.Count < 2) return 0;

        var cx = center.State2D![0];
        var cy = center.State2D![1];

        int nearbyIndex = -1;
        double minDist = double.MaxValue;

        for (int j = 0; j < trajectory.Count; j++)
        {
            if (Math.Abs(j - centerIndex) < windowSize) continue;
            
            var other = trajectory[j];
            if (other.State2D?.Count < 2) continue;

            var dist = Math.Sqrt(
                Math.Pow(other.State2D![0] - cx, 2) +
                Math.Pow(other.State2D![1] - cy, 2));

            if (dist > epsilon && dist < minDist && dist < 0.1)
            {
                minDist = dist;
                nearbyIndex = j;
            }
        }

        if (nearbyIndex < 0) return 0;

        // Track divergence over time
        var divergences = new List<double>();
        var dt = trajectory.Count > 1 ? trajectory[1].T - trajectory[0].T : 0.01;

        for (int k = 1; k <= Math.Min(windowSize, trajectory.Count - Math.Max(centerIndex, nearbyIndex) - 1); k++)
        {
            var i1 = centerIndex + k;
            var i2 = nearbyIndex + k;

            if (i1 >= trajectory.Count || i2 >= trajectory.Count) break;

            var t1 = trajectory[i1];
            var t2 = trajectory[i2];

            if (t1.State2D?.Count < 2 || t2.State2D?.Count < 2) continue;

            var newDist = Math.Sqrt(
                Math.Pow(t1.State2D![0] - t2.State2D![0], 2) +
                Math.Pow(t1.State2D![1] - t2.State2D![1], 2));

            if (newDist > 0 && minDist > 0)
            {
                divergences.Add(Math.Log(newDist / minDist) / (k * dt));
            }
        }

        return divergences.Count > 0 ? divergences.Average() : 0;
    }

    #endregion

    #region Bifurcation Detection

    /// <summary>
    /// Detect potential bifurcation points where dynamics change qualitatively.
    /// </summary>
    public BifurcationAnalysis DetectBifurcations(GeometryRun run)
    {
        var trajectory = run.Trajectory?.Timesteps ?? [];
        if (trajectory.Count < 30)
            return new BifurcationAnalysis { Markers = [] };

        var markers = new List<BifurcationMarker>();
        const int windowSize = 10;
        const double curvatureThreshold = 2.0;
        const double velocityChangeThreshold = 0.5;

        for (int i = windowSize; i < trajectory.Count - windowSize; i++)
        {
            var beforeWindow = trajectory.Skip(i - windowSize).Take(windowSize).ToList();
            var afterWindow = trajectory.Skip(i).Take(windowSize).ToList();

            // Check for sudden curvature change
            var curvatureBefore = beforeWindow.Average(t => Math.Abs(t.Curvature));
            var curvatureAfter = afterWindow.Average(t => Math.Abs(t.Curvature));
            var curvatureChange = curvatureAfter - curvatureBefore;

            // Check for velocity direction change
            var velocityChangeMagnitude = ComputeVelocityDirectionChange(beforeWindow, afterWindow);

            // Check for eigenvalue ratio change (if available)
            var eigenRatioBefore = ComputeAverageEigenRatio(run, beforeWindow);
            var eigenRatioAfter = ComputeAverageEigenRatio(run, afterWindow);
            var eigenRatioChange = Math.Abs(eigenRatioAfter - eigenRatioBefore);

            BifurcationType? type = null;

            if (Math.Abs(curvatureChange) > curvatureThreshold)
            {
                type = curvatureChange > 0 ? BifurcationType.FoldBifurcation : BifurcationType.Saddle;
            }
            else if (velocityChangeMagnitude > velocityChangeThreshold)
            {
                type = BifurcationType.HopfBifurcation;
            }
            else if (eigenRatioChange > 2.0)
            {
                type = BifurcationType.PitchforkBifurcation;
            }

            if (type != null)
            {
                // Avoid duplicate markers too close together
                if (markers.Count == 0 || trajectory[i].T - markers[^1].Time > 0.1)
                {
                    markers.Add(new BifurcationMarker
                    {
                        Time = trajectory[i].T,
                        Type = type.Value,
                        Confidence = Math.Min(1.0, Math.Max(
                            Math.Abs(curvatureChange) / curvatureThreshold,
                            velocityChangeMagnitude / velocityChangeThreshold) / 2),
                        Position = trajectory[i].State2D?.Count >= 2
                            ? (trajectory[i].State2D![0], trajectory[i].State2D![1])
                            : (0, 0)
                    });
                }
            }
        }

        return new BifurcationAnalysis { Markers = markers };
    }

    private static double ComputeVelocityDirectionChange(
        List<TrajectoryTimestep> before,
        List<TrajectoryTimestep> after)
    {
        var velBefore = before
            .Where(t => t.Velocity?.Count >= 2)
            .Select(t => (t.Velocity![0], t.Velocity![1]))
            .ToList();

        var velAfter = after
            .Where(t => t.Velocity?.Count >= 2)
            .Select(t => (t.Velocity![0], t.Velocity![1]))
            .ToList();

        if (velBefore.Count == 0 || velAfter.Count == 0) return 0;

        var avgBefore = (velBefore.Average(v => v.Item1), velBefore.Average(v => v.Item2));
        var avgAfter = (velAfter.Average(v => v.Item1), velAfter.Average(v => v.Item2));

        var magBefore = Math.Sqrt(avgBefore.Item1 * avgBefore.Item1 + avgBefore.Item2 * avgBefore.Item2);
        var magAfter = Math.Sqrt(avgAfter.Item1 * avgAfter.Item1 + avgAfter.Item2 * avgAfter.Item2);

        if (magBefore < 1e-10 || magAfter < 1e-10) return 0;

        // Cosine similarity
        var dot = avgBefore.Item1 * avgAfter.Item1 + avgBefore.Item2 * avgAfter.Item2;
        var cosSim = dot / (magBefore * magAfter);

        return 1 - cosSim; // 0 = same direction, 2 = opposite
    }

    private static double ComputeAverageEigenRatio(GeometryRun run, List<TrajectoryTimestep> window)
    {
        var eigenvalues = run.Geometry?.Eigenvalues ?? [];
        if (eigenvalues.Count == 0) return 1.0;

        var windowStart = window.First().T;
        var windowEnd = window.Last().T;

        var relevantEigen = eigenvalues
            .Where(e => e.T >= windowStart && e.T <= windowEnd && e.Values.Count >= 2 && e.Values[1] != 0)
            .Select(e => e.Values[0] / e.Values[1])
            .ToList();

        return relevantEigen.Count > 0 ? relevantEigen.Average() : 1.0;
    }

    #endregion

    #region Dimension Collapse Detection

    /// <summary>
    /// Detect regions where effective dimension drops significantly.
    /// </summary>
    public DimensionCollapseAnalysis DetectDimensionCollapse(GeometryRun run)
    {
        var trajectory = run.Trajectory?.Timesteps ?? [];
        if (trajectory.Count < 10)
            return new DimensionCollapseAnalysis { Alerts = [] };

        var alerts = new List<DimensionCollapseAlert>();
        const double collapseThreshold = 1.5; // Effective dim below this is concerning
        const double changeThreshold = 0.3;   // Sudden drop threshold

        double? previousEffDim = null;

        foreach (var timestep in trajectory)
        {
            var effDim = timestep.EffectiveDim;
            
            if (effDim > 0 && effDim < collapseThreshold)
            {
                var isNewAlert = alerts.Count == 0 || timestep.T - alerts[^1].EndTime > 0.1;
                
                if (isNewAlert)
                {
                    alerts.Add(new DimensionCollapseAlert
                    {
                        StartTime = timestep.T,
                        EndTime = timestep.T,
                        MinDimension = effDim,
                        Severity = effDim < 1.2 ? CollapseSeverity.Critical
                                 : effDim < 1.3 ? CollapseSeverity.Warning
                                 : CollapseSeverity.Info
                    });
                }
                else
                {
                    var lastAlert = alerts[^1];
                    alerts[^1] = lastAlert with
                    {
                        EndTime = timestep.T,
                        MinDimension = Math.Min(lastAlert.MinDimension, effDim)
                    };
                }
            }

            // Check for sudden drop
            if (previousEffDim != null && effDim > 0 && previousEffDim - effDim > changeThreshold)
            {
                var existingAlert = alerts.LastOrDefault();
                if (existingAlert == null || Math.Abs(existingAlert.StartTime - timestep.T) > 0.01)
                {
                    alerts.Add(new DimensionCollapseAlert
                    {
                        StartTime = timestep.T,
                        EndTime = timestep.T,
                        MinDimension = effDim,
                        Severity = CollapseSeverity.Warning,
                        IsSuddenDrop = true
                    });
                }
            }

            if (effDim > 0) previousEffDim = effDim;
        }

        return new DimensionCollapseAnalysis { Alerts = alerts };
    }

    #endregion

    #region Comprehensive Analysis

    /// <summary>
    /// Run all analysis methods and return comprehensive results.
    /// </summary>
    public ComprehensiveAnalysis AnalyzeRun(GeometryRun run)
    {
        return new ComprehensiveAnalysis
        {
            Eigenvalues = GetEigenvalueTimeline(run),
            Lyapunov = EstimateLyapunovExponent(run),
            Bifurcations = DetectBifurcations(run),
            DimensionCollapse = DetectDimensionCollapse(run)
        };
    }

    #endregion
}

#region Result Types

public record EigenvalueTimeline
{
    public List<EigenvaluePoint> Points { get; init; } = [];
    public double MinLambda1 { get; init; }
    public double MaxLambda1 { get; init; }
    public double MinLambda2 { get; init; }
    public double MaxLambda2 { get; init; }
    public double TimeStart => Points.Count > 0 ? Points[0].Time : 0;
    public double TimeEnd => Points.Count > 0 ? Points[^1].Time : 1;
}

public record EigenvaluePoint
{
    public double Time { get; init; }
    public double Lambda1 { get; init; }
    public double Lambda2 { get; init; }
    public double Ratio { get; init; }
}

public record LyapunovAnalysis
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public List<LyapunovPoint> Points { get; init; } = [];
    public double AverageExponent { get; init; }
    public string Classification { get; init; } = "Unknown";
}

public record LyapunovPoint
{
    public double Time { get; init; }
    public double Value { get; init; }
    public bool IsChaotic { get; init; }
}

public record BifurcationAnalysis
{
    public List<BifurcationMarker> Markers { get; init; } = [];
}

public record BifurcationMarker
{
    public double Time { get; init; }
    public BifurcationType Type { get; init; }
    public double Confidence { get; init; }
    public (double X, double Y) Position { get; init; }
}

public enum BifurcationType
{
    FoldBifurcation,      // Saddle-node
    HopfBifurcation,      // Oscillation onset
    PitchforkBifurcation, // Symmetry breaking
    Saddle                // Saddle point
}

public record DimensionCollapseAnalysis
{
    public List<DimensionCollapseAlert> Alerts { get; init; } = [];
}

public record DimensionCollapseAlert
{
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public double MinDimension { get; init; }
    public CollapseSeverity Severity { get; init; }
    public bool IsSuddenDrop { get; init; }
}

public enum CollapseSeverity
{
    Info,
    Warning,
    Critical
}

public record ComprehensiveAnalysis
{
    public EigenvalueTimeline Eigenvalues { get; init; } = new();
    public LyapunovAnalysis Lyapunov { get; init; } = new();
    public BifurcationAnalysis Bifurcations { get; init; } = new();
    public DimensionCollapseAnalysis DimensionCollapse { get; init; } = new();
}

#endregion
