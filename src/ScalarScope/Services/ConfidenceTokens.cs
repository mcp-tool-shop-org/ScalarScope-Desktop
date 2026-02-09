using SkiaSharp;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.3: Confidence ↔ Intensity semantic tokens.
/// Visual weight correlates with certainty; nothing is mute or screaming.
/// </summary>
public static class ConfidenceTokens
{
    // Confidence tier thresholds (based on p-value)
    public const double HighConfidenceThreshold = 0.01;   // p < 0.01 = high
    public const double MediumConfidenceThreshold = 0.05; // p < 0.05 = medium
    public const double LowConfidenceThreshold = 0.10;    // p < 0.10 = low
    // p >= 0.10 = negligible (barely visible)
    
    /// <summary>
    /// Confidence tiers for visual mapping.
    /// </summary>
    public enum ConfidenceTier
    {
        /// <summary>p >= 0.10 - Nearly invisible, very low certainty</summary>
        Negligible,
        /// <summary>p < 0.10 - Subtle emphasis, low certainty</summary>
        Low,
        /// <summary>p < 0.05 - Moderate emphasis, reasonable certainty</summary>
        Medium,
        /// <summary>p < 0.01 - Strong emphasis, high certainty</summary>
        High
    }
    
    /// <summary>
    /// Get confidence tier from p-value.
    /// </summary>
    public static ConfidenceTier GetTier(double pValue)
    {
        if (pValue < HighConfidenceThreshold) return ConfidenceTier.High;
        if (pValue < MediumConfidenceThreshold) return ConfidenceTier.Medium;
        if (pValue < LowConfidenceThreshold) return ConfidenceTier.Low;
        return ConfidenceTier.Negligible;
    }
    
    /// <summary>
    /// Get glow pulse amplitude for confidence level.
    /// Higher confidence = stronger pulse.
    /// </summary>
    public static float GetGlowPulseAmplitude(ConfidenceTier tier) => tier switch
    {
        ConfidenceTier.High => 1.0f,       // Full pulse
        ConfidenceTier.Medium => 0.6f,     // Moderate pulse
        ConfidenceTier.Low => 0.3f,        // Subtle pulse
        ConfidenceTier.Negligible => 0.1f, // Nearly static
        _ => 0.5f
    };
    
    /// <summary>
    /// Get glow pulse amplitude directly from p-value.
    /// </summary>
    public static float GetGlowPulseAmplitude(double pValue)
    {
        return GetGlowPulseAmplitude(GetTier(pValue));
    }
    
    /// <summary>
    /// Get badge saturation multiplier for confidence level.
    /// Higher confidence = more saturated colors.
    /// </summary>
    public static float GetBadgeSaturation(ConfidenceTier tier) => tier switch
    {
        ConfidenceTier.High => 1.0f,       // Full saturation
        ConfidenceTier.Medium => 0.75f,    // Slightly desaturated
        ConfidenceTier.Low => 0.5f,        // Noticeable desaturation
        ConfidenceTier.Negligible => 0.25f,// Very washed out
        _ => 0.7f
    };
    
    /// <summary>
    /// Get badge opacity for confidence level.
    /// </summary>
    public static byte GetBadgeAlpha(ConfidenceTier tier) => tier switch
    {
        ConfidenceTier.High => 255,        // Full opacity
        ConfidenceTier.Medium => 220,      // Slightly transparent
        ConfidenceTier.Low => 180,         // Moderately transparent
        ConfidenceTier.Negligible => 120,  // Very transparent
        _ => 200
    };
    
    /// <summary>
    /// Get human-readable confidence label for tier.
    /// </summary>
    public static string GetLabel(ConfidenceTier tier) => tier switch
    {
        ConfidenceTier.High => "high confidence",
        ConfidenceTier.Medium => "medium confidence",
        ConfidenceTier.Low => "low confidence",
        ConfidenceTier.Negligible => "uncertain",
        _ => "unknown confidence"
    };
    
    /// <summary>
    /// Get tooltip copy prefix for confidence level.
    /// </summary>
    public static string GetTooltipPrefix(ConfidenceTier tier) => tier switch
    {
        ConfidenceTier.High => "Strong evidence:",
        ConfidenceTier.Medium => "Moderate evidence:",
        ConfidenceTier.Low => "Weak evidence:",
        ConfidenceTier.Negligible => "Uncertain:",
        _ => "Note:"
    };
    
    /// <summary>
    /// Get accessibility description for screen readers.
    /// </summary>
    public static string GetAccessibilityDescription(ConfidenceTier tier, string deltaDescription)
    {
        var label = GetLabel(tier);
        return $"{deltaDescription}, {label}";
    }
    
    /// <summary>
    /// Apply confidence-based saturation to a color.
    /// </summary>
    public static SKColor ApplyConfidenceSaturation(SKColor color, ConfidenceTier tier)
    {
        var saturation = GetBadgeSaturation(tier);
        var alpha = GetBadgeAlpha(tier);
        
        // Convert to HSL, modify saturation, convert back
        color.ToHsl(out float h, out float s, out float l);
        var newSaturation = s * saturation;
        
        return SKColor.FromHsl(h, newSaturation, l).WithAlpha(alpha);
    }
    
    /// <summary>
    /// Apply confidence-based saturation to a color from p-value.
    /// </summary>
    public static SKColor ApplyConfidenceSaturation(SKColor color, double pValue)
    {
        return ApplyConfidenceSaturation(color, GetTier(pValue));
    }
}
