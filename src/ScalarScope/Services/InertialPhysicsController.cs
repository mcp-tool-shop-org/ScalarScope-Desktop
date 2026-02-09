using System.Numerics;

namespace ScalarScope.Services;

/// <summary>
/// Physics-based controller for smooth pan/zoom with momentum.
/// Phase 4.1 - Fluid Navigation
/// </summary>
public class InertialPhysicsController
{
    #region Configuration

    /// <summary>Friction coefficient for pan momentum decay (0-1, higher = more friction)</summary>
    public float PanFriction { get; set; } = 0.92f;

    /// <summary>Friction coefficient for zoom momentum decay</summary>
    public float ZoomFriction { get; set; } = 0.85f;

    /// <summary>Minimum velocity before considering stopped</summary>
    public float VelocityThreshold { get; set; } = 0.5f;

    /// <summary>Maximum pan velocity (pixels per frame)</summary>
    public float MaxPanVelocity { get; set; } = 100f;

    /// <summary>Zoom bounds</summary>
    public float MinZoom { get; set; } = 0.25f;
    public float MaxZoom { get; set; } = 4f;

    /// <summary>Smoothing factor for input (0-1, lower = smoother)</summary>
    public float InputSmoothing { get; set; } = 0.3f;

    /// <summary>Spring stiffness for snap-back when out of bounds</summary>
    public float SpringStiffness { get; set; } = 0.15f;

    /// <summary>Spring damping for snap-back</summary>
    public float SpringDamping { get; set; } = 0.8f;

    #endregion

    #region State

    // Pan state
    private Vector2 _panOffset = Vector2.Zero;
    private Vector2 _panVelocity = Vector2.Zero;
    private Vector2 _lastInputPosition = Vector2.Zero;
    private DateTime _lastInputTime = DateTime.MinValue;
    private bool _isDragging;

    // Zoom state
    private float _zoom = 1f;
    private float _zoomVelocity = 0f;
    private float _targetZoom = 1f;

    // Bounds for content area (optional)
    private Vector2 _contentMin = new(-1000, -1000);
    private Vector2 _contentMax = new(1000, 1000);
    private Vector2 _viewportSize = new(800, 600);

    // Animation state
    private bool _isAnimating;
    private CancellationTokenSource? _animationCts;

    #endregion

    #region Properties

    public Vector2 PanOffset => _panOffset;
    public float Zoom => _zoom;
    public bool IsAnimating => _isAnimating;
    public bool IsDragging => _isDragging;

    #endregion

    #region Events

    public event Action? StateChanged;

    #endregion

    #region Input Methods

    /// <summary>
    /// Called when a drag/pan gesture begins.
    /// </summary>
    public void BeginDrag(float x, float y)
    {
        _isDragging = true;
        _lastInputPosition = new Vector2(x, y);
        _lastInputTime = DateTime.Now;
        _panVelocity = Vector2.Zero;
        StopAnimation();
    }

    /// <summary>
    /// Called during drag/pan gesture.
    /// </summary>
    public void UpdateDrag(float x, float y)
    {
        if (!_isDragging) return;

        var currentPosition = new Vector2(x, y);
        var now = DateTime.Now;
        var dt = (float)(now - _lastInputTime).TotalSeconds;

        if (dt > 0.001f)
        {
            // Calculate velocity from delta
            var delta = currentPosition - _lastInputPosition;
            var instantVelocity = delta / dt;

            // Smooth velocity with exponential moving average
            _panVelocity = Vector2.Lerp(_panVelocity, instantVelocity, InputSmoothing);

            // Apply delta to pan offset
            _panOffset += delta;

            _lastInputPosition = currentPosition;
            _lastInputTime = now;

            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Called when drag/pan gesture ends. Starts momentum animation.
    /// </summary>
    public void EndDrag()
    {
        if (!_isDragging) return;

        _isDragging = false;

        // Clamp velocity
        var speed = _panVelocity.Length();
        if (speed > MaxPanVelocity)
        {
            _panVelocity = Vector2.Normalize(_panVelocity) * MaxPanVelocity;
        }

        // Start momentum animation if velocity is significant
        if (speed > VelocityThreshold)
        {
            StartMomentumAnimation();
        }
    }

    /// <summary>
    /// Apply zoom centered at a screen position.
    /// </summary>
    public void ApplyZoom(float delta, float centerX, float centerY)
    {
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * (1f + delta * 0.1f), MinZoom, MaxZoom);

        // Adjust pan to keep the zoom center fixed
        if (Math.Abs(_zoom - oldZoom) > 0.001f)
        {
            var center = new Vector2(centerX, centerY);
            var scale = _zoom / oldZoom;
            _panOffset = center + (_panOffset - center) * scale;
        }

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Smooth zoom to target level with animation.
    /// </summary>
    public void ZoomTo(float targetZoom, float centerX, float centerY, float durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationStandard;
        _targetZoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);
        _ = AnimateZoomAsync(centerX, centerY, durationMs);
    }

    /// <summary>
    /// Pinch zoom gesture.
    /// </summary>
    public void Pinch(float scaleFactor, float centerX, float centerY)
    {
        var oldZoom = _zoom;
        _zoom = Math.Clamp(_zoom * scaleFactor, MinZoom, MaxZoom);

        // Adjust pan to keep pinch center fixed
        if (Math.Abs(_zoom - oldZoom) > 0.001f)
        {
            var center = new Vector2(centerX, centerY);
            var scale = _zoom / oldZoom;
            _panOffset = center + (_panOffset - center) * scale;
        }

        StateChanged?.Invoke();
    }

    /// <summary>
    /// Reset pan and zoom to defaults with animation.
    /// </summary>
    public async Task ResetAsync(float durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationStandard;
        StopAnimation();
        _animationCts = new CancellationTokenSource();

        var startPan = _panOffset;
        var startZoom = _zoom;
        var startTime = DateTime.Now;

        _isAnimating = true;

        try
        {
            while (!_animationCts.Token.IsCancellationRequested)
            {
                var elapsed = (float)(DateTime.Now - startTime).TotalMilliseconds;
                var t = Math.Clamp(elapsed / durationMs, 0, 1);

                // Ease-out cubic
                var ease = 1f - (1f - t) * (1f - t) * (1f - t);

                _panOffset = Vector2.Lerp(startPan, Vector2.Zero, ease);
                _zoom = startZoom + (1f - startZoom) * ease;

                StateChanged?.Invoke();

                if (t >= 1f) break;
                await Task.Delay(16, _animationCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isAnimating = false;
            _panOffset = Vector2.Zero;
            _zoom = 1f;
            _panVelocity = Vector2.Zero;
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Animate pan to a specific position.
    /// </summary>
    public async Task PanToAsync(float x, float y, float durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationDeliberate;
        StopAnimation();
        _animationCts = new CancellationTokenSource();

        var startPan = _panOffset;
        var targetPan = new Vector2(x, y);
        var startTime = DateTime.Now;

        _isAnimating = true;

        try
        {
            while (!_animationCts.Token.IsCancellationRequested)
            {
                var elapsed = (float)(DateTime.Now - startTime).TotalMilliseconds;
                var t = Math.Clamp(elapsed / durationMs, 0, 1);

                // Ease-out cubic
                var ease = 1f - (1f - t) * (1f - t) * (1f - t);

                _panOffset = Vector2.Lerp(startPan, targetPan, ease);

                StateChanged?.Invoke();

                if (t >= 1f) break;
                await Task.Delay(16, _animationCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isAnimating = false;
        }
    }

    #endregion

    #region Bounds

    /// <summary>
    /// Set the content bounds for snap-back behavior.
    /// </summary>
    public void SetContentBounds(float minX, float minY, float maxX, float maxY)
    {
        _contentMin = new Vector2(minX, minY);
        _contentMax = new Vector2(maxX, maxY);
    }

    /// <summary>
    /// Set the viewport size for bounds calculations.
    /// </summary>
    public void SetViewportSize(float width, float height)
    {
        _viewportSize = new Vector2(width, height);
    }

    #endregion

    #region Direct State Access

    /// <summary>
    /// Directly set pan offset (bypassing physics).
    /// </summary>
    public void SetPanOffset(float x, float y)
    {
        _panOffset = new Vector2(x, y);
        _panVelocity = Vector2.Zero;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Directly set zoom level (bypassing physics).
    /// </summary>
    public void SetZoom(float zoom)
    {
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        StateChanged?.Invoke();
    }

    #endregion

    #region Animation

    private void StartMomentumAnimation()
    {
        StopAnimation();
        _animationCts = new CancellationTokenSource();
        _ = RunMomentumAnimationAsync(_animationCts.Token);
    }

    private void StopAnimation()
    {
        _animationCts?.Cancel();
        _animationCts = null;
        _isAnimating = false;
    }

    private async Task RunMomentumAnimationAsync(CancellationToken ct)
    {
        _isAnimating = true;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Apply friction
                _panVelocity *= PanFriction;

                // Apply snap-back spring if out of bounds
                var springForce = Vector2.Zero;
                // (Optional: implement bounds checking and spring force)

                // Update position
                _panOffset += _panVelocity / 60f; // Assuming 60fps

                StateChanged?.Invoke();

                // Check if we should stop
                if (_panVelocity.Length() < VelocityThreshold)
                {
                    break;
                }

                await Task.Delay(16, ct); // ~60fps
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task AnimateZoomAsync(float centerX, float centerY, float durationMs)
    {
        var startZoom = _zoom;
        var startPan = _panOffset;
        var startTime = DateTime.Now;
        var center = new Vector2(centerX, centerY);

        while (true)
        {
            var elapsed = (float)(DateTime.Now - startTime).TotalMilliseconds;
            var t = Math.Clamp(elapsed / durationMs, 0, 1);

            // Ease-out
            var ease = 1f - (1f - t) * (1f - t);

            var newZoom = startZoom + (_targetZoom - startZoom) * ease;
            var scale = newZoom / _zoom;
            _zoom = newZoom;
            _panOffset = center + (_panOffset - center) * scale;

            StateChanged?.Invoke();

            if (t >= 1f) break;
            await Task.Delay(16);
        }
    }

    #endregion
}

/// <summary>
/// Bookmarks for saving interesting pan/zoom positions.
/// </summary>
public class ViewportBookmark
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public float PanX { get; init; }
    public float PanY { get; init; }
    public float Zoom { get; init; }
    public double PlaybackTime { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string? Description { get; set; }
}

/// <summary>
/// Service for managing viewport bookmarks.
/// </summary>
public class BookmarkService
{
    private readonly List<ViewportBookmark> _bookmarks = [];

    public IReadOnlyList<ViewportBookmark> Bookmarks => _bookmarks.AsReadOnly();

    public event Action? BookmarksChanged;

    /// <summary>
    /// Add a new bookmark at the current viewport state.
    /// </summary>
    public ViewportBookmark AddBookmark(string name, float panX, float panY, float zoom, double playbackTime, string? description = null)
    {
        var bookmark = new ViewportBookmark
        {
            Name = name,
            PanX = panX,
            PanY = panY,
            Zoom = zoom,
            PlaybackTime = playbackTime,
            Description = description
        };

        _bookmarks.Add(bookmark);
        BookmarksChanged?.Invoke();
        return bookmark;
    }

    /// <summary>
    /// Remove a bookmark by ID.
    /// </summary>
    public bool RemoveBookmark(string id)
    {
        var bookmark = _bookmarks.FirstOrDefault(b => b.Id == id);
        if (bookmark != null)
        {
            _bookmarks.Remove(bookmark);
            BookmarksChanged?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Rename a bookmark.
    /// </summary>
    public void RenameBookmark(string id, string newName)
    {
        var bookmark = _bookmarks.FirstOrDefault(b => b.Id == id);
        if (bookmark != null)
        {
            bookmark.Name = newName;
            BookmarksChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clear all bookmarks.
    /// </summary>
    public void ClearAll()
    {
        _bookmarks.Clear();
        BookmarksChanged?.Invoke();
    }

    /// <summary>
    /// Get bookmark by ID.
    /// </summary>
    public ViewportBookmark? GetBookmark(string id)
    {
        return _bookmarks.FirstOrDefault(b => b.Id == id);
    }
}
