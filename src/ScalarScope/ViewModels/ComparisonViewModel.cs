using System.Text.Json;
using ScalarScope.Models;
using ScalarScope.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScalarScope.ViewModels;

/// <summary>
/// ViewModel for side-by-side run comparison.
/// Manages two runs with synchronized playback.
/// </summary>
public partial class ComparisonViewModel : ObservableObject
{
    // Left run (typically Path A / orthogonal)
    [ObservableProperty]
    private GeometryRun? _leftRun;

    [ObservableProperty]
    private string _leftRunName = "Load Path A";

    [ObservableProperty]
    private bool _hasLeftRun;

    // Right run (typically Path B / correlated)
    [ObservableProperty]
    private GeometryRun? _rightRun;

    [ObservableProperty]
    private string _rightRunName = "Load Path B";

    [ObservableProperty]
    private bool _hasRightRun;

    // Comparison state
    [ObservableProperty]
    private bool _hasBothRuns;

    [ObservableProperty]
    private string _comparisonSummary = "";

    [ObservableProperty]
    private string _interpretationVerdict = "";

    [ObservableProperty]
    private string _leftDescription = "";

    [ObservableProperty]
    private string _rightDescription = "";

    // Visual dominance indicators for canvas dimming
    [ObservableProperty]
    private bool? _isLeftDominant;

    [ObservableProperty]
    private bool? _isRightDominant;

    // Demo mode state
    [ObservableProperty]
    private bool _isDemoMode;

    [ObservableProperty]
    private bool _isDemoComplete;

    // Shared playback controller
    public TrajectoryPlayerViewModel Player { get; } = new();

    // Computed properties for current time - Left
    public TrajectoryTimestep? LeftCurrentTrajectory => GetTrajectoryAtTime(LeftRun, Player.Time);
    public ScalarTimestep? LeftCurrentScalars => GetScalarsAtTime(LeftRun, Player.Time);
    public EigenTimestep? LeftCurrentEigenvalues => GetEigenvaluesAtTime(LeftRun, Player.Time);

    // Computed properties for current time - Right
    public TrajectoryTimestep? RightCurrentTrajectory => GetTrajectoryAtTime(RightRun, Player.Time);
    public ScalarTimestep? RightCurrentScalars => GetScalarsAtTime(RightRun, Player.Time);
    public EigenTimestep? RightCurrentEigenvalues => GetEigenvaluesAtTime(RightRun, Player.Time);

    public ComparisonViewModel()
    {
        Player.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged()
    {
        // Invariant check: time must always be valid
        var clampedTime = InvariantGuard.ClampTime(Player.Time, "ComparisonViewModel.OnTimeChanged");
        if (Math.Abs(clampedTime - Player.Time) > 0.001)
        {
            // Time was out of bounds - this should never happen but we handle it
            Player.JumpToTimeCommand.Execute(clampedTime);
        }

        NotifyComputedPropertiesChanged();
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(LeftCurrentTrajectory));
        OnPropertyChanged(nameof(LeftCurrentScalars));
        OnPropertyChanged(nameof(LeftCurrentEigenvalues));
        OnPropertyChanged(nameof(RightCurrentTrajectory));
        OnPropertyChanged(nameof(RightCurrentScalars));
        OnPropertyChanged(nameof(RightCurrentEigenvalues));
    }

    /// <summary>
    /// Reset the left run to initial state.
    /// </summary>
    [RelayCommand]
    public void ResetLeftRun()
    {
        LeftRun = null;
        LeftRunName = "Load Path A";
        HasLeftRun = false;
        UpdateComparisonState();
        NotifyComputedPropertiesChanged();
    }

    /// <summary>
    /// Reset the right run to initial state.
    /// </summary>
    [RelayCommand]
    public void ResetRightRun()
    {
        RightRun = null;
        RightRunName = "Load Path B";
        HasRightRun = false;
        UpdateComparisonState();
        NotifyComputedPropertiesChanged();
    }

    /// <summary>
    /// Reset both runs to initial state.
    /// </summary>
    [RelayCommand]
    public void ResetAll()
    {
        if (Player.IsPlaying)
        {
            Player.PlayPauseCommand.Execute(null);
        }

        LeftRun = null;
        LeftRunName = "Load Path A";
        HasLeftRun = false;

        RightRun = null;
        RightRunName = "Load Path B";
        HasRightRun = false;

        HasBothRuns = false;
        ComparisonSummary = "";
        InterpretationVerdict = "";
        LeftDescription = "";
        RightDescription = "";
        IsLeftDominant = null;
        IsRightDominant = null;

        Player.JumpToTimeCommand.Execute(0.0);
        NotifyComputedPropertiesChanged();
    }

    /// <summary>
    /// Load demo runs directly without file picker.
    /// Called by the demo flow to initialize comparison view.
    /// </summary>
    public void LoadDemoRuns(GeometryRun pathA, GeometryRun pathB)
    {
        // Reset any existing state
        if (Player.IsPlaying)
        {
            Player.PlayPauseCommand.Execute(null);
        }

        // Set demo mode
        IsDemoMode = true;
        IsDemoComplete = false;

        // Subscribe to demo completion
        DemoAnnotationService.DemoCompleted += OnDemoCompleted;

        // Load Path A (orthogonal) on the left
        LeftRun = pathA;
        LeftRunName = pathA.Metadata?.Condition ?? "Path A: Orthogonal";
        HasLeftRun = true;

        // Load Path B (correlated) on the right
        RightRun = pathB;
        RightRunName = pathB.Metadata?.Condition ?? "Path B: Correlated";
        HasRightRun = true;

        // Update comparison state
        UpdateComparisonState();

        // Reset player to start
        Player.JumpToTimeCommand.Execute(0.0);
        NotifyComputedPropertiesChanged();
    }

    private void OnDemoCompleted()
    {
        IsDemoComplete = true;
        DemoService.EndDemo(completed: true);
    }

    /// <summary>
    /// End the demo early (user requested).
    /// </summary>
    [RelayCommand]
    public void EndDemo()
    {
        if (!IsDemoMode) return;

        // Stop playback
        if (Player.IsPlaying)
        {
            Player.PlayPauseCommand.Execute(null);
        }

        // Clean up demo state
        IsDemoMode = false;
        IsDemoComplete = false;
        DemoAnnotationService.DemoCompleted -= OnDemoCompleted;
        DemoService.EndDemo(completed: false);

        // Reset the comparison view
        ResetAll();
    }

    /// <summary>
    /// Silently end demo mode when user takes over with their own file.
    /// Does not reset the view - just cleans up demo state.
    /// </summary>
    private void EndDemoModeIfActive()
    {
        if (!IsDemoMode) return;

        IsDemoMode = false;
        IsDemoComplete = false;
        DemoAnnotationService.DemoCompleted -= OnDemoCompleted;
        DemoService.EndDemo(completed: false);
    }

    [RelayCommand]
    private async Task LoadLeftRunAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Path A / Orthogonal Run",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.macOS, new[] { "json" } },
            })
        });

        if (result != null)
        {
            await LoadLeftFromFileAsync(result.FullPath);
        }
    }

    [RelayCommand]
    private async Task LoadRightRunAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Path B / Correlated Run",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.macOS, new[] { "json" } },
            })
        });

        if (result != null)
        {
            await LoadRightFromFileAsync(result.FullPath);
        }
    }

    public async Task LoadLeftFromFileAsync(string path)
    {
        // End demo mode if user loads their own file
        EndDemoModeIfActive();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var run = JsonSerializer.Deserialize<GeometryRun>(json);

            if (run != null)
            {
                // Invariant check: trajectory must have data
                if (!InvariantGuard.AssertTrajectoryNonEmpty(run, $"LoadLeftFromFileAsync({Path.GetFileName(path)})"))
                {
                    LeftRunName = "Error: No trajectory data";
                    HasLeftRun = false;
                    return;
                }

                // Invariant check: data consistency
                InvariantGuard.AssertDataConsistentLengths(run, $"LoadLeftFromFileAsync({Path.GetFileName(path)})");

                LeftRun = run;
                LeftRunName = Path.GetFileNameWithoutExtension(path);
                HasLeftRun = true;
                UpdateComparisonState();
            }
        }
        catch (Exception ex)
        {
            LeftRunName = $"Error: {ex.Message}";
            HasLeftRun = false;
        }
    }

    public async Task LoadRightFromFileAsync(string path)
    {
        // End demo mode if user loads their own file
        EndDemoModeIfActive();

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var run = JsonSerializer.Deserialize<GeometryRun>(json);

            if (run != null)
            {
                // Invariant check: trajectory must have data
                if (!InvariantGuard.AssertTrajectoryNonEmpty(run, $"LoadRightFromFileAsync({Path.GetFileName(path)})"))
                {
                    RightRunName = "Error: No trajectory data";
                    HasRightRun = false;
                    return;
                }

                // Invariant check: data consistency
                InvariantGuard.AssertDataConsistentLengths(run, $"LoadRightFromFileAsync({Path.GetFileName(path)})");

                RightRun = run;
                RightRunName = Path.GetFileNameWithoutExtension(path);
                HasRightRun = true;
                UpdateComparisonState();
            }
        }
        catch (Exception ex)
        {
            RightRunName = $"Error: {ex.Message}";
            HasRightRun = false;
        }
    }

    private void UpdateComparisonState()
    {
        HasBothRuns = HasLeftRun && HasRightRun;

        if (HasBothRuns)
        {
            GenerateComparisonSummary();
            Player.JumpToTimeCommand.Execute(0.0);
        }
    }

    private void GenerateComparisonSummary()
    {
        if (LeftRun == null || RightRun == null) return;

        // Invariant check: both runs must be valid for comparison
        if (!InvariantGuard.AssertCompareRunsValid(LeftRun, RightRun, "ComparisonViewModel.GenerateComparisonSummary"))
        {
            ComparisonSummary = "Error: One or both runs have invalid data.";
            InterpretationVerdict = "Cannot compare runs with missing trajectory data.";
            return;
        }

        // Invariant check: data consistency
        InvariantGuard.AssertDataConsistentLengths(LeftRun, "ComparisonViewModel.LeftRun");
        InvariantGuard.AssertDataConsistentLengths(RightRun, "ComparisonViewModel.RightRun");

        var leftCondition = LeftRun.Metadata?.Condition ?? "Unknown";
        var rightCondition = RightRun.Metadata?.Condition ?? "Unknown";
        var leftTier = LeftRun.Metadata?.ConscienceTier ?? "?";
        var rightTier = RightRun.Metadata?.ConscienceTier ?? "?";
        var leftFailures = LeftRun.Failures?.Count ?? 0;
        var rightFailures = RightRun.Failures?.Count ?? 0;

        // Compute final eigenvalue comparison with null safety
        var leftFinalEigen = LeftRun.Geometry?.Eigenvalues?.LastOrDefault()?.Values;
        var rightFinalEigen = RightRun.Geometry?.Eigenvalues?.LastOrDefault()?.Values;

        var leftSum = leftFinalEigen?.Sum() ?? 0;
        var rightSum = rightFinalEigen?.Sum() ?? 0;

        var leftFirstFactor = (leftFinalEigen?.Count > 0 && leftSum > 0.001)
            ? leftFinalEigen[0] / leftSum
            : 0;
        var rightFirstFactor = (rightFinalEigen?.Count > 0 && rightSum > 0.001)
            ? rightFinalEigen[0] / rightSum
            : 0;

        ComparisonSummary = $"Left: {leftCondition} ({leftTier}, {leftFailures} failures, λ₁={leftFirstFactor:P0})\n" +
                          $"Right: {rightCondition} ({rightTier}, {rightFailures} failures, λ₁={rightFirstFactor:P0})";

        // Generate descriptions for interpretation strip
        LeftDescription = GenerateRunDescription(LeftRun, leftFirstFactor, "A");
        RightDescription = GenerateRunDescription(RightRun, rightFirstFactor, "B");
        InterpretationVerdict = GenerateVerdict(leftFirstFactor, rightFirstFactor, leftFailures, rightFailures);

        // Determine visual dominance for canvas dimming - use centralized threshold
        var delta = rightFirstFactor - leftFirstFactor;
        if (Math.Abs(delta) < ConsistencyCheckService.FirstFactorDeltaThreshold)
        {
            // Too close to call - no dimming
            IsLeftDominant = null;
            IsRightDominant = null;
        }
        else if (delta > 0)
        {
            // Right (Path B) is dominant
            IsLeftDominant = false;
            IsRightDominant = true;
        }
        else
        {
            // Left (Path A) is dominant
            IsLeftDominant = true;
            IsRightDominant = false;
        }
    }

    private static string GenerateRunDescription(GeometryRun run, double firstFactor, string pathLabel)
    {
        var tier = run.Metadata.ConscienceTier;
        var failures = run.Failures.Count;

        // Use centralized interpretation for consistency
        var interpretation = ConsistencyCheckService.GetEigenInterpretation(firstFactor);

        return interpretation switch
        {
            EigenInterpretation.StrongSharedAxis =>
                $"Path {pathLabel}: Strong eigenvalue dominance ({firstFactor:P0}). Evaluators share structure. {tier} tier with {failures} failure(s).",
            EigenInterpretation.ModerateUnification =>
                $"Path {pathLabel}: Mixed eigenvalue distribution ({firstFactor:P0}). Partial structure sharing. {tier} tier with {failures} failure(s).",
            _ =>
                $"Path {pathLabel}: Distributed eigenvalues ({firstFactor:P0}). Evaluators are orthogonal. {tier} tier with {failures} failure(s)."
        };
    }

    private static string GenerateVerdict(double leftFirst, double rightFirst, int leftFail, int rightFail)
    {
        var delta = rightFirst - leftFirst;

        // Use centralized threshold for consistency
        if (Math.Abs(delta) < ConsistencyCheckService.FirstFactorDeltaThreshold)
            return "Both paths show similar eigenvalue structure. No clear convergence advantage.";
        else if (delta > 0.2)
            return $"Path B shows stronger convergence (Δλ₁ = +{delta:P0}). Correlated professors enable unified representation.";
        else if (delta > 0)
            return $"Path B shows slightly better convergence (Δλ₁ = +{delta:P0}).";
        else if (delta < -0.2)
            return $"Path A maintains diversity (Δλ₁ = {delta:P0}). Orthogonal professors prevent collapse.";
        else
            return $"Path A shows slightly more distributed learning (Δλ₁ = {delta:P0}).";
    }

    // Helper methods for getting data at time t
    private static TrajectoryTimestep? GetTrajectoryAtTime(GeometryRun? run, double t)
    {
        try
        {
            if (run?.Trajectory?.Timesteps is not { Count: > 0 } steps)
                return null;

            var idx = (int)(t * Math.Max(0, steps.Count - 1));
            idx = Math.Clamp(idx, 0, steps.Count - 1);
            return steps[idx];
        }
        catch
        {
            return null;
        }
    }

    private static ScalarTimestep? GetScalarsAtTime(GeometryRun? run, double t)
    {
        try
        {
            if (run?.Scalars?.Values is not { Count: > 0 } values)
                return null;

            var idx = (int)(t * Math.Max(0, values.Count - 1));
            idx = Math.Clamp(idx, 0, values.Count - 1);
            return values[idx];
        }
        catch
        {
            return null;
        }
    }

    private static EigenTimestep? GetEigenvaluesAtTime(GeometryRun? run, double t)
    {
        try
        {
            if (run?.Geometry?.Eigenvalues is not { Count: > 0 } values)
                return null;

            var idx = (int)(t * Math.Max(0, values.Count - 1));
            idx = Math.Clamp(idx, 0, values.Count - 1);
            return values[idx];
        }
        catch
        {
            return null;
        }
    }

    public IEnumerable<TrajectoryTimestep> GetLeftTrajectoryUpToTime(double t)
    {
        if (LeftRun?.Trajectory?.Timesteps is not { Count: > 0 } steps)
            yield break;

        var maxIdx = (int)(t * Math.Max(0, steps.Count - 1));
        maxIdx = Math.Clamp(maxIdx, 0, steps.Count - 1);

        for (int i = 0; i <= maxIdx && i < steps.Count; i++)
        {
            yield return steps[i];
        }
    }

    public IEnumerable<TrajectoryTimestep> GetRightTrajectoryUpToTime(double t)
    {
        if (RightRun?.Trajectory?.Timesteps is not { Count: > 0 } steps)
            yield break;

        var maxIdx = (int)(t * Math.Max(0, steps.Count - 1));
        maxIdx = Math.Clamp(maxIdx, 0, steps.Count - 1);

        for (int i = 0; i <= maxIdx && i < steps.Count; i++)
        {
            yield return steps[i];
        }
    }

    /// <summary>
    /// Compute comparison metrics at current time.
    /// </summary>
    public ComparisonMetrics GetCurrentMetrics()
    {
        var leftEigen = LeftCurrentEigenvalues?.Values ?? [];
        var rightEigen = RightCurrentEigenvalues?.Values ?? [];

        var leftFirstFactor = leftEigen.Count > 0 ? leftEigen[0] / Math.Max(0.001, leftEigen.Sum()) : 0;
        var rightFirstFactor = rightEigen.Count > 0 ? rightEigen[0] / Math.Max(0.001, rightEigen.Sum()) : 0;

        var leftEffDim = LeftCurrentTrajectory?.EffectiveDim ?? 0;
        var rightEffDim = RightCurrentTrajectory?.EffectiveDim ?? 0;

        var leftCurvature = LeftCurrentTrajectory?.Curvature ?? 0;
        var rightCurvature = RightCurrentTrajectory?.Curvature ?? 0;

        return new ComparisonMetrics
        {
            LeftFirstFactorVariance = leftFirstFactor,
            RightFirstFactorVariance = rightFirstFactor,
            LeftEffectiveDim = leftEffDim,
            RightEffectiveDim = rightEffDim,
            LeftCurvature = leftCurvature,
            RightCurvature = rightCurvature,
            FirstFactorDelta = rightFirstFactor - leftFirstFactor,
            EffectiveDimDelta = leftEffDim - rightEffDim, // Lower is more unified
        };
    }
}

public record ComparisonMetrics
{
    public double LeftFirstFactorVariance { get; init; }
    public double RightFirstFactorVariance { get; init; }
    public double LeftEffectiveDim { get; init; }
    public double RightEffectiveDim { get; init; }
    public double LeftCurvature { get; init; }
    public double RightCurvature { get; init; }
    public double FirstFactorDelta { get; init; }
    public double EffectiveDimDelta { get; init; }
}
