using System.Numerics;

namespace ScalarScope.Services;

/// <summary>
/// Real-time playback controller for trajectory animation.
/// Phase 4.2 - Real-Time Playback
/// </summary>
public class PlaybackController : IDisposable
{
    private readonly ParticleSystem _particleSystem = new();
    private readonly MotionBlurEffect _motionBlurEffect = new();
    private readonly CinematicController _cinematicController = new();

    private CancellationTokenSource? _playbackCts;
    private double _currentTime;
    private double _totalDuration;
    private bool _isPlaying;
    private float _playbackSpeed = 1.0f;
    private bool _loop;

    #region Properties

    public double CurrentTime => _currentTime;
    public double TotalDuration => _totalDuration;
    public bool IsPlaying => _isPlaying;
    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set => _playbackSpeed = Math.Clamp(value, 0.1f, 10f);
    }
    public bool Loop
    {
        get => _loop;
        set => _loop = value;
    }

    public double Progress => _totalDuration > 0 ? _currentTime / _totalDuration : 0;

    public ParticleSystem ParticleSystem => _particleSystem;
    public MotionBlurEffect MotionBlurEffect => _motionBlurEffect;
    public CinematicController CinematicController => _cinematicController;

    #endregion

    #region Events

    public event Action<double>? TimeChanged;
    public event Action<int>? FrameChanged;
    public event Action? PlaybackStarted;
    public event Action? PlaybackPaused;
    public event Action? PlaybackStopped;
    public event Action? PlaybackCompleted;

    #endregion

    #region Configuration

    public bool EnableParticles { get; set; } = true;
    public bool EnableMotionBlur { get; set; } = true;
    public int TargetFrameRate { get; set; } = 60;

    #endregion

    private List<Vector2>? _trajectoryPoints;
    private List<double>? _pointTimes;
    private int _currentFrameIndex;
    private Vector2 _lastEmitPosition;

    /// <summary>
    /// Set the trajectory data for playback.
    /// </summary>
    public void SetTrajectory(IReadOnlyList<Vector2> points, IReadOnlyList<double> times)
    {
        if (points.Count != times.Count || points.Count == 0)
            throw new ArgumentException("Points and times must have the same non-zero count.");

        _trajectoryPoints = [..points];
        _pointTimes = [..times];
        _totalDuration = times[^1] - times[0];
        _currentTime = 0;
        _currentFrameIndex = 0;

        if (_trajectoryPoints.Count > 0)
            _lastEmitPosition = _trajectoryPoints[0];

        _particleSystem.Clear();
    }

    /// <summary>
    /// Start or resume playback.
    /// </summary>
    public void Play()
    {
        if (_isPlaying || _trajectoryPoints == null) return;

        _isPlaying = true;
        _playbackCts = new CancellationTokenSource();

        PlaybackStarted?.Invoke();
        _ = RunPlaybackLoopAsync(_playbackCts.Token);
    }

    /// <summary>
    /// Pause playback.
    /// </summary>
    public void Pause()
    {
        if (!_isPlaying) return;

        _playbackCts?.Cancel();
        _isPlaying = false;
        PlaybackPaused?.Invoke();
    }

    /// <summary>
    /// Stop playback and reset to beginning.
    /// </summary>
    public void Stop()
    {
        Pause();
        _currentTime = 0;
        _currentFrameIndex = 0;
        _particleSystem.Clear();

        if (_trajectoryPoints?.Count > 0)
            _lastEmitPosition = _trajectoryPoints[0];

        PlaybackStopped?.Invoke();
    }

    /// <summary>
    /// Seek to a specific time.
    /// </summary>
    public void SeekTo(double time)
    {
        if (_trajectoryPoints == null || _pointTimes == null) return;

        _currentTime = Math.Clamp(time, 0, _totalDuration);
        _currentFrameIndex = FindFrameAtTime(_currentTime);

        if (_currentFrameIndex < _trajectoryPoints.Count)
            _lastEmitPosition = _trajectoryPoints[_currentFrameIndex];

        _particleSystem.Clear();
        TimeChanged?.Invoke(_currentTime);
        FrameChanged?.Invoke(_currentFrameIndex);
    }

    /// <summary>
    /// Step forward one frame.
    /// </summary>
    public void StepForward()
    {
        if (_trajectoryPoints == null || _pointTimes == null) return;

        _currentFrameIndex = Math.Min(_currentFrameIndex + 1, _trajectoryPoints.Count - 1);
        _currentTime = _pointTimes[_currentFrameIndex] - _pointTimes[0];

        UpdateParticlesForFrame();

        TimeChanged?.Invoke(_currentTime);
        FrameChanged?.Invoke(_currentFrameIndex);
    }

    /// <summary>
    /// Step backward one frame.
    /// </summary>
    public void StepBackward()
    {
        if (_trajectoryPoints == null || _pointTimes == null) return;

        _currentFrameIndex = Math.Max(_currentFrameIndex - 1, 0);
        _currentTime = _pointTimes[_currentFrameIndex] - _pointTimes[0];

        TimeChanged?.Invoke(_currentTime);
        FrameChanged?.Invoke(_currentFrameIndex);
    }

    /// <summary>
    /// Get the current frame index.
    /// </summary>
    public int GetCurrentFrameIndex() => _currentFrameIndex;

    /// <summary>
    /// Get the position at the current time.
    /// </summary>
    public Vector2? GetCurrentPosition()
    {
        if (_trajectoryPoints == null || _currentFrameIndex >= _trajectoryPoints.Count)
            return null;

        return _trajectoryPoints[_currentFrameIndex];
    }

    /// <summary>
    /// Get interpolated position at exact current time.
    /// </summary>
    public Vector2? GetInterpolatedPosition()
    {
        if (_trajectoryPoints == null || _pointTimes == null || _trajectoryPoints.Count == 0)
            return null;

        var index = FindFrameAtTime(_currentTime);
        if (index >= _trajectoryPoints.Count - 1)
            return _trajectoryPoints[^1];

        var t1 = _pointTimes[index] - _pointTimes[0];
        var t2 = _pointTimes[index + 1] - _pointTimes[0];
        var t = (t2 > t1) ? (_currentTime - t1) / (t2 - t1) : 0;

        return Vector2.Lerp(_trajectoryPoints[index], _trajectoryPoints[index + 1], (float)t);
    }

    private async Task RunPlaybackLoopAsync(CancellationToken ct)
    {
        var frameInterval = 1000.0 / TargetFrameRate;
        var lastFrameTime = DateTime.Now;

        try
        {
            while (!ct.IsCancellationRequested && _isPlaying)
            {
                var now = DateTime.Now;
                var dt = (now - lastFrameTime).TotalSeconds;
                lastFrameTime = now;

                // Advance time
                _currentTime += dt * _playbackSpeed;

                // Check for loop or completion
                if (_currentTime >= _totalDuration)
                {
                    if (_loop)
                    {
                        _currentTime %= _totalDuration;
                        _particleSystem.Clear();
                    }
                    else
                    {
                        _currentTime = _totalDuration;
                        _isPlaying = false;
                        PlaybackCompleted?.Invoke();
                        break;
                    }
                }

                // Update frame index
                var newFrameIndex = FindFrameAtTime(_currentTime);
                if (newFrameIndex != _currentFrameIndex)
                {
                    _currentFrameIndex = newFrameIndex;
                    FrameChanged?.Invoke(_currentFrameIndex);
                }

                // Update particles
                if (EnableParticles)
                {
                    UpdateParticlesForFrame();
                    _particleSystem.Update((float)dt);
                }

                TimeChanged?.Invoke(_currentTime);

                // Wait for next frame
                var elapsed = (DateTime.Now - now).TotalMilliseconds;
                var sleepTime = Math.Max(1, frameInterval - elapsed);
                await Task.Delay((int)sleepTime, ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void UpdateParticlesForFrame()
    {
        if (!EnableParticles || _trajectoryPoints == null) return;

        var currentPos = GetInterpolatedPosition() ?? _lastEmitPosition;
        var velocity = currentPos - _lastEmitPosition;
        _lastEmitPosition = currentPos;

        _particleSystem.Update(0.016f, currentPos, velocity * 60f); // Scale velocity for 60fps
    }

    private int FindFrameAtTime(double time)
    {
        if (_pointTimes == null || _pointTimes.Count == 0) return 0;

        var targetTime = time + _pointTimes[0];

        for (int i = 0; i < _pointTimes.Count - 1; i++)
        {
            if (targetTime >= _pointTimes[i] && targetTime < _pointTimes[i + 1])
                return i;
        }

        return _pointTimes.Count - 1;
    }

    public void Dispose()
    {
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _cinematicController.Stop();
        GC.SuppressFinalize(this);
    }
}
