using ScalarScope.Models;
using SkiaSharp;

namespace ScalarScope.Services;

/// <summary>
/// Phase 1: Canonical Demo State Service.
/// Manages synthetic demo data that flows through the same rendering pipeline as real data.
/// Provides a living, breathing reference state that eliminates all blank screens.
/// </summary>
public class DemoStateService : IDisposable
{
    // Singleton instance for global access
    private static readonly Lazy<DemoStateService> _instance = new(() => new DemoStateService());
    public static DemoStateService Instance => _instance.Value;

    // === Configuration ===
    
    /// <summary>
    /// Deterministic seed for reproducible demo data.
    /// This ensures the demo always looks the same across sessions.
    /// </summary>
    public const int DemoSeed = 42;
    
    /// <summary>
    /// Demo animation speed multiplier (slower than real data).
    /// Phase 1: Demo motion is slower to feel calm and illustrative.
    /// </summary>
    public const float DemoPlaybackSpeed = 0.6f;
    
    /// <summary>
    /// Demo saturation multiplier (reduced for reference state).
    /// Phase 1: Demo uses reduced saturation to be distinguishable.
    /// </summary>
    public const float DemoSaturation = 0.7f;

    // === Demo State ===
    
    /// <summary>
    /// Global flag indicating demo mode is active.
    /// All views should check this to adjust rendering behavior.
    /// </summary>
    public bool IsDemo { get; private set; }
    
    /// <summary>
    /// Label shown in UI to indicate reference state.
    /// </summary>
    public string DemoLabel => "Reference Example";

    // === Synthetic Demo Data ===
    
    /// <summary>
    /// Pre-generated demo trajectory data.
    /// </summary>
    public GeometryRun? DemoRun { get; private set; }
    
    /// <summary>
    /// Demo Path A (Pluralistic/Orthogonal) for comparison view.
    /// </summary>
    public GeometryRun? DemoPathA { get; private set; }
    
    /// <summary>
    /// Demo Path B (Convergent/Correlated) for comparison view.
    /// </summary>
    public GeometryRun? DemoPathB { get; private set; }

    // === Animation State ===
    
    private IDispatcherTimer? _animationTimer;
    private double _animationTime;
    private readonly Random _rng = new(DemoSeed);

    /// <summary>
    /// Current animation time (0-1 normalized, loops continuously).
    /// </summary>
    public double AnimationTime => _animationTime;

    /// <summary>
    /// Event fired when animation frame updates.
    /// Views should subscribe to repaint on this event.
    /// </summary>
    public event Action? OnAnimationFrame;

    // === Lifecycle ===

    private DemoStateService()
    {
        GenerateDemoData();
    }

    /// <summary>
    /// Initialize demo state and start animation loop.
    /// Call this once at app startup.
    /// </summary>
    public void Initialize()
    {
        IsDemo = true;
        StartAnimationLoop();
    }

    /// <summary>
    /// Transition from demo to real data.
    /// Phase 1 requirement: loading real data replaces demo without hard reset.
    /// </summary>
    public void TransitionToRealData()
    {
        IsDemo = false;
        // Animation continues but views will show real data instead
    }

    /// <summary>
    /// Return to demo state (e.g., when user clears data).
    /// </summary>
    public void ReturnToDemo()
    {
        IsDemo = true;
    }

    public void Dispose()
    {
        StopAnimationLoop();
        GC.SuppressFinalize(this);
    }

    // === Animation Loop ===

    private void StartAnimationLoop()
    {
        if (_animationTimer != null) return;

        _animationTimer = Application.Current?.Dispatcher.CreateTimer();
        if (_animationTimer == null) return;

        _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60fps
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimationLoop()
    {
        if (_animationTimer == null) return;
        
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        // Update animation time (loop every 10 seconds for calm demo pace)
        _animationTime += 0.016 * DemoPlaybackSpeed / 10.0;
        if (_animationTime > 1.0) _animationTime -= 1.0;

        OnAnimationFrame?.Invoke();
    }

    // === Demo Data Generation ===

    /// <summary>
    /// Generate all synthetic demo data with deterministic seed.
    /// Creates trajectory, scalar, eigenvalue, and geometry data.
    /// </summary>
    private void GenerateDemoData()
    {
        var rng = new Random(DemoSeed);

        // Generate main demo run (used for single-run views)
        DemoRun = GenerateSyntheticRun(rng, "demo_reference", "Reference");

        // Generate comparison paths
        DemoPathA = GenerateSyntheticRun(rng, "path_a_pluralistic", "Pluralistic");
        DemoPathB = GenerateSyntheticRun(rng, "path_b_convergent", "Convergent");
    }

    /// <summary>
    /// Generate a complete synthetic GeometryRun.
    /// </summary>
    private static GeometryRun GenerateSyntheticRun(Random rng, string runId, string condition)
    {
        const int timestepCount = 100;
        var timesteps = new List<TrajectoryTimestep>();
        var scalarValues = new List<ScalarTimestep>();
        var eigenvalueTimesteps = new List<EigenTimestep>();

        // Generate smooth trajectory using Perlin-like noise
        double baseX = rng.NextDouble() * 10;
        double baseY = rng.NextDouble() * 10;

        for (int i = 0; i < timestepCount; i++)
        {
            double t = i / (double)(timestepCount - 1);
            double angle = t * Math.PI * 2 * (1 + rng.NextDouble() * 0.3);
            double radius = 0.3 + 0.4 * t + 0.1 * Math.Sin(t * Math.PI * 4);

            // Spiral trajectory with organic variation
            double x = Math.Cos(angle) * radius + Math.Sin(t * 5.7) * 0.1;
            double y = Math.Sin(angle) * radius + Math.Cos(t * 4.3) * 0.1;

            // Compute velocity from position change
            double vx = -Math.Sin(angle) * radius * Math.PI * 2 / timestepCount;
            double vy = Math.Cos(angle) * radius * Math.PI * 2 / timestepCount;

            // Curvature - higher at tight turns
            double curvature = Math.Abs(Math.Sin(t * Math.PI * 3)) * 0.5 + 0.1;

            timesteps.Add(new TrajectoryTimestep
            {
                T = t,
                State2D = [x, y],
                Velocity = [vx, vy],
                Curvature = curvature
            });

            // Generate scalar values (5 dimensions)
            scalarValues.Add(new ScalarTimestep
            {
                T = t,
                Correctness = 0.5 + 0.4 * Math.Sin(t * Math.PI * 2 + 0.0),
                Coherence = 0.5 + 0.3 * Math.Sin(t * Math.PI * 2 + 0.5),
                Calibration = 0.5 + 0.2 * Math.Sin(t * Math.PI * 2 + 1.0),
                Tradeoffs = 0.5 + 0.35 * Math.Sin(t * Math.PI * 2 + 1.5),
                Clarity = 0.5 + 0.25 * Math.Sin(t * Math.PI * 2 + 2.0)
            });

            // Generate eigenvalues (dominant + minor)
            double dominance = condition == "Convergent" 
                ? 0.7 + 0.2 * t  // Path B: one eigenvalue dominates over time
                : 0.4 + 0.1 * Math.Sin(t * Math.PI); // Path A: eigenvalues stay distributed

            eigenvalueTimesteps.Add(new EigenTimestep
            {
                T = t,
                Values = [
                    dominance,
                    (1 - dominance) * 0.6,
                    (1 - dominance) * 0.3,
                    (1 - dominance) * 0.08,
                    (1 - dominance) * 0.02
                ]
            });
        }

        // Generate professor positions (evaluator vectors)
        var professors = new List<ProfessorVector>();
        var professorNames = new[] { "Correctness", "Coherence", "Calibration", "Tradeoffs", "Clarity" };
        for (int i = 0; i < 5; i++)
        {
            double angle = i * Math.PI * 2 / 5;
            professors.Add(new ProfessorVector
            {
                Name = professorNames[i],
                Vector = [Math.Cos(angle) * 0.8, Math.Sin(angle) * 0.8],
                Holdout = i == 4 // Last professor is holdout
            });
        }

        return new GeometryRun
        {
            SchemaVersion = "1.0",
            Metadata = new RunMetadata
            {
                RunId = runId,
                Condition = condition,
                Seed = DemoSeed,
                TrainingItems = 1000,
                Cycles = 10,
                ConscienceTier = "DEMO"
            },
            Reduction = new DimensionalityReduction
            {
                Method = "PCA",
                InputDim = 768,
                OutputDim = 2,
                ExplainedVariance = [0.45, 0.25, 0.15, 0.10, 0.05]
            },
            Trajectory = new Trajectory
            {
                Timesteps = timesteps
            },
            Scalars = new ScalarTimeSeries
            {
                Dimensions = ["Correctness", "Coherence", "Calibration", "Tradeoffs", "Clarity"],
                Values = scalarValues
            },
            Geometry = new GeometryMetrics
            {
                Eigenvalues = eigenvalueTimesteps
            },
            Evaluators = new EvaluatorGeometry
            {
                Professors = professors
            },
            Failures = [] // Demo has no failures
        };
    }

    // === Demo Color Utilities ===

    /// <summary>
    /// Apply demo saturation reduction to a color.
    /// Phase 1: Demo visuals use reduced saturation.
    /// </summary>
    public static SKColor ApplyDemoSaturation(SKColor color)
    {
        if (!Instance.IsDemo) return color;

        color.ToHsl(out float h, out float s, out float l);
        s *= DemoSaturation;
        return SKColor.FromHsl(h, s, l).WithAlpha(color.Alpha);
    }

    /// <summary>
    /// Get interpolated position along demo trajectory at current animation time.
    /// </summary>
    public (double X, double Y) GetAnimatedTrajectoryPoint()
    {
        if (DemoRun?.Trajectory.Timesteps == null || DemoRun.Trajectory.Timesteps.Count == 0)
            return (0, 0);

        var timesteps = DemoRun.Trajectory.Timesteps;
        int idx = (int)(_animationTime * (timesteps.Count - 1));
        idx = Math.Clamp(idx, 0, timesteps.Count - 1);

        var point = timesteps[idx];
        return (point.State2D[0], point.State2D[1]);
    }

    /// <summary>
    /// Get current demo eigenvalues at animation time.
    /// </summary>
    public List<double> GetAnimatedEigenvalues()
    {
        if (DemoRun?.Geometry.Eigenvalues == null || DemoRun.Geometry.Eigenvalues.Count == 0)
            return [0.5, 0.3, 0.15, 0.04, 0.01];

        var eigenvalues = DemoRun.Geometry.Eigenvalues;
        int idx = (int)(_animationTime * (eigenvalues.Count - 1));
        idx = Math.Clamp(idx, 0, eigenvalues.Count - 1);

        return eigenvalues[idx].Values;
    }

    /// <summary>
    /// Get current demo scalar values at animation time.
    /// </summary>
    public List<double> GetAnimatedScalars()
    {
        if (DemoRun?.Scalars.Values == null || DemoRun.Scalars.Values.Count == 0)
            return [0.5, 0.5, 0.5, 0.5, 0.5];

        var scalars = DemoRun.Scalars.Values;
        int idx = (int)(_animationTime * (scalars.Count - 1));
        idx = Math.Clamp(idx, 0, scalars.Count - 1);

        return scalars[idx].ToArray().ToList();
    }

    /// <summary>
    /// Get the color for the DEMO badge.
    /// Uses a subtle, non-intrusive color.
    /// </summary>
    public SKColor GetDemoBadgeColor()
    {
        return SKColor.Parse("#555577");
    }

    // === Geometry Animation (Rotating Manifold) ===

    /// <summary>
    /// Current rotation angle for geometry view manifold.
    /// Rotates slowly and continuously.
    /// </summary>
    public float ManifoldRotationAngle => (float)(_animationTime * Math.PI * 2);

    // === Latent Vector Animation (Overview) ===

    /// <summary>
    /// Generate animated latent vector points for overview background.
    /// Phase 1: Overview never displays blank background.
    /// </summary>
    public List<(double X, double Y, double Size, double Alpha)> GetAnimatedLatentVectors(int count = 20)
    {
        var vectors = new List<(double, double, double, double)>();
        var rng = new Random(DemoSeed);

        for (int i = 0; i < count; i++)
        {
            // Each vector has its own phase offset
            double phase = rng.NextDouble() * Math.PI * 2;
            double speed = 0.5 + rng.NextDouble() * 0.5;
            double baseX = (rng.NextDouble() - 0.5) * 2;
            double baseY = (rng.NextDouble() - 0.5) * 2;
            double size = 0.02 + rng.NextDouble() * 0.03;

            // Animate position with subtle oscillation
            double t = _animationTime * speed + phase;
            double x = baseX + Math.Sin(t * Math.PI * 2) * 0.05;
            double y = baseY + Math.Cos(t * Math.PI * 2) * 0.05;

            // Pulse alpha
            double alpha = 0.3 + 0.2 * Math.Sin(t * Math.PI * 4);

            vectors.Add((x, y, size, alpha));
        }

        return vectors;
    }
}
