using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Service for comparative analysis of multiple training runs.
/// Provides overlay mode, deviation highlighting, statistical bands,
/// and distance metrics for Phase 3.2 Comparative Analysis.
/// </summary>
public class ComparativeAnalysisService
{
    #region Distance Metrics

    /// <summary>
    /// Compute Dynamic Time Warping distance between two trajectories.
    /// DTW accounts for temporal misalignment between sequences.
    /// </summary>
    public DtwResult ComputeDtw(GeometryRun run1, GeometryRun run2)
    {
        var traj1 = ExtractPoints(run1);
        var traj2 = ExtractPoints(run2);

        if (traj1.Count == 0 || traj2.Count == 0)
            return new DtwResult { Distance = double.NaN, IsValid = false };

        int n = traj1.Count;
        int m = traj2.Count;

        // DTW matrix
        var dtw = new double[n + 1, m + 1];
        for (int i = 0; i <= n; i++) dtw[i, 0] = double.PositiveInfinity;
        for (int j = 0; j <= m; j++) dtw[0, j] = double.PositiveInfinity;
        dtw[0, 0] = 0;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                var cost = EuclideanDistance(traj1[i - 1], traj2[j - 1]);
                dtw[i, j] = cost + Math.Min(Math.Min(dtw[i - 1, j], dtw[i, j - 1]), dtw[i - 1, j - 1]);
            }
        }

        // Backtrack to find warping path
        var path = BacktrackDtw(dtw, n, m);

        return new DtwResult
        {
            Distance = dtw[n, m],
            NormalizedDistance = dtw[n, m] / (n + m),
            WarpingPath = path,
            IsValid = true
        };
    }

    /// <summary>
    /// Compute Fréchet distance between two trajectories.
    /// The "dog walking" distance - minimum leash length needed.
    /// </summary>
    public FrechetResult ComputeFrechet(GeometryRun run1, GeometryRun run2)
    {
        var traj1 = ExtractPoints(run1);
        var traj2 = ExtractPoints(run2);

        if (traj1.Count == 0 || traj2.Count == 0)
            return new FrechetResult { Distance = double.NaN, IsValid = false };

        int n = traj1.Count;
        int m = traj2.Count;

        // Memoization table for recursive Fréchet computation
        var memo = new double[n, m];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
                memo[i, j] = -1;

        double ComputeRecursive(int i, int j)
        {
            if (memo[i, j] >= 0) return memo[i, j];

            var dist = EuclideanDistance(traj1[i], traj2[j]);

            if (i == 0 && j == 0)
            {
                memo[i, j] = dist;
            }
            else if (i == 0)
            {
                memo[i, j] = Math.Max(ComputeRecursive(0, j - 1), dist);
            }
            else if (j == 0)
            {
                memo[i, j] = Math.Max(ComputeRecursive(i - 1, 0), dist);
            }
            else
            {
                memo[i, j] = Math.Max(
                    Math.Min(Math.Min(
                        ComputeRecursive(i - 1, j),
                        ComputeRecursive(i, j - 1)),
                        ComputeRecursive(i - 1, j - 1)),
                    dist);
            }

            return memo[i, j];
        }

        var distance = ComputeRecursive(n - 1, m - 1);

        return new FrechetResult
        {
            Distance = distance,
            IsValid = true
        };
    }

    /// <summary>
    /// Compute point-wise deviation between two trajectories at each timestep.
    /// </summary>
    public DeviationAnalysis ComputeDeviation(GeometryRun run1, GeometryRun run2)
    {
        var traj1 = run1.Trajectory?.Timesteps ?? [];
        var traj2 = run2.Trajectory?.Timesteps ?? [];

        if (traj1.Count == 0 || traj2.Count == 0)
            return new DeviationAnalysis { IsValid = false };

        var points = new List<DeviationPoint>();
        var divergenceRegions = new List<DivergenceRegion>();
        var currentRegion = (DivergenceRegion?)null;
        const double divergenceThreshold = 0.5; // Significant deviation threshold

        // Interpolate shorter trajectory to match longer one
        var maxCount = Math.Max(traj1.Count, traj2.Count);
        
        for (int i = 0; i < maxCount; i++)
        {
            var t = (double)i / (maxCount - 1);
            
            var p1 = InterpolatePoint(traj1, t);
            var p2 = InterpolatePoint(traj2, t);

            if (p1 == null || p2 == null) continue;

            var deviation = EuclideanDistance(p1.Value, p2.Value);
            var time = p1.Value.time;

            points.Add(new DeviationPoint
            {
                Time = time,
                Position1 = (p1.Value.x, p1.Value.y),
                Position2 = (p2.Value.x, p2.Value.y),
                Deviation = deviation
            });

            // Track divergence regions
            if (deviation > divergenceThreshold)
            {
                if (currentRegion == null)
                {
                    currentRegion = new DivergenceRegion { StartTime = time, MaxDeviation = deviation };
                }
                else
                {
                    currentRegion = currentRegion with { MaxDeviation = Math.Max(currentRegion.MaxDeviation, deviation) };
                }
            }
            else if (currentRegion != null)
            {
                divergenceRegions.Add(currentRegion with { EndTime = time });
                currentRegion = null;
            }
        }

        // Close any open region
        if (currentRegion != null && points.Count > 0)
        {
            divergenceRegions.Add(currentRegion with { EndTime = points[^1].Time });
        }

        return new DeviationAnalysis
        {
            IsValid = true,
            Points = points,
            DivergenceRegions = divergenceRegions,
            MeanDeviation = points.Count > 0 ? points.Average(p => p.Deviation) : 0,
            MaxDeviation = points.Count > 0 ? points.Max(p => p.Deviation) : 0,
            FinalDeviation = points.Count > 0 ? points[^1].Deviation : 0
        };
    }

    #endregion

    #region Statistical Bands

    /// <summary>
    /// Compute statistical bands (mean and confidence intervals) from multiple runs.
    /// </summary>
    public StatisticalBands ComputeStatisticalBands(IEnumerable<GeometryRun> runs, double confidenceLevel = 0.95)
    {
        var runList = runs.ToList();
        if (runList.Count < 2)
            return new StatisticalBands { IsValid = false };

        // Normalize all trajectories to same time scale
        var allTrajectories = runList
            .Select(r => r.Trajectory?.Timesteps ?? [])
            .Where(t => t.Count > 0)
            .ToList();

        if (allTrajectories.Count < 2)
            return new StatisticalBands { IsValid = false };

        var maxLength = allTrajectories.Max(t => t.Count);
        var points = new List<StatisticalBandPoint>();

        for (int i = 0; i < maxLength; i++)
        {
            var t = (double)i / (maxLength - 1);
            
            var xValues = new List<double>();
            var yValues = new List<double>();

            foreach (var trajectory in allTrajectories)
            {
                var point = InterpolatePoint(trajectory, t);
                if (point != null)
                {
                    xValues.Add(point.Value.x);
                    yValues.Add(point.Value.y);
                }
            }

            if (xValues.Count < 2) continue;

            var meanX = xValues.Average();
            var meanY = yValues.Average();
            var stdX = StandardDeviation(xValues);
            var stdY = StandardDeviation(yValues);

            // Z-score for confidence level (95% = 1.96)
            var z = confidenceLevel switch
            {
                >= 0.99 => 2.576,
                >= 0.95 => 1.96,
                >= 0.90 => 1.645,
                _ => 1.0
            };

            var time = allTrajectories[0].Count > 0 
                ? allTrajectories[0][(int)(t * (allTrajectories[0].Count - 1))].T 
                : t;

            points.Add(new StatisticalBandPoint
            {
                Time = time,
                MeanX = meanX,
                MeanY = meanY,
                StdX = stdX,
                StdY = stdY,
                ConfidenceRadiusX = z * stdX / Math.Sqrt(xValues.Count),
                ConfidenceRadiusY = z * stdY / Math.Sqrt(yValues.Count)
            });
        }

        return new StatisticalBands
        {
            IsValid = true,
            Points = points,
            ConfidenceLevel = confidenceLevel,
            RunCount = runList.Count
        };
    }

    #endregion

    #region Overlay Alignment

    /// <summary>
    /// Align multiple trajectories for overlay visualization.
    /// Returns normalized coordinates for each trajectory.
    /// </summary>
    public OverlayAlignment AlignForOverlay(IEnumerable<GeometryRun> runs)
    {
        var runList = runs.ToList();
        if (runList.Count == 0)
            return new OverlayAlignment { IsValid = false };

        // Find global bounds
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var run in runList)
        {
            var traj = run.Trajectory?.Timesteps ?? [];
            foreach (var ts in traj)
            {
                if (ts.State2D?.Count >= 2)
                {
                    minX = Math.Min(minX, ts.State2D[0]);
                    maxX = Math.Max(maxX, ts.State2D[0]);
                    minY = Math.Min(minY, ts.State2D[1]);
                    maxY = Math.Max(maxY, ts.State2D[1]);
                }
            }
        }

        var rangeX = maxX - minX;
        var rangeY = maxY - minY;
        if (rangeX < 0.001) rangeX = 1;
        if (rangeY < 0.001) rangeY = 1;

        // Add margin
        minX -= rangeX * 0.1;
        maxX += rangeX * 0.1;
        minY -= rangeY * 0.1;
        maxY += rangeY * 0.1;

        var trajectories = new List<AlignedTrajectory>();
        int index = 0;

        foreach (var run in runList)
        {
            var points = new List<(double time, double x, double y)>();
            var traj = run.Trajectory?.Timesteps ?? [];

            foreach (var ts in traj)
            {
                if (ts.State2D?.Count >= 2)
                {
                    // Normalize to [0,1]
                    var nx = (ts.State2D[0] - minX) / (maxX - minX);
                    var ny = (ts.State2D[1] - minY) / (maxY - minY);
                    points.Add((ts.T, nx, ny));
                }
            }

            trajectories.Add(new AlignedTrajectory
            {
                RunId = run.Metadata?.RunId ?? $"Run {index + 1}",
                Condition = run.Metadata?.Condition ?? "Unknown",
                Seed = run.Metadata?.Seed ?? 0,
                Points = points,
                ColorIndex = index
            });

            index++;
        }

        return new OverlayAlignment
        {
            IsValid = true,
            Trajectories = trajectories,
            Bounds = (minX, maxX, minY, maxY)
        };
    }

    #endregion

    #region Helper Methods

    private static List<(double time, double x, double y)> ExtractPoints(GeometryRun run)
    {
        var trajectory = run.Trajectory?.Timesteps ?? [];
        return trajectory
            .Where(t => t.State2D?.Count >= 2)
            .Select(t => (t.T, t.State2D![0], t.State2D![1]))
            .ToList();
    }

    private static double EuclideanDistance((double time, double x, double y) p1, (double time, double x, double y) p2)
    {
        var dx = p1.x - p2.x;
        var dy = p1.y - p2.y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static (double time, double x, double y)? InterpolatePoint(List<TrajectoryTimestep> trajectory, double t)
    {
        if (trajectory.Count == 0) return null;
        if (trajectory.Count == 1) 
        {
            var ts = trajectory[0];
            return ts.State2D?.Count >= 2 ? (ts.T, ts.State2D[0], ts.State2D[1]) : null;
        }

        var index = t * (trajectory.Count - 1);
        var i = (int)index;
        var frac = index - i;

        if (i >= trajectory.Count - 1)
        {
            var ts = trajectory[^1];
            return ts.State2D?.Count >= 2 ? (ts.T, ts.State2D[0], ts.State2D[1]) : null;
        }

        var t1 = trajectory[i];
        var t2 = trajectory[i + 1];

        if (t1.State2D?.Count < 2 || t2.State2D?.Count < 2) return null;

        return (
            t1.T + frac * (t2.T - t1.T),
            t1.State2D![0] + frac * (t2.State2D![0] - t1.State2D![0]),
            t1.State2D![1] + frac * (t2.State2D![1] - t1.State2D![1])
        );
    }

    private static List<(int i, int j)> BacktrackDtw(double[,] dtw, int n, int m)
    {
        var path = new List<(int i, int j)>();
        int i = n, j = m;

        while (i > 0 || j > 0)
        {
            path.Add((i - 1, j - 1));

            if (i == 0)
            {
                j--;
            }
            else if (j == 0)
            {
                i--;
            }
            else
            {
                var minVal = Math.Min(Math.Min(dtw[i - 1, j], dtw[i, j - 1]), dtw[i - 1, j - 1]);
                if (dtw[i - 1, j - 1] == minVal)
                {
                    i--; j--;
                }
                else if (dtw[i - 1, j] == minVal)
                {
                    i--;
                }
                else
                {
                    j--;
                }
            }
        }

        path.Reverse();
        return path;
    }

    private static double StandardDeviation(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0;

        var mean = list.Average();
        var sumSquares = list.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (list.Count - 1));
    }

    #endregion
}

#region Result Types

public record DtwResult
{
    public double Distance { get; init; }
    public double NormalizedDistance { get; init; }
    public List<(int i, int j)> WarpingPath { get; init; } = [];
    public bool IsValid { get; init; }
}

public record FrechetResult
{
    public double Distance { get; init; }
    public bool IsValid { get; init; }
}

public record DeviationAnalysis
{
    public bool IsValid { get; init; }
    public List<DeviationPoint> Points { get; init; } = [];
    public List<DivergenceRegion> DivergenceRegions { get; init; } = [];
    public double MeanDeviation { get; init; }
    public double MaxDeviation { get; init; }
    public double FinalDeviation { get; init; }
}

public record DeviationPoint
{
    public double Time { get; init; }
    public (double X, double Y) Position1 { get; init; }
    public (double X, double Y) Position2 { get; init; }
    public double Deviation { get; init; }
}

public record DivergenceRegion
{
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public double MaxDeviation { get; init; }
}

public record StatisticalBands
{
    public bool IsValid { get; init; }
    public List<StatisticalBandPoint> Points { get; init; } = [];
    public double ConfidenceLevel { get; init; }
    public int RunCount { get; init; }
}

public record StatisticalBandPoint
{
    public double Time { get; init; }
    public double MeanX { get; init; }
    public double MeanY { get; init; }
    public double StdX { get; init; }
    public double StdY { get; init; }
    public double ConfidenceRadiusX { get; init; }
    public double ConfidenceRadiusY { get; init; }
}

public record OverlayAlignment
{
    public bool IsValid { get; init; }
    public List<AlignedTrajectory> Trajectories { get; init; } = [];
    public (double MinX, double MaxX, double MinY, double MaxY) Bounds { get; init; }
}

public record AlignedTrajectory
{
    public string RunId { get; init; } = "";
    public string Condition { get; init; } = "";
    public int Seed { get; init; }
    public List<(double time, double x, double y)> Points { get; init; } = [];
    public int ColorIndex { get; init; }
}

#endregion
