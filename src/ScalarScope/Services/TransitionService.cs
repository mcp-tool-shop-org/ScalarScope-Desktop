namespace ScalarScope.Services;

/// <summary>
/// Phase 5.2: Manages smooth transitions between states.
/// No teleports - navigation should feel continuous.
/// </summary>
public static class TransitionService
{
    /// <summary>
    /// Current transition state.
    /// </summary>
    public static TransitionState CurrentTransition { get; private set; } = TransitionState.None;

    /// <summary>
    /// Transition progress (0-1).
    /// </summary>
    public static double TransitionProgress { get; private set; }

    /// <summary>
    /// Event fired during transition animation.
    /// </summary>
    public static event Action<double>? OnTransitionProgress;

    /// <summary>
    /// Event fired when transition completes.
    /// </summary>
    public static event Action<TransitionState>? OnTransitionComplete;

    // Camera/scale state preserved across transitions
    private static float _preservedZoom = 1f;
    private static float _preservedPanX;
    private static float _preservedPanY;

    /// <summary>
    /// Begin a crossfade transition from demo to real data.
    /// </summary>
    public static async Task StartDemoToRealTransition(int durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationDeliberate;
        
        CurrentTransition = TransitionState.DemoToReal;
        TransitionProgress = 0;

        var startTime = DateTime.Now;
        
        while (TransitionProgress < 1)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            TransitionProgress = Math.Clamp(elapsed / durationMs, 0, 1);
            
            // Apply ease-out for smooth deceleration
            var easedProgress = 1 - Math.Pow(1 - TransitionProgress, 3);
            
            OnTransitionProgress?.Invoke(easedProgress);
            
            if (TransitionProgress >= 1) break;
            await Task.Delay(16);
        }

        TransitionProgress = 1;
        var completedState = CurrentTransition;
        CurrentTransition = TransitionState.None;
        OnTransitionComplete?.Invoke(completedState);
    }

    /// <summary>
    /// Begin a crossfade transition from single to compare mode.
    /// </summary>
    public static async Task StartSingleToCompareTransition(int durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationStandard;
        
        CurrentTransition = TransitionState.SingleToCompare;
        TransitionProgress = 0;

        var startTime = DateTime.Now;
        
        while (TransitionProgress < 1)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            TransitionProgress = Math.Clamp(elapsed / durationMs, 0, 1);
            
            var easedProgress = 1 - Math.Pow(1 - TransitionProgress, 3);
            OnTransitionProgress?.Invoke(easedProgress);
            
            if (TransitionProgress >= 1) break;
            await Task.Delay(16);
        }

        TransitionProgress = 1;
        var completedState = CurrentTransition;
        CurrentTransition = TransitionState.None;
        OnTransitionComplete?.Invoke(completedState);
    }

    /// <summary>
    /// "Show me" navigation with scroll animation and destination highlight.
    /// </summary>
    public static async Task NavigateToAnchor(
        double targetTime,
        string? highlightElementId,
        Action<double> onSeek,
        Action<string>? onHighlight,
        int durationMs = -1)
    {
        if (durationMs < 0) durationMs = MotionTokens.DurationDeliberate;
        
        CurrentTransition = TransitionState.ShowMeNavigation;
        TransitionProgress = 0;

        var startTime = DateTime.Now;
        
        while (TransitionProgress < 1)
        {
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            TransitionProgress = Math.Clamp(elapsed / durationMs, 0, 1);
            
            // Use ease-out-cubic
            var easedProgress = 1 - Math.Pow(1 - TransitionProgress, 3);
            
            onSeek(easedProgress * targetTime);
            OnTransitionProgress?.Invoke(easedProgress);
            
            if (TransitionProgress >= 1) break;
            await Task.Delay(16);
        }

        // Highlight destination
        if (!string.IsNullOrEmpty(highlightElementId))
        {
            onHighlight?.Invoke(highlightElementId);
            
            // Brief highlight pulse
            await Task.Delay(MotionTokens.DurationStandard);
        }

        TransitionProgress = 1;
        CurrentTransition = TransitionState.None;
        OnTransitionComplete?.Invoke(TransitionState.ShowMeNavigation);
    }

    /// <summary>
    /// Preserve camera/scale state before transition.
    /// </summary>
    public static void PreserveCameraState(float zoom, float panX, float panY)
    {
        _preservedZoom = zoom;
        _preservedPanX = panX;
        _preservedPanY = panY;
    }

    /// <summary>
    /// Get preserved camera state for restoration.
    /// </summary>
    public static (float zoom, float panX, float panY) GetPreservedCameraState()
    {
        return (_preservedZoom, _preservedPanX, _preservedPanY);
    }

    /// <summary>
    /// Reset preserved state.
    /// </summary>
    public static void ClearPreservedState()
    {
        _preservedZoom = 1f;
        _preservedPanX = 0;
        _preservedPanY = 0;
    }

    /// <summary>
    /// Check if a transition is currently in progress.
    /// </summary>
    public static bool IsTransitioning => CurrentTransition != TransitionState.None;
}

/// <summary>
/// Types of state transitions.
/// </summary>
public enum TransitionState
{
    None,
    DemoToReal,
    RealToDemo,
    SingleToCompare,
    CompareToSingle,
    ShowMeNavigation,
    TabNavigation
}
