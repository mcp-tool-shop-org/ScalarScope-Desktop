using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScalarScope.Services;

namespace ScalarScope.ViewModels;

/// <summary>
/// Shared playback controller for synchronized visualization.
/// All views bind to the same instance.
/// </summary>
public partial class TrajectoryPlayerViewModel : ObservableObject, IDisposable
{
    private System.Timers.Timer? _playbackTimer;
    private const double TickIntervalMs = 16.67; // ~60fps

    private static readonly double[] SpeedValues = [0.25, 0.5, 1.0, 2.0, 4.0];

    // Smooth speed transition
    private double _targetSpeed = 1.0;
    private const double SpeedTransitionRate = 0.15; // Smooth interpolation factor

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimePercent))]
    [NotifyPropertyChangedFor(nameof(TimeDisplay))]
    [NotifyPropertyChangedFor(nameof(CurrentFrame))]
    private double _time; // 0.0 to 1.0

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedDisplay))]
    private double _speed = 1.0; // 0.25x to 4x

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedDisplay))]
    private int _speedIndex = 2; // Index into speed array: 0=0.25, 1=0.5, 2=1, 3=2, 4=4

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseIcon))]
    private bool _isPlaying;

    [ObservableProperty]
    private int _totalCycles;

    [ObservableProperty]
    private double _duration = 10.0; // seconds for full playthrough at 1x

    // Speed change visual feedback
    [ObservableProperty]
    private bool _showSpeedIndicator;

    // Performance settings
    [ObservableProperty]
    private bool _isLargeRun;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameSkipDisplay))]
    private int _frameSkip = 1; // 1 = no skip, 2 = every other frame, etc.

    [ObservableProperty]
    private bool _reducedDetailMode;

    private const int LargeRunThreshold = 10_000; // timesteps
    private const int VeryLargeRunThreshold = 30_000;

    public double TimePercent => Time * 100;

    public string TimeDisplay => $"{Time:P0}";

    public string SpeedDisplay => SpeedIndex switch
    {
        0 => "0.25×",
        1 => "0.5×",
        2 => "1×",
        3 => "2×",
        4 => "4×",
        _ => "1×"
    };

    public int CurrentFrame => TotalCycles > 0 ? (int)(Time * TotalCycles) : 0;

    public string FrameSkipDisplay => FrameSkip switch
    {
        1 => "Full detail",
        2 => "Skip every 2nd",
        4 => "Skip every 4th",
        _ => $"Skip {FrameSkip}×"
    };

    public string PlayPauseIcon => IsPlaying ? "pause.png" : "play.png";

    public event Action? TimeChanged;

    public TrajectoryPlayerViewModel()
    {
        _playbackTimer = new System.Timers.Timer(TickIntervalMs);
        _playbackTimer.Elapsed += OnPlaybackTick;
    }

    partial void OnSpeedIndexChanged(int value)
    {
        if (value >= 0 && value < SpeedValues.Length)
        {
            _targetSpeed = SpeedValues[value];
            // Show speed indicator briefly
            ShowSpeedIndicator = true;
            _ = HideSpeedIndicatorAfterDelay();
        }
    }

    private async Task HideSpeedIndicatorAfterDelay()
    {
        await Task.Delay(1500);
        ShowSpeedIndicator = false;
    }

    [RelayCommand]
    private void PlayPause()
    {
        IsPlaying = !IsPlaying;

        if (IsPlaying)
        {
            _playbackTimer?.Start();
        }
        else
        {
            _playbackTimer?.Stop();
        }
    }

    [RelayCommand]
    private void Stop()
    {
        IsPlaying = false;
        _playbackTimer?.Stop();
        Time = 0;
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    private void StepForward()
    {
        Time = Math.Min(1.0, Time + 0.01);
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    private void StepBackward()
    {
        Time = Math.Max(0.0, Time - 0.01);
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    private void JumpToTime(double t)
    {
        Time = InvariantGuard.ClampTime(t, "JumpToTime");
        TimeChanged?.Invoke();
    }

    public void SetSpeed(double speed)
    {
        Speed = Math.Clamp(speed, 0.25, 4.0);

        // Update SpeedIndex to match
        for (int i = 0; i < SpeedValues.Length; i++)
        {
            if (Math.Abs(SpeedValues[i] - speed) < 0.01)
            {
                SpeedIndex = i;
                break;
            }
        }
    }

    public void IncreaseSpeed()
    {
        if (SpeedIndex < SpeedValues.Length - 1)
            SpeedIndex++;
    }

    public void DecreaseSpeed()
    {
        if (SpeedIndex > 0)
            SpeedIndex--;
    }

    /// <summary>
    /// Configure performance settings based on run size.
    /// Called after loading a run.
    /// </summary>
    public void ConfigureForRunSize(int timestepCount)
    {
        if (timestepCount >= VeryLargeRunThreshold)
        {
            IsLargeRun = true;
            FrameSkip = 4;
            ReducedDetailMode = true;
        }
        else if (timestepCount >= LargeRunThreshold)
        {
            IsLargeRun = true;
            FrameSkip = 2;
            ReducedDetailMode = false;
        }
        else
        {
            IsLargeRun = false;
            FrameSkip = 1;
            ReducedDetailMode = false;
        }
    }

    public void CycleFrameSkip()
    {
        FrameSkip = FrameSkip switch
        {
            1 => 2,
            2 => 4,
            4 => 1,
            _ => 1
        };
    }

    private void OnPlaybackTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        // Smooth speed transition (ease toward target)
        if (Math.Abs(Speed - _targetSpeed) > 0.01)
        {
            Speed += ((_targetSpeed - Speed) * SpeedTransitionRate);
        }
        else
        {
            Speed = _targetSpeed;
        }

        var delta = (TickIntervalMs / 1000.0) / Duration * Speed;
        Time += delta;

        if (Time >= 1.0)
        {
            Time = 1.0;
            IsPlaying = false;
            _playbackTimer?.Stop();
        }

        // Check for demo annotations at current time
        DemoAnnotationService.CheckTimeThreshold(Time);

        TimeChanged?.Invoke();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _playbackTimer?.Stop();
            _playbackTimer?.Dispose();
            _playbackTimer = null;
        }
    }
}
