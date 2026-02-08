using SkiaSharp;

namespace ScalarScope.Services;

/// <summary>
/// Accessibility service providing colorblind palettes, high contrast modes,
/// screen reader descriptions, and reduced motion support.
/// Phase 4.3 - Accessibility
/// </summary>
public class AccessibilityService : IDisposable
{
    private static AccessibilityService? _instance;
    public static AccessibilityService Instance => _instance ??= new AccessibilityService();

    private AccessibilitySettings _settings = new();

    public event Action? SettingsChanged;

    #region Settings

    public AccessibilitySettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            SettingsChanged?.Invoke();
        }
    }

    public ColorPalette CurrentPalette => _settings.ColorPaletteMode switch
    {
        ColorPaletteMode.Default => ColorPalettes.Default,
        ColorPaletteMode.Deuteranopia => ColorPalettes.Deuteranopia,
        ColorPaletteMode.Protanopia => ColorPalettes.Protanopia,
        ColorPaletteMode.Tritanopia => ColorPalettes.Tritanopia,
        ColorPaletteMode.HighContrast => ColorPalettes.HighContrast,
        ColorPaletteMode.Monochrome => ColorPalettes.Monochrome,
        _ => ColorPalettes.Default
    };

    public bool ReducedMotion => _settings.ReducedMotionEnabled;
    public bool ScreenReaderMode => _settings.ScreenReaderEnabled;

    #endregion

    #region Color Transformation

    /// <summary>
    /// Transform a color based on current accessibility settings.
    /// </summary>
    public SKColor TransformColor(SKColor original, ColorRole role)
    {
        if (_settings.ColorPaletteMode == ColorPaletteMode.Default && !_settings.HighContrastEnabled)
            return original;

        return role switch
        {
            ColorRole.Primary => CurrentPalette.Primary,
            ColorRole.Secondary => CurrentPalette.Secondary,
            ColorRole.Accent => CurrentPalette.Accent,
            ColorRole.Success => CurrentPalette.Success,
            ColorRole.Warning => CurrentPalette.Warning,
            ColorRole.Error => CurrentPalette.Error,
            ColorRole.Background => CurrentPalette.Background,
            ColorRole.Surface => CurrentPalette.Surface,
            ColorRole.Text => CurrentPalette.Text,
            ColorRole.TextSecondary => CurrentPalette.TextSecondary,
            _ => original
        };
    }

    /// <summary>
    /// Get a palette color directly by role.
    /// </summary>
    public SKColor GetColor(ColorRole role) => TransformColor(SKColors.White, role);

    /// <summary>
    /// Transform for colorblind simulation (for testing).
    /// </summary>
    public SKColor SimulateColorblindness(SKColor color, ColorPaletteMode mode)
    {
        // Using Brettel/Viénot/Mollon algorithm approximations
        return mode switch
        {
            ColorPaletteMode.Deuteranopia => ApplyDeuteranopiaMatrix(color),
            ColorPaletteMode.Protanopia => ApplyProtanopiaMatrix(color),
            ColorPaletteMode.Tritanopia => ApplyTritanopiaMatrix(color),
            _ => color
        };
    }

    private static SKColor ApplyDeuteranopiaMatrix(SKColor c)
    {
        // Deuteranopia (green-blind) simulation matrix
        var r = c.Red * 0.625f + c.Green * 0.375f;
        var g = c.Red * 0.7f + c.Green * 0.3f;
        var b = c.Blue * 1.0f;
        return new SKColor(ClampByte(r), ClampByte(g), ClampByte(b), c.Alpha);
    }

    private static SKColor ApplyProtanopiaMatrix(SKColor c)
    {
        // Protanopia (red-blind) simulation matrix
        var r = c.Red * 0.567f + c.Green * 0.433f;
        var g = c.Red * 0.558f + c.Green * 0.442f;
        var b = c.Blue * 1.0f;
        return new SKColor(ClampByte(r), ClampByte(g), ClampByte(b), c.Alpha);
    }

    private static SKColor ApplyTritanopiaMatrix(SKColor c)
    {
        // Tritanopia (blue-blind) simulation matrix
        var r = c.Red * 0.95f + c.Blue * 0.05f;
        var g = c.Green * 0.433f + c.Blue * 0.567f;
        var b = c.Green * 0.475f + c.Blue * 0.525f;
        return new SKColor(ClampByte(r), ClampByte(g), ClampByte(b), c.Alpha);
    }

    private static byte ClampByte(float value) => (byte)Math.Clamp(value, 0, 255);

    #endregion

    #region Screen Reader

    /// <summary>
    /// Generate a spoken description of a trajectory's current state.
    /// </summary>
    public string DescribeTrajectoryState(TrajectoryDescription desc)
    {
        var parts = new List<string>();

        // Overall shape
        parts.Add($"Trajectory showing {desc.PointCount} timesteps.");

        // Current position
        if (desc.CurrentPosition != null)
        {
            parts.Add($"Currently at timestep {desc.CurrentTimestep} of {desc.PointCount}.");
        }

        // Phase information
        if (!string.IsNullOrEmpty(desc.CurrentPhase))
        {
            parts.Add($"Current phase: {desc.CurrentPhase}.");
        }

        // Velocity/dynamics
        if (desc.CurrentVelocity > 0)
        {
            var velocityDesc = desc.CurrentVelocity switch
            {
                < 0.2f => "slow",
                < 0.5f => "moderate",
                < 0.8f => "fast",
                _ => "very fast"
            };
            parts.Add($"Movement is {velocityDesc}.");
        }

        // Warnings
        if (desc.HasWarning)
        {
            parts.Add($"Warning: {desc.WarningMessage}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Generate description of a notable event.
    /// </summary>
    public string DescribeEvent(TrajectoryEvent evt)
    {
        return evt.Type switch
        {
            EventType.PhaseTransition => $"Phase transition at timestep {evt.Timestep}: entering {evt.PhaseName}.",
            EventType.Bifurcation => $"Bifurcation detected at timestep {evt.Timestep}.",
            EventType.DimensionCollapse => $"Dimension collapse warning at timestep {evt.Timestep}. Effective dimension dropped to {evt.Value:F1}.",
            EventType.VelocitySpike => $"Velocity spike at timestep {evt.Timestep}. Training accelerated rapidly.",
            EventType.Convergence => $"Convergence detected at timestep {evt.Timestep}. Training has stabilized.",
            EventType.Divergence => $"Divergence warning at timestep {evt.Timestep}. Training may be unstable.",
            _ => $"Event at timestep {evt.Timestep}."
        };
    }

    #endregion

    #region Animation Settings

    /// <summary>
    /// Get animation duration based on reduced motion setting.
    /// </summary>
    public TimeSpan GetAnimationDuration(TimeSpan requested)
    {
        if (_settings.ReducedMotionEnabled)
            return TimeSpan.Zero; // Instant transitions
        
        return requested;
    }

    /// <summary>
    /// Check if an animation should be played.
    /// </summary>
    public bool ShouldAnimate() => !_settings.ReducedMotionEnabled;

    /// <summary>
    /// Get particle count scaled for performance/motion sensitivity.
    /// </summary>
    public int GetScaledParticleCount(int requested)
    {
        if (_settings.ReducedMotionEnabled)
            return 0;
        
        return requested;
    }

    #endregion

    public void Dispose()
    {
        _instance = null;
        GC.SuppressFinalize(this);
    }
}

#region Data Models

public record AccessibilitySettings
{
    public ColorPaletteMode ColorPaletteMode { get; init; } = ColorPaletteMode.Default;
    public bool HighContrastEnabled { get; init; }
    public bool ReducedMotionEnabled { get; init; }
    public bool ScreenReaderEnabled { get; init; }
    public float TextScale { get; init; } = 1.0f;
    public bool LargePointer { get; init; }
}

public enum ColorPaletteMode
{
    Default,
    Deuteranopia,   // Green-blind
    Protanopia,     // Red-blind
    Tritanopia,     // Blue-blind
    HighContrast,
    Monochrome
}

public enum ColorRole
{
    Primary,
    Secondary,
    Accent,
    Success,
    Warning,
    Error,
    Background,
    Surface,
    Text,
    TextSecondary
}

public class ColorPalette
{
    public required string Name { get; init; }
    public SKColor Primary { get; init; }
    public SKColor Secondary { get; init; }
    public SKColor Accent { get; init; }
    public SKColor Success { get; init; }
    public SKColor Warning { get; init; }
    public SKColor Error { get; init; }
    public SKColor Background { get; init; }
    public SKColor Surface { get; init; }
    public SKColor Text { get; init; }
    public SKColor TextSecondary { get; init; }
}

public static class ColorPalettes
{
    /// <summary>
    /// Default ScalarScope palette
    /// </summary>
    public static ColorPalette Default { get; } = new()
    {
        Name = "Default",
        Primary = SKColor.Parse("#00d9ff"),
        Secondary = SKColor.Parse("#4ecdc4"),
        Accent = SKColor.Parse("#ff6b9d"),
        Success = SKColor.Parse("#4ecdc4"),
        Warning = SKColor.Parse("#ff9f43"),
        Error = SKColor.Parse("#ff6b6b"),
        Background = SKColor.Parse("#0f0f1a"),
        Surface = SKColor.Parse("#1a1a2e"),
        Text = SKColors.White,
        TextSecondary = SKColor.Parse("#888888")
    };

    /// <summary>
    /// Deuteranopia-safe palette (green-blind)
    /// Uses blue/orange contrast instead of red/green
    /// </summary>
    public static ColorPalette Deuteranopia { get; } = new()
    {
        Name = "Deuteranopia Safe",
        Primary = SKColor.Parse("#0077BB"),    // Blue
        Secondary = SKColor.Parse("#33BBEE"),  // Cyan
        Accent = SKColor.Parse("#EE7733"),     // Orange
        Success = SKColor.Parse("#009988"),    // Teal
        Warning = SKColor.Parse("#EE7733"),    // Orange
        Error = SKColor.Parse("#CC3311"),      // Red-orange
        Background = SKColor.Parse("#0f0f1a"),
        Surface = SKColor.Parse("#1a1a2e"),
        Text = SKColors.White,
        TextSecondary = SKColor.Parse("#888888")
    };

    /// <summary>
    /// Protanopia-safe palette (red-blind)
    /// Uses blue/yellow contrast
    /// </summary>
    public static ColorPalette Protanopia { get; } = new()
    {
        Name = "Protanopia Safe",
        Primary = SKColor.Parse("#0077BB"),    // Blue
        Secondary = SKColor.Parse("#33BBEE"),  // Cyan
        Accent = SKColor.Parse("#DDCC77"),     // Yellow
        Success = SKColor.Parse("#009988"),    // Teal
        Warning = SKColor.Parse("#DDCC77"),    // Yellow
        Error = SKColor.Parse("#BB5566"),      // Muted magenta
        Background = SKColor.Parse("#0f0f1a"),
        Surface = SKColor.Parse("#1a1a2e"),
        Text = SKColors.White,
        TextSecondary = SKColor.Parse("#888888")
    };

    /// <summary>
    /// Tritanopia-safe palette (blue-blind)
    /// Uses red/green-shifted yellows
    /// </summary>
    public static ColorPalette Tritanopia { get; } = new()
    {
        Name = "Tritanopia Safe",
        Primary = SKColor.Parse("#EE3377"),    // Magenta
        Secondary = SKColor.Parse("#EE7733"),  // Orange
        Accent = SKColor.Parse("#009988"),     // Teal
        Success = SKColor.Parse("#009988"),    // Teal
        Warning = SKColor.Parse("#EE7733"),    // Orange
        Error = SKColor.Parse("#CC3311"),      // Red
        Background = SKColor.Parse("#0f0f1a"),
        Surface = SKColor.Parse("#1a1a2e"),
        Text = SKColors.White,
        TextSecondary = SKColor.Parse("#888888")
    };

    /// <summary>
    /// High contrast palette for low vision
    /// </summary>
    public static ColorPalette HighContrast { get; } = new()
    {
        Name = "High Contrast",
        Primary = SKColors.Yellow,
        Secondary = SKColors.Cyan,
        Accent = SKColors.Magenta,
        Success = SKColors.Lime,
        Warning = SKColors.Yellow,
        Error = SKColors.Red,
        Background = SKColors.Black,
        Surface = SKColor.Parse("#1a1a1a"),
        Text = SKColors.White,
        TextSecondary = SKColors.Yellow
    };

    /// <summary>
    /// Monochrome palette for complete color blindness
    /// </summary>
    public static ColorPalette Monochrome { get; } = new()
    {
        Name = "Monochrome",
        Primary = SKColors.White,
        Secondary = SKColor.Parse("#CCCCCC"),
        Accent = SKColors.White,
        Success = SKColor.Parse("#AAAAAA"),
        Warning = SKColor.Parse("#888888"),
        Error = SKColor.Parse("#666666"),
        Background = SKColors.Black,
        Surface = SKColor.Parse("#1a1a1a"),
        Text = SKColors.White,
        TextSecondary = SKColor.Parse("#888888")
    };

    /// <summary>
    /// Get all available palettes
    /// </summary>
    public static IReadOnlyList<ColorPalette> All { get; } =
    [
        Default,
        Deuteranopia,
        Protanopia,
        Tritanopia,
        HighContrast,
        Monochrome
    ];
}

public class TrajectoryDescription
{
    public int PointCount { get; set; }
    public int CurrentTimestep { get; set; }
    public (float X, float Y)? CurrentPosition { get; set; }
    public string? CurrentPhase { get; set; }
    public float CurrentVelocity { get; set; }
    public bool HasWarning { get; set; }
    public string? WarningMessage { get; set; }
}

public class TrajectoryEvent
{
    public EventType Type { get; set; }
    public int Timestep { get; set; }
    public string? PhaseName { get; set; }
    public float Value { get; set; }
}

public enum EventType
{
    PhaseTransition,
    Bifurcation,
    DimensionCollapse,
    VelocitySpike,
    Convergence,
    Divergence
}

#endregion
