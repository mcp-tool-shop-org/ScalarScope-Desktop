using System.Text.Json;
using AspireDesktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AspireDesktop.ViewModels;

/// <summary>
/// Global session state - shared across all views.
/// Manages loaded geometry run and playback state.
/// </summary>
public partial class VortexSessionViewModel : ObservableObject
{
    [ObservableProperty]
    private GeometryRun? _run;

    [ObservableProperty]
    private string _runName = "No run loaded";

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private string _condition = "";

    [ObservableProperty]
    private string _conscienceTier = "";

    [ObservableProperty]
    private int _failureCount;

    public TrajectoryPlayerViewModel Player { get; } = new();

    // Computed properties for current time
    public TrajectoryTimestep? CurrentTrajectoryState => GetTrajectoryAtTime(Player.Time);
    public ScalarTimestep? CurrentScalars => GetScalarsAtTime(Player.Time);
    public EigenTimestep? CurrentEigenvalues => GetEigenvaluesAtTime(Player.Time);

    public VortexSessionViewModel()
    {
        Player.TimeChanged += OnTimeChanged;
    }

    private void OnTimeChanged()
    {
        OnPropertyChanged(nameof(CurrentTrajectoryState));
        OnPropertyChanged(nameof(CurrentScalars));
        OnPropertyChanged(nameof(CurrentEigenvalues));
    }

    [RelayCommand]
    private async Task LoadRunAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select ASPIRE Geometry Export",
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
        try
        {
            var json = await File.ReadAllTextAsync(path);
            var run = JsonSerializer.Deserialize<GeometryRun>(json);

            if (run != null)
            {
                Run = run;
                RunName = Path.GetFileNameWithoutExtension(path);
                HasRun = true;
                Condition = run.Metadata.Condition;
                ConscienceTier = run.Metadata.ConscienceTier;
                FailureCount = run.Failures.Count;
                Player.TotalCycles = run.Metadata.Cycles;
                Player.JumpToTimeCommand.Execute(0.0);
            }
        }
        catch (Exception ex)
        {
            RunName = $"Error: {ex.Message}";
            HasRun = false;
        }
    }

    private TrajectoryTimestep? GetTrajectoryAtTime(double t)
    {
        if (Run?.Trajectory.Timesteps is not { Count: > 0 } steps)
            return null;

        var idx = (int)(t * (steps.Count - 1));
        return steps[Math.Clamp(idx, 0, steps.Count - 1)];
    }

    private ScalarTimestep? GetScalarsAtTime(double t)
    {
        if (Run?.Scalars.Values is not { Count: > 0 } values)
            return null;

        var idx = (int)(t * (values.Count - 1));
        return values[Math.Clamp(idx, 0, values.Count - 1)];
    }

    private EigenTimestep? GetEigenvaluesAtTime(double t)
    {
        if (Run?.Geometry.Eigenvalues is not { Count: > 0 } values)
            return null;

        var idx = (int)(t * (values.Count - 1));
        return values[Math.Clamp(idx, 0, values.Count - 1)];
    }

    /// <summary>
    /// Get trajectory points up to current time for rendering path.
    /// </summary>
    public IEnumerable<TrajectoryTimestep> GetTrajectoryUpToTime(double t)
    {
        if (Run?.Trajectory.Timesteps is not { Count: > 0 } steps)
            yield break;

        var maxIdx = (int)(t * (steps.Count - 1));
        for (int i = 0; i <= Math.Min(maxIdx, steps.Count - 1); i++)
        {
            yield return steps[i];
        }
    }

    /// <summary>
    /// Get failures up to current time.
    /// </summary>
    public IEnumerable<FailureEvent> GetFailuresUpToTime(double t)
    {
        if (Run?.Failures is null)
            yield break;

        foreach (var f in Run.Failures.Where(f => f.T <= t))
        {
            yield return f;
        }
    }
}
