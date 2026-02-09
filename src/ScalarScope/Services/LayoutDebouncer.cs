namespace ScalarScope.Services;

/// <summary>
/// Phase 5.2: Debounce layout recalculations to prevent flicker.
/// Delays re-layout when trays/panels open/close in quick succession.
/// </summary>
public static class LayoutDebouncer
{
    private static CancellationTokenSource? _currentDebounce;
    private static readonly object _lock = new();
    
    /// <summary>
    /// Default debounce delay in milliseconds.
    /// </summary>
    public const int DefaultDelayMs = 80;
    
    /// <summary>
    /// Request a layout update with debouncing.
    /// Cancels previous pending updates and schedules a new one.
    /// </summary>
    public static async Task RequestLayoutUpdate(Action layoutAction, int delayMs = DefaultDelayMs)
    {
        CancellationToken token;
        
        lock (_lock)
        {
            // Cancel any pending layout request
            _currentDebounce?.Cancel();
            _currentDebounce = new CancellationTokenSource();
            token = _currentDebounce.Token;
        }
        
        try
        {
            await Task.Delay(delayMs, token);
            
            if (!token.IsCancellationRequested)
            {
                layoutAction();
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when a new request comes in
        }
    }
    
    /// <summary>
    /// Request layout on main thread with debouncing.
    /// </summary>
    public static Task RequestLayoutUpdateOnMainThread(Action layoutAction, int delayMs = DefaultDelayMs)
    {
        return RequestLayoutUpdate(() =>
        {
            MainThread.BeginInvokeOnMainThread(layoutAction);
        }, delayMs);
    }
    
    /// <summary>
    /// Clear any pending layout requests.
    /// </summary>
    public static void CancelPending()
    {
        lock (_lock)
        {
            _currentDebounce?.Cancel();
            _currentDebounce = null;
        }
    }
}

/// <summary>
/// Phase 5.2: Smooth axis range interpolation to prevent jarring rescales.
/// </summary>
public class AxisRangeInterpolator
{
    private double _currentMinX, _currentMaxX;
    private double _currentMinY, _currentMaxY;
    private double _targetMinX, _targetMaxX;
    private double _targetMinY, _targetMaxY;
    
    private readonly double _smoothing;
    
    /// <summary>
    /// Create interpolator with smoothing factor (0-1, higher = faster).
    /// </summary>
    public AxisRangeInterpolator(double smoothing = 0.15)
    {
        _smoothing = Math.Clamp(smoothing, 0.01, 1.0);
    }
    
    /// <summary>
    /// Set new target bounds. Will interpolate towards this over multiple frames.
    /// </summary>
    public void SetTargetBounds(double minX, double maxX, double minY, double maxY)
    {
        _targetMinX = minX;
        _targetMaxX = maxX;
        _targetMinY = minY;
        _targetMaxY = maxY;
        
        // If current is uninitialized, snap immediately
        if (_currentMinX == 0 && _currentMaxX == 0 && _currentMinY == 0 && _currentMaxY == 0)
        {
            _currentMinX = minX;
            _currentMaxX = maxX;
            _currentMinY = minY;
            _currentMaxY = maxY;
        }
    }
    
    /// <summary>
    /// Update bounds with interpolation. Call each frame.
    /// Returns true if still animating.
    /// </summary>
    public bool Update()
    {
        var prevMinX = _currentMinX;
        var prevMaxX = _currentMaxX;
        var prevMinY = _currentMinY;
        var prevMaxY = _currentMaxY;
        
        _currentMinX = Lerp(_currentMinX, _targetMinX, _smoothing);
        _currentMaxX = Lerp(_currentMaxX, _targetMaxX, _smoothing);
        _currentMinY = Lerp(_currentMinY, _targetMinY, _smoothing);
        _currentMaxY = Lerp(_currentMaxY, _targetMaxY, _smoothing);
        
        // Check if we've essentially reached target (within epsilon)
        const double epsilon = 0.001;
        var reachedTarget = 
            Math.Abs(_currentMinX - _targetMinX) < epsilon &&
            Math.Abs(_currentMaxX - _targetMaxX) < epsilon &&
            Math.Abs(_currentMinY - _targetMinY) < epsilon &&
            Math.Abs(_currentMaxY - _targetMaxY) < epsilon;
            
        if (reachedTarget)
        {
            // Snap to exact target
            _currentMinX = _targetMinX;
            _currentMaxX = _targetMaxX;
            _currentMinY = _targetMinY;
            _currentMaxY = _targetMaxY;
        }
        
        return !reachedTarget;
    }
    
    /// <summary>
    /// Get current interpolated bounds.
    /// </summary>
    public (double minX, double maxX, double minY, double maxY) GetCurrentBounds()
    {
        return (_currentMinX, _currentMaxX, _currentMinY, _currentMaxY);
    }
    
    /// <summary>
    /// Force snap to target bounds (no animation).
    /// </summary>
    public void SnapToTarget()
    {
        _currentMinX = _targetMinX;
        _currentMaxX = _targetMaxX;
        _currentMinY = _targetMinY;
        _currentMaxY = _targetMaxY;
    }
    
    private static double Lerp(double current, double target, double t)
    {
        return current + (target - current) * t;
    }
}
