using ScalarScope.Models;
using ScalarScope.Services;
using System.Text;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.4: Generate copy-ready cards for sharing deltas.
/// </summary>
public static class DeltaCopyService
{
    /// <summary>
    /// Generate a plain-text copy card for a delta.
    /// </summary>
    public static string GeneratePlainTextCard(CanonicalDelta delta)
    {
        var sb = new StringBuilder();
        
        // Header with emoji based on delta type
        var emoji = GetDeltaEmoji(delta.DeltaType);
        sb.AppendLine($"{emoji} {delta.Name}");
        sb.AppendLine(new string('─', 30));
        
        // Main explanation
        sb.AppendLine(delta.Explanation);
        sb.AppendLine();
        
        // Values
        sb.AppendLine($"📊 Path A: {FormatValue(delta.LeftValue, delta.Units)}");
        sb.AppendLine($"📊 Path B: {FormatValue(delta.RightValue, delta.Units)}");
        sb.AppendLine($"📐 Δ: {FormatValue(delta.Delta, delta.Units)} ({GetDirection(delta.Delta)})");
        sb.AppendLine();
        
        // Confidence
        var tier = ConfidenceTokens.GetTierFromConfidence(delta.Confidence);
        var confidenceLabel = ConfidenceTokens.GetLabel(tier);
        sb.AppendLine($"🎯 Confidence: {confidenceLabel} ({delta.Confidence:P0})");
        
        // Visual anchor
        sb.AppendLine($"📍 Visible at: {delta.VisualAnchorTime:P0} of timeline");
        
        sb.AppendLine();
        sb.AppendLine("─ ScalarScope Analysis");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generate a Markdown copy card for a delta.
    /// </summary>
    public static string GenerateMarkdownCard(CanonicalDelta delta)
    {
        var sb = new StringBuilder();
        
        // Header
        var emoji = GetDeltaEmoji(delta.DeltaType);
        sb.AppendLine($"### {emoji} {delta.Name}");
        sb.AppendLine();
        
        // Main explanation
        sb.AppendLine($"> {delta.Explanation}");
        sb.AppendLine();
        
        // Table of values
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Path A | {FormatValue(delta.LeftValue, delta.Units)} |");
        sb.AppendLine($"| Path B | {FormatValue(delta.RightValue, delta.Units)} |");
        sb.AppendLine($"| Delta | {FormatValue(delta.Delta, delta.Units)} ({GetDirection(delta.Delta)}) |");
        sb.AppendLine($"| Confidence | {ConfidenceTokens.GetLabel(ConfidenceTokens.GetTierFromConfidence(delta.Confidence))} |");
        sb.AppendLine();
        
        // Footer
        sb.AppendLine($"*Visible at {delta.VisualAnchorTime:P0} of timeline • ScalarScope*");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Phase 5.4: Generate plain-language executive summary.
    /// Non-technical stakeholders can understand this.
    /// </summary>
    public static string GeneratePlainLanguageSummary(IEnumerable<CanonicalDelta> deltas, string? leftRunName = null, string? rightRunName = null)
    {
        var deltaList = deltas.Where(d => d.Status == DeltaStatus.Present).ToList();
        var pathA = leftRunName ?? "Path A";
        var pathB = rightRunName ?? "Path B";
        
        if (deltaList.Count == 0)
        {
            return $"**Bottom Line:** {pathA} and {pathB} performed similarly. " +
                   "No significant differences were detected in timing, stability, or outcome.";
        }
        
        var sb = new StringBuilder();
        sb.AppendLine("## What We Found");
        sb.AppendLine();
        
        // Group by confidence for priority ordering
        var highConfidence = deltaList.Where(d => d.Confidence >= 0.99).ToList();
        var mediumConfidence = deltaList.Where(d => d.Confidence >= 0.95 && d.Confidence < 0.99).ToList();
        var lowConfidence = deltaList.Where(d => d.Confidence < 0.95).ToList();
        
        // Summary opening
        sb.AppendLine($"Comparing **{pathA}** and **{pathB}**, we found {deltaList.Count} notable difference{(deltaList.Count > 1 ? "s" : "")}:");
        sb.AppendLine();
        
        // High confidence findings (definite)
        if (highConfidence.Any())
        {
            sb.AppendLine("### Clear Findings (High Confidence)");
            foreach (var delta in highConfidence)
            {
                sb.AppendLine($"- {TranslateToPlainLanguage(delta, pathA, pathB)}");
            }
            sb.AppendLine();
        }
        
        // Medium confidence findings (likely)
        if (mediumConfidence.Any())
        {
            sb.AppendLine("### Likely Findings (Moderate Confidence)");
            foreach (var delta in mediumConfidence)
            {
                sb.AppendLine($"- {TranslateToPlainLanguage(delta, pathA, pathB)}");
            }
            sb.AppendLine();
        }
        
        // Low confidence findings (possible)
        if (lowConfidence.Any())
        {
            sb.AppendLine("### Possible Findings (Lower Confidence)");
            foreach (var delta in lowConfidence)
            {
                sb.AppendLine($"- {TranslateToPlainLanguage(delta, pathA, pathB)}");
            }
            sb.AppendLine();
        }
        
        // Bottom line
        sb.AppendLine("---");
        sb.AppendLine("**Bottom Line:** " + GenerateBottomLine(deltaList, pathA, pathB));
        
        return sb.ToString();
    }
    
    private static string TranslateToPlainLanguage(CanonicalDelta delta, string pathA, string pathB)
    {
        var winner = delta.Delta > 0 ? pathB : pathA;
        var loser = delta.Delta > 0 ? pathA : pathB;
        
        return delta.Id switch
        {
            "delta_tc" => delta.Delta > 0
                ? $"{loser} converged {Math.Abs((int)(delta.Delta * 100))} steps faster than {winner}"
                : $"{winner} converged {Math.Abs((int)(delta.Delta * 100))} steps faster than {loser}",
                
            "delta_td" => delta.Delta > 0
                ? $"{winner} showed stronger emergence of shared structure"
                : $"{loser} showed stronger emergence of shared structure",
                
            "delta_a" => delta.Delta > 0
                ? $"{winner} had better final alignment (professors agreed more)"
                : $"{loser} had better final alignment (professors agreed more)",
                
            "delta_o" => delta.Delta > 0
                ? $"{winner} was less stable during training (more oscillation)"
                : $"{loser} was less stable during training (more oscillation)",
                
            "delta_f" => $"Only {winner} completed without failure",
                
            _ => delta.Explanation
        };
    }
    
    private static string GenerateBottomLine(List<CanonicalDelta> deltas, string pathA, string pathB)
    {
        // Check for failure delta
        var failure = deltas.FirstOrDefault(d => d.Id == "delta_f");
        if (failure != null)
        {
            var failedPath = failure.FailedA == true ? pathA : pathB;
            var successPath = failure.FailedA == true ? pathB : pathA;
            return $"{successPath} completed successfully while {failedPath} failed. " +
                   "The successful approach should be preferred.";
        }
        
        // Check dominance (emergence) delta
        var dominance = deltas.FirstOrDefault(d => d.Id == "delta_td");
        var convergence = deltas.FirstOrDefault(d => d.Id == "delta_tc");
        
        if (dominance != null && dominance.Confidence >= 0.95)
        {
            var better = dominance.Delta > 0 ? pathB : pathA;
            return $"{better} showed clearer emergence of shared evaluative structure, " +
                   "suggesting better generalization potential.";
        }
        
        if (convergence != null && convergence.Confidence >= 0.95)
        {
            var faster = convergence.Delta > 0 ? pathA : pathB;
            return $"{faster} converged faster, which may indicate more efficient learning.";
        }
        
        return "The differences detected are relatively minor. " +
               "Either approach appears viable, but review individual findings for specific trade-offs.";
    }
    
    /// <summary>
    /// Generate a summary card for multiple deltas.
    /// </summary>
    public static string GenerateSummaryCard(IEnumerable<CanonicalDelta> deltas)
    {
        var deltaList = deltas.Where(d => d.Status == DeltaStatus.Present).ToList();
        if (deltaList.Count == 0)
            return "No significant differences detected between the runs.";
        
        var sb = new StringBuilder();
        sb.AppendLine("## ScalarScope Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"**{deltaList.Count} differences detected:**");
        sb.AppendLine();
        
        foreach (var delta in deltaList)
        {
            var emoji = GetDeltaEmoji(delta.DeltaType);
            var tier = ConfidenceTokens.GetTierFromConfidence(delta.Confidence);
            var badge = tier switch
            {
                ConfidenceTokens.ConfidenceTier.High => "🟢",
                ConfidenceTokens.ConfidenceTier.Medium => "🟡",
                ConfidenceTokens.ConfidenceTier.Low => "🟠",
                _ => "⚪"
            };
            sb.AppendLine($"- {emoji} **{delta.Name}** {badge}");
            sb.AppendLine($"  {delta.Explanation}");
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine("*Generated by ScalarScope*");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Copy delta card to clipboard.
    /// </summary>
    public static async Task CopyToClipboardAsync(CanonicalDelta delta, bool useMarkdown = false)
    {
        var text = useMarkdown 
            ? GenerateMarkdownCard(delta) 
            : GeneratePlainTextCard(delta);
        
        await Clipboard.Default.SetTextAsync(text);
    }
    
    /// <summary>
    /// Copy summary card to clipboard.
    /// </summary>
    public static async Task CopySummaryToClipboardAsync(IEnumerable<CanonicalDelta> deltas)
    {
        var text = GenerateSummaryCard(deltas);
        await Clipboard.Default.SetTextAsync(text);
    }
    
    private static string GetDeltaEmoji(DeltaType type) => type switch
    {
        DeltaType.Event => "⚠️",
        DeltaType.Timing => "⏱️",
        DeltaType.Structure => "🔷",
        DeltaType.Behavior => "📈",
        _ => "🔹"
    };
    
    private static string FormatValue(double value, string? units)
    {
        var formatted = Math.Abs(value) < 0.01 || Math.Abs(value) > 1000
            ? value.ToString("G3")
            : value.ToString("F2");
        
        return string.IsNullOrEmpty(units) ? formatted : $"{formatted} {units}";
    }
    
    private static string GetDirection(double delta) => delta switch
    {
        > 0 => "↑",
        < 0 => "↓",
        _ => "="
    };
}
