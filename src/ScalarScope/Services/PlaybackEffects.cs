using SkiaSharp;
using System.Numerics;

namespace ScalarScope.Services;

/// <summary>
/// Particle system for trajectory trail effects.
/// Phase 4.2 - Real-Time Playback
/// </summary>
public class ParticleSystem
{
    #region Configuration

    public int MaxParticles { get; set; } = 100;
    public float ParticleSize { get; set; } = 4f;
    public float ParticleSizeDecay { get; set; } = 0.95f;
    public float ParticleFadeRate { get; set; } = 0.02f;
    public float SpawnRate { get; set; } = 2f; // Particles per frame
    public float InitialVelocityScale { get; set; } = 0.3f;
    public float SpreadAngle { get; set; } = 30f; // Degrees
    public float Gravity { get; set; } = 0f;
    public float Friction { get; set; } = 0.98f;
    public bool UseGlow { get; set; } = true;
    public float GlowRadius { get; set; } = 8f;

    #endregion

    private readonly List<Particle> _particles = [];
    private readonly Random _random = new();
    private float _spawnAccumulator;

    /// <summary>
    /// Update particle system state.
    /// </summary>
    public void Update(float dt, Vector2? emitPosition = null, Vector2? emitVelocity = null)
    {
        // Spawn new particles
        if (emitPosition.HasValue)
        {
            _spawnAccumulator += SpawnRate;
            while (_spawnAccumulator >= 1f && _particles.Count < MaxParticles)
            {
                SpawnParticle(emitPosition.Value, emitVelocity ?? Vector2.Zero);
                _spawnAccumulator -= 1f;
            }
        }

        // Update existing particles
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];

            // Apply physics
            p.Velocity.Y += Gravity * dt;
            p.Velocity *= Friction;
            p.Position += p.Velocity * dt;

            // Decay
            p.Life -= ParticleFadeRate;
            p.Size *= ParticleSizeDecay;

            // Remove dead particles
            if (p.Life <= 0 || p.Size < 0.5f)
            {
                _particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Spawn a single particle at the given position.
    /// </summary>
    private void SpawnParticle(Vector2 position, Vector2 baseVelocity)
    {
        // Add some randomness to velocity direction
        var spreadRad = SpreadAngle * MathF.PI / 180f;
        var angle = (float)(_random.NextDouble() - 0.5) * spreadRad;
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);

        var velocity = new Vector2(
            baseVelocity.X * cos - baseVelocity.Y * sin,
            baseVelocity.X * sin + baseVelocity.Y * cos
        ) * InitialVelocityScale;

        // Add some random velocity component
        velocity += new Vector2(
            (float)(_random.NextDouble() - 0.5) * 20f,
            (float)(_random.NextDouble() - 0.5) * 20f
        );

        _particles.Add(new Particle
        {
            Position = position,
            Velocity = velocity,
            Life = 1f,
            Size = ParticleSize * (0.8f + (float)_random.NextDouble() * 0.4f),
            ColorOffset = (float)_random.NextDouble()
        });
    }

    /// <summary>
    /// Draw all particles to the canvas.
    /// </summary>
    public void Draw(SKCanvas canvas, SKColor baseColor, Func<Vector2, SKPoint>? worldToScreen = null)
    {
        if (_particles.Count == 0) return;

        worldToScreen ??= (v) => new SKPoint(v.X, v.Y);

        // Draw in reverse order (oldest first, newest on top)
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            var screenPos = worldToScreen(p.Position);

            // Calculate alpha based on life
            var alpha = (byte)(255 * p.Life * p.Life); // Quadratic fade

            // Slight color variation
            var colorShift = (int)(p.ColorOffset * 30);
            var color = new SKColor(
                (byte)Math.Clamp(baseColor.Red + colorShift, 0, 255),
                (byte)Math.Clamp(baseColor.Green - colorShift / 2, 0, 255),
                (byte)Math.Clamp(baseColor.Blue + colorShift / 2, 0, 255),
                alpha
            );

            // Draw glow
            if (UseGlow && p.Life > 0.3f)
            {
                using var glowPaint = new SKPaint
                {
                    Color = color.WithAlpha((byte)(alpha / 3)),
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, GlowRadius * p.Life)
                };
                canvas.DrawCircle(screenPos, p.Size * 2, glowPaint);
            }

            // Draw particle
            using var paint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawCircle(screenPos, p.Size, paint);
        }
    }

    /// <summary>
    /// Clear all particles.
    /// </summary>
    public void Clear()
    {
        _particles.Clear();
        _spawnAccumulator = 0;
    }

    public int ParticleCount => _particles.Count;

    private class Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Life;
        public float Size;
        public float ColorOffset;
    }
}

/// <summary>
/// Motion blur effect for trajectory rendering.
/// Phase 4.2 - Real-Time Playback
/// </summary>
public class MotionBlurEffect
{
    public int SampleCount { get; set; } = 8;
    public float BlurStrength { get; set; } = 0.5f;

    /// <summary>
    /// Draw a trajectory segment with motion blur.
    /// </summary>
    public void DrawBlurredSegment(
        SKCanvas canvas,
        SKPoint start,
        SKPoint end,
        SKColor color,
        float strokeWidth,
        float velocity)
    {
        if (velocity < 0.1f)
        {
            // No blur for slow segments
            using var solidPaint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(start, end, solidPaint);
            return;
        }

        // Calculate blur extent based on velocity
        var blurExtent = Math.Min(velocity * BlurStrength, 50f);
        var direction = new SKPoint(end.X - start.X, end.Y - start.Y);
        var length = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        
        if (length < 0.1f) return;
        
        direction = new SKPoint(direction.X / length, direction.Y / length);

        // Draw multiple semi-transparent samples
        for (int i = 0; i < SampleCount; i++)
        {
            var t = (float)i / (SampleCount - 1);
            var alpha = (byte)(255 * (1f - t) / SampleCount * 2);
            var offset = t * blurExtent;

            var blurStart = new SKPoint(
                start.X - direction.X * offset,
                start.Y - direction.Y * offset
            );

            using var blurPaint = new SKPaint
            {
                Color = color.WithAlpha(alpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth * (1f - t * 0.5f),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };
            canvas.DrawLine(blurStart, end, blurPaint);
        }
    }
}

/// <summary>
/// Cinematic camera controller for automated camera movements.
/// Phase 4.2 - Real-Time Playback
/// </summary>
public class CinematicController
{
    private readonly List<CameraKeyframe> _keyframes = [];
    private CinematicMode _currentMode = CinematicMode.None;
    private double _animationTime;
    private bool _isPlaying;
    private CancellationTokenSource? _cts;

    public event Action<CameraState>? CameraUpdated;
    public event Action? AnimationCompleted;

    public bool IsPlaying => _isPlaying;
    public CinematicMode CurrentMode => _currentMode;

    /// <summary>
    /// Start a cinematic mode.
    /// </summary>
    public void StartMode(CinematicMode mode, double duration = 5.0)
    {
        Stop();
        _currentMode = mode;

        switch (mode)
        {
            case CinematicMode.SlowZoomIn:
                AddKeyframe(new CameraKeyframe(0, 0, 0, 1.0f, EaseType.EaseInOut));
                AddKeyframe(new CameraKeyframe(duration, 0, 0, 2.0f, EaseType.EaseInOut));
                break;

            case CinematicMode.SlowZoomOut:
                AddKeyframe(new CameraKeyframe(0, 0, 0, 2.0f, EaseType.EaseInOut));
                AddKeyframe(new CameraKeyframe(duration, 0, 0, 1.0f, EaseType.EaseInOut));
                break;

            case CinematicMode.PanRight:
                AddKeyframe(new CameraKeyframe(0, -200, 0, 1.0f, EaseType.EaseInOut));
                AddKeyframe(new CameraKeyframe(duration, 200, 0, 1.0f, EaseType.EaseInOut));
                break;

            case CinematicMode.PanLeft:
                AddKeyframe(new CameraKeyframe(0, 200, 0, 1.0f, EaseType.EaseInOut));
                AddKeyframe(new CameraKeyframe(duration, -200, 0, 1.0f, EaseType.EaseInOut));
                break;

            case CinematicMode.OrbitSpin:
                // 360 degree orbit
                for (int i = 0; i <= 8; i++)
                {
                    var angle = i * MathF.PI / 4;
                    var radius = 150f;
                    AddKeyframe(new CameraKeyframe(
                        duration * i / 8,
                        MathF.Cos(angle) * radius,
                        MathF.Sin(angle) * radius,
                        1.5f,
                        EaseType.Linear));
                }
                break;

            case CinematicMode.DramaticReveal:
                AddKeyframe(new CameraKeyframe(0, 0, 0, 4.0f, EaseType.EaseOut));
                AddKeyframe(new CameraKeyframe(duration * 0.7, 0, 0, 4.0f, EaseType.Linear));
                AddKeyframe(new CameraKeyframe(duration, 0, 0, 1.0f, EaseType.EaseInOut));
                break;
        }

        Play();
    }

    /// <summary>
    /// Add a camera keyframe.
    /// </summary>
    public void AddKeyframe(CameraKeyframe keyframe)
    {
        _keyframes.Add(keyframe);
        _keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>
    /// Clear all keyframes.
    /// </summary>
    public void ClearKeyframes()
    {
        _keyframes.Clear();
    }

    /// <summary>
    /// Play the cinematic animation.
    /// </summary>
    public void Play()
    {
        if (_keyframes.Count < 2) return;

        _isPlaying = true;
        _animationTime = 0;
        _cts = new CancellationTokenSource();

        _ = RunAnimationAsync(_cts.Token);
    }

    /// <summary>
    /// Stop the animation.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _isPlaying = false;
        _currentMode = CinematicMode.None;
        ClearKeyframes();
    }

    private async Task RunAnimationAsync(CancellationToken ct)
    {
        var startTime = DateTime.Now;
        var totalDuration = _keyframes.Count > 0 ? _keyframes[^1].Time : 0;

        try
        {
            while (!ct.IsCancellationRequested && _animationTime < totalDuration)
            {
                _animationTime = (DateTime.Now - startTime).TotalSeconds;

                var state = EvaluateAt(_animationTime);
                CameraUpdated?.Invoke(state);

                await Task.Delay(16, ct); // ~60fps
            }

            AnimationCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isPlaying = false;
        }
    }

    private CameraState EvaluateAt(double time)
    {
        if (_keyframes.Count == 0)
            return new CameraState(0, 0, 1);

        if (_keyframes.Count == 1)
            return new CameraState(_keyframes[0].PanX, _keyframes[0].PanY, _keyframes[0].Zoom);

        // Find surrounding keyframes
        CameraKeyframe? prev = null;
        CameraKeyframe? next = null;

        for (int i = 0; i < _keyframes.Count - 1; i++)
        {
            if (time >= _keyframes[i].Time && time <= _keyframes[i + 1].Time)
            {
                prev = _keyframes[i];
                next = _keyframes[i + 1];
                break;
            }
        }

        if (prev == null) return new CameraState(_keyframes[0].PanX, _keyframes[0].PanY, _keyframes[0].Zoom);
        if (next == null) return new CameraState(_keyframes[^1].PanX, _keyframes[^1].PanY, _keyframes[^1].Zoom);

        // Interpolate
        var t = (time - prev.Time) / (next.Time - prev.Time);
        var easedT = ApplyEasing(t, next.Easing);

        return new CameraState(
            Lerp(prev.PanX, next.PanX, easedT),
            Lerp(prev.PanY, next.PanY, easedT),
            Lerp(prev.Zoom, next.Zoom, easedT)
        );
    }

    private static float Lerp(float a, float b, double t) => a + (b - a) * (float)t;

    private static double ApplyEasing(double t, EaseType easing) => easing switch
    {
        EaseType.Linear => t,
        EaseType.EaseIn => t * t,
        EaseType.EaseOut => 1 - (1 - t) * (1 - t),
        EaseType.EaseInOut => t < 0.5 ? 2 * t * t : 1 - Math.Pow(-2 * t + 2, 2) / 2,
        _ => t
    };
}

public enum CinematicMode
{
    None,
    SlowZoomIn,
    SlowZoomOut,
    PanRight,
    PanLeft,
    OrbitSpin,
    DramaticReveal
}

public enum EaseType
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut
}

public record CameraKeyframe(double Time, float PanX, float PanY, float Zoom, EaseType Easing = EaseType.Linear);
public record CameraState(float PanX, float PanY, float Zoom);
