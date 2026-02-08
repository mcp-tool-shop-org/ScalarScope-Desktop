using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VortexKit.Core;

/// <summary>
/// Shared playback controller for synchronized time-based visualization.
/// Multiple views bind to the same controller instance.
/// </summary>
public partial class PlaybackController : ObservableObject, IDisposable
{
    private System.Timers.Timer? _playbackTimer;
    private const double DefaultTickInterval = 16.67; // ~60fps

    private static readonly double[] SpeedPresets = [0.25, 0.5, 1.0, 2.0, 4.0];

    /// <summary>
    /// Current playback position (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TimePercent))]
    [NotifyPropertyChangedFor(nameof(TimeDisplay))]
    private double _time;

    /// <summary>
    /// Playback speed multiplier.
    /// </summary>
    [ObservableProperty]
    private double _speed = 1.0;

    /// <summary>
    /// Index into speed presets (0=0.25x, 1=0.5x, 2=1x, 3=2x, 4=4x).
    /// </summary>
    [ObservableProperty]
    private int _speedIndex = 2;

    /// <summary>
    /// Whether playback is currently active.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotPlaying))]
    private bool _isPlaying;

    /// <summary>
    /// Duration in seconds for a full playthrough at 1x speed.
    /// </summary>
    [ObservableProperty]
    private double _duration = 10.0;

    /// <summary>
    /// Whether playback should loop.
    /// </summary>
    [ObservableProperty]
    private bool _loop;

    public double TimePercent => Time * 100;
    public string TimeDisplay => $"{Time:P0}";
    public bool IsNotPlaying => !IsPlaying;

    /// <summary>
    /// Fired whenever time changes (from playback or manual scrubbing).
    /// </summary>
    public event Action? TimeChanged;

    /// <summary>
    /// Fired when playback reaches the end.
    /// </summary>
    public event Action? PlaybackEnded;

    public PlaybackController()
    {
        _playbackTimer = new System.Timers.Timer(DefaultTickInterval);
        _playbackTimer.Elapsed += OnPlaybackTick;
    }

    partial void OnSpeedIndexChanged(int value)
    {
        if (value >= 0 && value < SpeedPresets.Length)
        {
            Speed = SpeedPresets[value];
        }
    }

    [RelayCommand]
    public void PlayPause()
    {
        IsPlaying = !IsPlaying;

        if (IsPlaying)
        {
            // Reset to start if at end
            if (Time >= 1.0)
                Time = 0;

            _playbackTimer?.Start();
        }
        else
        {
            _playbackTimer?.Stop();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        IsPlaying = false;
        _playbackTimer?.Stop();
        Time = 0;
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    public void StepForward(double? stepSize = null)
    {
        var step = stepSize ?? 0.01;
        Time = Math.Min(1.0, Time + step);
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    public void StepBackward(double? stepSize = null)
    {
        var step = stepSize ?? 0.01;
        Time = Math.Max(0.0, Time - step);
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    public void JumpToTime(double t)
    {
        Time = Math.Clamp(t, 0.0, 1.0);
        TimeChanged?.Invoke();
    }

    [RelayCommand]
    public void JumpToStart() => JumpToTime(0.0);

    [RelayCommand]
    public void JumpToEnd() => JumpToTime(1.0);

    public void IncreaseSpeed()
    {
        if (SpeedIndex < SpeedPresets.Length - 1)
            SpeedIndex++;
    }

    public void DecreaseSpeed()
    {
        if (SpeedIndex > 0)
            SpeedIndex--;
    }

    public void SetSpeed(double speed)
    {
        Speed = Math.Clamp(speed, 0.1, 10.0);

        // Find closest preset
        var closestIdx = 0;
        var closestDist = double.MaxValue;
        for (int i = 0; i < SpeedPresets.Length; i++)
        {
            var dist = Math.Abs(SpeedPresets[i] - speed);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestIdx = i;
            }
        }
        SpeedIndex = closestIdx;
    }

    private void OnPlaybackTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        var delta = (DefaultTickInterval / 1000.0) / Duration * Speed;
        Time += delta;

        if (Time >= 1.0)
        {
            if (Loop)
            {
                Time = 0.0;
            }
            else
            {
                Time = 1.0;
                IsPlaying = false;
                _playbackTimer?.Stop();
                PlaybackEnded?.Invoke();
            }
        }

        TimeChanged?.Invoke();
    }

    public void Dispose()
    {
        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
