namespace ScalarScope.Services;

/// <summary>
/// Phase 5.1: Centralized motion tokens and animation configuration.
/// Every animation has a defined semantic purpose - no decorative motion.
/// </summary>
public static class MotionTokens
{
    // ═══════════════════════════════════════════════════════════════════════
    // DURATION TOKENS (in milliseconds)
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>Instant feedback (hover, click response).</summary>
    public const int DurationInstant = 100;
    
    /// <summary>Quick transitions (tooltip show/hide, panel expand).</summary>
    public const int DurationQuick = 150;
    
    /// <summary>Standard transitions (page navigation, modal open).</summary>
    public const int DurationStandard = 250;
    
    /// <summary>Deliberate transitions (complex state changes, "show me" navigation).</summary>
    public const int DurationDeliberate = 400;
    
    /// <summary>Slow transitions (demo mode, idle calming).</summary>
    public const int DurationSlow = 600;
    
    /// <summary>Extended animations (loading sequences, intro sequences).</summary>
    public const int DurationExtended = 1000;

    // ═══════════════════════════════════════════════════════════════════════
    // EASING TOKENS
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>Standard easing for most animations.</summary>
    public static readonly Easing EaseStandard = Easing.CubicOut;
    
    /// <summary>Easing for entering animations (fade in, expand).</summary>
    public static readonly Easing EaseEnter = Easing.CubicOut;
    
    /// <summary>Easing for exiting animations (fade out, collapse).</summary>
    public static readonly Easing EaseExit = Easing.CubicIn;
    
    /// <summary>Easing for emphasis (bounce, overshoot).</summary>
    public static readonly Easing EaseEmphasis = Easing.SpringOut;
    
    /// <summary>Linear interpolation for data-driven animations.</summary>
    public static readonly Easing EaseLinear = Easing.Linear;

    // ═══════════════════════════════════════════════════════════════════════
    // MOTION SEMANTICS (what animations mean)
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Motion purpose categories. Every animation must map to one of these.
    /// </summary>
    public enum MotionPurpose
    {
        /// <summary>Indicates state change (loading → loaded, collapsed → expanded).</summary>
        StateChange,
        
        /// <summary>Communicates data meaning (confidence level, delta magnitude).</summary>
        DataMeaning,
        
        /// <summary>Provides navigation feedback ("show me" scroll, page transition).</summary>
        Navigation,
        
        /// <summary>Confirms user action (click feedback, selection).</summary>
        Feedback,
        
        /// <summary>Draws attention to important change (new delta, alert).</summary>
        Attention,
        
        /// <summary>Idle/ambient state (demo visualizations, waiting state).</summary>
        Ambient
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MOTION INVENTORY - Every animation in the app
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Animation registry mapping animation names to their purpose and duration.
    /// Used for auditing and reduced-motion substitutions.
    /// </summary>
    public static readonly Dictionary<string, MotionEntry> Inventory = new()
    {
        // === UI State Changes ===
        ["tooltip.show"] = new(MotionPurpose.StateChange, DurationQuick, EaseEnter,
            "Tooltip fade in", ReducedMotionBehavior.Instant),
        ["tooltip.hide"] = new(MotionPurpose.StateChange, DurationQuick, EaseExit,
            "Tooltip fade out", ReducedMotionBehavior.Instant),
        ["panel.expand"] = new(MotionPurpose.StateChange, DurationStandard, EaseEnter,
            "Panel height expansion", ReducedMotionBehavior.Instant),
        ["panel.collapse"] = new(MotionPurpose.StateChange, DurationStandard, EaseExit,
            "Panel height collapse", ReducedMotionBehavior.Instant),
        ["section.expand"] = new(MotionPurpose.StateChange, DurationQuick, EaseEnter,
            "Advanced section expand", ReducedMotionBehavior.Instant),
        ["tray.open"] = new(MotionPurpose.StateChange, DurationStandard, EaseEnter,
            "Insights tray slide in", ReducedMotionBehavior.Instant),
        ["tray.close"] = new(MotionPurpose.StateChange, DurationStandard, EaseExit,
            "Insights tray slide out", ReducedMotionBehavior.Instant),
        
        // === Data Visualization ===
        ["trajectory.draw"] = new(MotionPurpose.DataMeaning, DurationSlow, EaseLinear,
            "Training path progression", ReducedMotionBehavior.StaticEndState),
        ["trajectory.glow"] = new(MotionPurpose.DataMeaning, DurationInstant, EaseLinear,
            "Current position highlight", ReducedMotionBehavior.SolidOutline),
        ["eigenvalue.pulse"] = new(MotionPurpose.DataMeaning, DurationStandard, EaseStandard,
            "Value change emphasis", ReducedMotionBehavior.SolidOutline),
        ["confidence.fill"] = new(MotionPurpose.DataMeaning, DurationQuick, EaseEnter,
            "Confidence bar animation", ReducedMotionBehavior.StaticEndState),
        ["delta.highlight"] = new(MotionPurpose.DataMeaning, DurationStandard, EaseEmphasis,
            "Delta tile selection", ReducedMotionBehavior.BorderOnly),
        
        // === Navigation ===
        ["showme.scroll"] = new(MotionPurpose.Navigation, DurationDeliberate, EaseStandard,
            "Scroll to anchor", ReducedMotionBehavior.InstantJump),
        ["showme.highlight"] = new(MotionPurpose.Navigation, DurationStandard, EaseEmphasis,
            "Destination highlight pulse", ReducedMotionBehavior.BorderOnly),
        ["page.transition"] = new(MotionPurpose.Navigation, DurationStandard, EaseStandard,
            "Tab navigation", ReducedMotionBehavior.Instant),
        ["breadcrumb.show"] = new(MotionPurpose.Navigation, DurationQuick, EaseEnter,
            "Navigation breadcrumb", ReducedMotionBehavior.Instant),
        
        // === User Feedback ===
        ["button.press"] = new(MotionPurpose.Feedback, DurationInstant, EaseStandard,
            "Button click feedback", ReducedMotionBehavior.ColorOnly),
        ["hover.highlight"] = new(MotionPurpose.Feedback, DurationInstant, EaseStandard,
            "Hover state transition", ReducedMotionBehavior.Instant),
        ["copy.confirm"] = new(MotionPurpose.Feedback, DurationQuick, EaseEnter,
            "Copy-to-clipboard confirmation", ReducedMotionBehavior.TextOnly),
        ["selection.ring"] = new(MotionPurpose.Feedback, DurationInstant, EaseStandard,
            "Selection indicator", ReducedMotionBehavior.SolidOutline),
        
        // === Attention ===
        ["delta.new"] = new(MotionPurpose.Attention, DurationStandard, EaseEmphasis,
            "New delta detected", ReducedMotionBehavior.BorderPulse),
        ["insight.new"] = new(MotionPurpose.Attention, DurationStandard, EaseEmphasis,
            "New insight in tray", ReducedMotionBehavior.BadgeOnly),
        ["alert.fire"] = new(MotionPurpose.Attention, DurationDeliberate, EaseEmphasis,
            "Alert threshold crossed", ReducedMotionBehavior.IconOnly),
        ["hint.show"] = new(MotionPurpose.Attention, DurationStandard, EaseEnter,
            "First-occurrence hint", ReducedMotionBehavior.Instant),
        
        // === Ambient (Demo/Idle) ===
        ["demo.trajectory"] = new(MotionPurpose.Ambient, DurationExtended, EaseLinear,
            "Demo mode path animation", ReducedMotionBehavior.StaticEndState),
        ["demo.eigenvalue"] = new(MotionPurpose.Ambient, DurationExtended, EaseLinear,
            "Demo eigenvalue evolution", ReducedMotionBehavior.StaticEndState),
        ["idle.calm"] = new(MotionPurpose.Ambient, DurationExtended * 2, EaseLinear,
            "Idle state subtle motion", ReducedMotionBehavior.Static),
        ["loading.skeleton"] = new(MotionPurpose.Ambient, DurationSlow, EaseLinear,
            "Loading state shimmer", ReducedMotionBehavior.StaticSkeleton),
        ["particle.emit"] = new(MotionPurpose.Ambient, DurationSlow, EaseLinear,
            "Decorative particle effects", ReducedMotionBehavior.Disabled),
        ["flowfield.drift"] = new(MotionPurpose.Ambient, DurationExtended, EaseLinear,
            "Background flow field", ReducedMotionBehavior.Disabled),
    };

    // ═══════════════════════════════════════════════════════════════════════
    // REDUCED MOTION SUBSTITUTIONS
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// How to handle animation when reduced motion is enabled.
    /// Ensures meaning is preserved without motion.
    /// </summary>
    public enum ReducedMotionBehavior
    {
        /// <summary>Skip animation entirely, show end state immediately.</summary>
        Instant,
        
        /// <summary>Replace animation with instant jump to destination.</summary>
        InstantJump,
        
        /// <summary>Show end state without animation.</summary>
        StaticEndState,
        
        /// <summary>Replace pulse/glow with solid outline.</summary>
        SolidOutline,
        
        /// <summary>Replace animation with border emphasis only.</summary>
        BorderOnly,
        
        /// <summary>Replace animated highlight with single border pulse.</summary>
        BorderPulse,
        
        /// <summary>Show static skeleton structure (no shimmer).</summary>
        StaticSkeleton,
        
        /// <summary>Completely static, no replacement needed.</summary>
        Static,
        
        /// <summary>Animation is purely decorative, disable entirely.</summary>
        Disabled,
        
        /// <summary>Replace with color change only.</summary>
        ColorOnly,
        
        /// <summary>Replace with text change only (e.g., "✓ Copied").</summary>
        TextOnly,
        
        /// <summary>Replace with icon change only.</summary>
        IconOnly,
        
        /// <summary>Replace with badge indicator only.</summary>
        BadgeOnly,
    }

    /// <summary>
    /// Motion entry describing a single animation's purpose and behavior.
    /// </summary>
    public record MotionEntry(
        MotionPurpose Purpose,
        int DurationMs,
        Easing Easing,
        string Description,
        ReducedMotionBehavior ReducedBehavior
    );

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get the effective duration for an animation, respecting reduced motion.
    /// </summary>
    public static int GetDuration(string animationKey)
    {
        if (!Inventory.TryGetValue(animationKey, out var entry))
            return DurationStandard;
            
        if (AccessibilityService.Instance?.ReducedMotion == true)
        {
            return entry.ReducedBehavior switch
            {
                ReducedMotionBehavior.Instant or
                ReducedMotionBehavior.InstantJump or
                ReducedMotionBehavior.StaticEndState or
                ReducedMotionBehavior.Static or
                ReducedMotionBehavior.Disabled => 0,
                
                ReducedMotionBehavior.BorderPulse => DurationInstant,
                
                _ => DurationInstant
            };
        }
        
        return entry.DurationMs;
    }

    /// <summary>
    /// Get the easing for an animation.
    /// </summary>
    public static Easing GetEasing(string animationKey)
    {
        if (!Inventory.TryGetValue(animationKey, out var entry))
            return EaseStandard;
        return entry.Easing;
    }

    /// <summary>
    /// Check if an animation should run at all (may be disabled for reduced motion).
    /// </summary>
    public static bool ShouldAnimate(string animationKey)
    {
        if (!Inventory.TryGetValue(animationKey, out var entry))
            return true;
            
        if (AccessibilityService.Instance?.ReducedMotion == true)
        {
            return entry.ReducedBehavior != ReducedMotionBehavior.Disabled &&
                   entry.ReducedBehavior != ReducedMotionBehavior.Static;
        }
        
        return true;
    }

    /// <summary>
    /// Check if glow effects should be replaced with solid outlines (reduced motion).
    /// </summary>
    public static bool ShouldUseOutlineInsteadOfGlow()
    {
        if (AccessibilityService.Instance?.ReducedMotion != true)
            return false;
            
        // In reduced motion, replace all glow/pulse effects with solid outlines
        return true;
    }

    /// <summary>
    /// Check if pulse/blink effects should be replaced with static borders.
    /// </summary>
    public static bool ShouldUseStaticBorderInsteadOfPulse()
    {
        if (AccessibilityService.Instance?.ReducedMotion != true)
            return false;
            
        return true;
    }

    /// <summary>
    /// Get the reduced motion substitution for an animation.
    /// </summary>
    public static ReducedMotionBehavior GetReducedBehavior(string animationKey)
    {
        if (!Inventory.TryGetValue(animationKey, out var entry))
            return ReducedMotionBehavior.Instant;
        return entry.ReducedBehavior;
    }

    /// <summary>
    /// Validate that all animations have a defined semantic purpose.
    /// Used for auditing.
    /// </summary>
    public static List<string> GetDecorativeAnimations()
    {
        return Inventory
            .Where(kvp => kvp.Value.Purpose == MotionPurpose.Ambient &&
                          kvp.Value.ReducedBehavior == ReducedMotionBehavior.Disabled)
            .Select(kvp => kvp.Key)
            .ToList();
    }
    
    /// <summary>
    /// Get idle/demo animation slowdown factor.
    /// In idle state, ambient animations run slower to avoid competing with data.
    /// </summary>
    public static float GetIdleSlowdownFactor(bool isIdle)
    {
        return isIdle ? 0.5f : 1.0f;
    }
}
