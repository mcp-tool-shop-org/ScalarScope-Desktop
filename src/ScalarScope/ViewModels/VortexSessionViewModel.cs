using System.Text.Json;
using ScalarScope.Models;
using ScalarScope.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScalarScope.ViewModels;

/// <summary>
/// Global session state - shared across all views.
/// Manages loaded geometry run and playback state.
/// </summary>
public partial class VortexSessionViewModel : ObservableObject
{
    private readonly InteractiveProbingService _probingService = new();
    [ObservableProperty]
    private GeometryRun? _run;

    [ObservableProperty]
    private string _runName = "No training run loaded yet";

    [ObservableProperty]
    private string? _loadedFilePath;

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private string _loadError = "";

    [ObservableProperty]
    private bool _hasLoadError;

    [ObservableProperty]
    private List<string> _loadWarnings = [];

    // Loading state for shimmer animation
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Loading...";

    // First-run state (checked from UserPreferencesService)
    [ObservableProperty]
    private bool _isFirstRun;

    /// <summary>
    /// True when we should show the empty state panel (not first run, no data loaded).
    /// </summary>
    public bool ShowEmptyState => !IsFirstRun && !HasRun;

    [ObservableProperty]
    private string _condition = "";

    [ObservableProperty]
    private string _conscienceTier = "";

    [ObservableProperty]
    private int _failureCount;

    // Phase 3.3: Interactive Probing
    [ObservableProperty]
    private int? _selectedTimestepIndex;

    [ObservableProperty]
    private bool _isProbingMode;

    [ObservableProperty]
    private WhatIfScenario? _activeWhatIfScenario;

    public TrajectoryPlayerViewModel Player { get; } = new();

    // Computed properties for current time
    public TrajectoryTimestep? CurrentTrajectoryState => GetTrajectoryAtTime(Player.Time);
    public ScalarTimestep? CurrentScalars => GetScalarsAtTime(Player.Time);
    public EigenTimestep? CurrentEigenvalues => GetEigenvaluesAtTime(Player.Time);

    public VortexSessionViewModel()
    {
        Player.TimeChanged += OnTimeChanged;
        // Check first-run state on construction
        RefreshFirstRunState();
    }

    /// <summary>
    /// Refresh the first-run state from UserPreferencesService.
    /// Call this after dismissing the first-run UI.
    /// </summary>
    public void RefreshFirstRunState()
    {
        IsFirstRun = UserPreferencesService.IsFirstRun;
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsFirstRunChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnHasRunChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    private void OnTimeChanged()
    {
        // Invariant check: time must always be valid
        var clampedTime = InvariantGuard.ClampTime(Player.Time, "VortexSessionViewModel.OnTimeChanged");
        if (Math.Abs(clampedTime - Player.Time) > 0.001)
        {
            // Time was out of bounds - this should never happen but we handle it
            Player.JumpToTimeCommand.Execute(clampedTime);
        }

        OnPropertyChanged(nameof(CurrentTrajectoryState));
        OnPropertyChanged(nameof(CurrentScalars));
        OnPropertyChanged(nameof(CurrentEigenvalues));
    }

    #region Interactive Probing (Phase 3.3)

    /// <summary>
    /// Select a trajectory point by world coordinates (from click).
    /// </summary>
    public void SelectPointAtPosition(double worldX, double worldY, double maxDistance = 0.5)
    {
        if (Run == null) return;

        var index = _probingService.FindNearestTimestep(Run, worldX, worldY, maxDistance);
        if (index != null)
        {
            SelectedTimestepIndex = index;

            // Jump playback to this time
            var trajectory = Run.Trajectory?.Timesteps;
            if (trajectory != null && index.Value < trajectory.Count)
            {
                var time = trajectory[index.Value].T;
                // Normalize time to [0,1] for the player
                var maxTime = trajectory.Count > 0 ? trajectory[^1].T : 1.0;
                var normalizedTime = maxTime > 0 ? time / maxTime : 0;
                Player.JumpToTimeCommand.Execute(Math.Clamp(normalizedTime, 0, 1));
            }
        }
    }

    /// <summary>
    /// Select a timestep by index directly.
    /// </summary>
    [RelayCommand]
    public void SelectTimestep(int index)
    {
        if (Run?.Trajectory?.Timesteps == null) return;
        if (index < 0 || index >= Run.Trajectory.Timesteps.Count) return;

        SelectedTimestepIndex = index;
    }

    /// <summary>
    /// Clear the selected point.
    /// </summary>
    [RelayCommand]
    public void ClearSelection()
    {
        SelectedTimestepIndex = null;
    }

    /// <summary>
    /// Get the current time travel state for the selected point.
    /// </summary>
    public TimeTravelState? GetSelectedState()
    {
        if (Run == null || SelectedTimestepIndex == null) return null;
        return _probingService.GetStateAt(Run, SelectedTimestepIndex.Value);
    }

    /// <summary>
    /// Get gradient inspection for the selected point.
    /// </summary>
    public GradientInspection? GetSelectedGradient()
    {
        if (Run == null || SelectedTimestepIndex == null) return null;
        return _probingService.InspectGradient(Run, SelectedTimestepIndex.Value);
    }

    /// <summary>
    /// Get what-if projection for the selected point.
    /// </summary>
    public WhatIfProjection? GetWhatIfProjection()
    {
        if (Run == null || SelectedTimestepIndex == null || ActiveWhatIfScenario == null) return null;
        return _probingService.ProjectHypothetical(Run, SelectedTimestepIndex.Value, ActiveWhatIfScenario);
    }

    /// <summary>
    /// Apply a preset what-if scenario.
    /// </summary>
    [RelayCommand]
    public void ApplyWhatIfScenario(string scenarioName)
    {
        ActiveWhatIfScenario = scenarioName switch
        {
            "HighLR" => WhatIfScenario.HigherLearningRate,
            "LowLR" => WhatIfScenario.LowerLearningRate,
            "HighMomentum" => WhatIfScenario.MoreMomentum,
            "LowMomentum" => WhatIfScenario.LessMomentum,
            "Noise" => WhatIfScenario.WithNoise,
            "TurnLeft" => WhatIfScenario.TurnLeft,
            "TurnRight" => WhatIfScenario.TurnRight,
            "None" => null,
            _ => WhatIfScenario.Default
        };
    }

    #endregion

    [RelayCommand]
    private async Task LoadRunAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select Training Dynamics Export",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.macOS, new[] { "json" } },
            })
        });

        if (result != null)
        {
            await LoadFromFileAsync(result.FullPath);
        }
    }

    public async Task LoadFromFileAsync(string path)
    {
        // Full state reset before loading new file
        ResetState();

        // Show loading state
        IsLoading = true;
        LoadingMessage = $"Loading {Path.GetFileName(path)}...";

        try
        {
            var result = await FileValidationService.ValidateAndLoadAsync(path);

            if (!result.IsSuccess)
            {
                RunName = result.ErrorTitle ?? "Error loading file";
                LoadError = result.GetFormattedError();
                HasLoadError = true;
                HasRun = false;
                return;
            }

            LoadingMessage = "Processing trajectory data...";
            var run = result.Run!;

            // Post-load invariant checks
            InvariantGuard.AssertTrajectoryMonotonic(run.Trajectory?.Timesteps, $"LoadFromFileAsync({Path.GetFileName(path)})");
            InvariantGuard.AssertDataConsistentLengths(run, $"LoadFromFileAsync({Path.GetFileName(path)})");

            Run = run;
            RunName = Path.GetFileNameWithoutExtension(path);
            LoadedFilePath = path;
            HasRun = true;
            HasLoadError = false;
            LoadWarnings = result.Warnings;
            Condition = run.Metadata?.Condition ?? "";
            ConscienceTier = run.Metadata?.ConscienceTier ?? "UNKNOWN";
            FailureCount = run.Failures?.Count ?? 0;
            Player.TotalCycles = run.Metadata?.Cycles ?? run.Trajectory?.Timesteps?.Count ?? 0;
            Player.ConfigureForRunSize(run.Trajectory?.Timesteps?.Count ?? 0);
            Player.JumpToTimeCommand.Execute(0.0);

            // Add to recent files list
            UserPreferencesService.AddRecentFile(path, RunName);

            // Notify all computed properties have changed
            NotifyComputedPropertiesChanged();
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Completely resets session state to initial values.
    /// Called before loading a new file to ensure clean state.
    /// </summary>
    [RelayCommand]
    public void ResetState()
    {
        // Stop any playback
        if (Player.IsPlaying)
        {
            Player.PlayPauseCommand.Execute(null);
        }

        // Clear run data
        Run = null;
        RunName = "No training run loaded yet";
        LoadedFilePath = null;
        HasRun = false;

        // Clear error state
        HasLoadError = false;
        LoadError = "";
        LoadWarnings = [];

        // Clear metadata
        Condition = "";
        ConscienceTier = "";
        FailureCount = 0;

        // Reset player state
        Player.TotalCycles = 0;
        Player.JumpToTimeCommand.Execute(0.0);
        Player.ConfigureForRunSize(0);

        // Notify all computed properties
        NotifyComputedPropertiesChanged();
    }

    private void NotifyComputedPropertiesChanged()
    {
        OnPropertyChanged(nameof(CurrentTrajectoryState));
        OnPropertyChanged(nameof(CurrentScalars));
        OnPropertyChanged(nameof(CurrentEigenvalues));
    }

    private TrajectoryTimestep? GetTrajectoryAtTime(double t)
    {
        try
        {
            if (Run?.Trajectory?.Timesteps is not { Count: > 0 } steps)
                return null;

            var idx = (int)(t * (steps.Count - 1));
            idx = Math.Clamp(idx, 0, steps.Count - 1);
            return steps[idx];
        }
        catch
        {
            return null;
        }
    }

    private ScalarTimestep? GetScalarsAtTime(double t)
    {
        try
        {
            if (Run?.Scalars?.Values is not { Count: > 0 } values)
                return null;

            var idx = (int)(t * (values.Count - 1));
            idx = Math.Clamp(idx, 0, values.Count - 1);
            return values[idx];
        }
        catch
        {
            return null;
        }
    }

    private EigenTimestep? GetEigenvaluesAtTime(double t)
    {
        try
        {
            if (Run?.Geometry?.Eigenvalues is not { Count: > 0 } values)
                return null;

            var idx = (int)(t * (values.Count - 1));
            idx = Math.Clamp(idx, 0, values.Count - 1);
            return values[idx];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get trajectory points up to current time for rendering path.
    /// Supports frame skipping for large runs.
    /// </summary>
    public IEnumerable<TrajectoryTimestep> GetTrajectoryUpToTime(double t, int? frameSkipOverride = null)
    {
        if (Run?.Trajectory?.Timesteps is not { Count: > 0 } steps)
            yield break;

        var maxIdx = (int)(t * Math.Max(0, steps.Count - 1));
        maxIdx = Math.Clamp(maxIdx, 0, steps.Count - 1);
        var skip = Math.Max(1, frameSkipOverride ?? Player.FrameSkip);

        for (int i = 0; i <= maxIdx; i += skip)
        {
            if (i < steps.Count)
                yield return steps[i];
        }

        // Always include the last point for visual continuity
        if (maxIdx > 0 && maxIdx % skip != 0 && maxIdx < steps.Count)
        {
            yield return steps[maxIdx];
        }
    }

    /// <summary>
    /// Get failures up to current time.
    /// </summary>
    public IEnumerable<FailureEvent> GetFailuresUpToTime(double t)
    {
        if (Run?.Failures is null || Run.Failures.Count == 0)
            yield break;

        foreach (var f in Run.Failures.Where(f => f != null && f.T <= t))
        {
            yield return f;
        }
    }
}
