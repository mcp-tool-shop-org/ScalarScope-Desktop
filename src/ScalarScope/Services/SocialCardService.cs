using ScalarScope.Models;
using SkiaSharp;
using System.Text;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.4: Generate social media card images for sharing.
/// Optimized for Twitter, LinkedIn, and presentation slides.
/// </summary>
public static class SocialCardService
{
    // Standard social card dimensions
    public const int TwitterCardWidth = 1200;
    public const int TwitterCardHeight = 628;
    
    public const int LinkedInCardWidth = 1200;
    public const int LinkedInCardHeight = 627;
    
    public const int SlideWidth = 1920;
    public const int SlideHeight = 1080;
    
    /// <summary>
    /// Generate a social card image for a comparison.
    /// </summary>
    public static async Task<byte[]> GenerateSocialCardAsync(
        IEnumerable<CanonicalDelta> deltas,
        string leftRunName,
        string rightRunName,
        SocialCardFormat format = SocialCardFormat.Twitter)
    {
        var (width, height) = format switch
        {
            SocialCardFormat.LinkedIn => (LinkedInCardWidth, LinkedInCardHeight),
            SocialCardFormat.Slide => (SlideWidth, SlideHeight),
            _ => (TwitterCardWidth, TwitterCardHeight)
        };
        
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        
        // Background gradient
        using var gradientPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, height),
                [SKColor.Parse("#0f0f1a"), SKColor.Parse("#1a1a2e")],
                [0f, 1f],
                SKShaderTileMode.Clamp),
            IsAntialias = true
        };
        canvas.DrawRect(0, 0, width, height, gradientPaint);
        
        // Header
        DrawHeader(canvas, width, leftRunName, rightRunName);
        
        // Delta summary
        var deltaList = deltas.Where(d => d.Status == DeltaStatus.Present).ToList();
        DrawDeltaSummary(canvas, width, height, deltaList);
        
        // Footer with branding
        DrawFooter(canvas, width, height);
        
        // Export as PNG
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        
        return await Task.FromResult(data.ToArray());
    }
    
    private static void DrawHeader(SKCanvas canvas, int width, string leftName, string rightName)
    {
        using var titleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 48);
        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        
        canvas.DrawText("Comparison Analysis", width / 2, 80, SKTextAlign.Center, titleFont, titlePaint);
        
        // Run names
        using var subtitleFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 24);
        using var accentPaint = new SKPaint
        {
            Color = SKColor.Parse("#00d9ff"),
            IsAntialias = true
        };
        
        canvas.DrawText($"{leftName} vs {rightName}", width / 2, 130, SKTextAlign.Center, subtitleFont, accentPaint);
        
        // Divider line
        using var dividerPaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawLine(100, 160, width - 100, 160, dividerPaint);
    }
    
    private static void DrawDeltaSummary(SKCanvas canvas, int width, int height, List<CanonicalDelta> deltas)
    {
        var startY = 200f;
        var cardWidth = width - 200;
        var cardHeight = 80f;
        var cardSpacing = 90f;
        var maxCards = Math.Min(deltas.Count, 4);
        
        using var cardPaint = new SKPaint
        {
            Color = SKColor.Parse("#1a1a2e"),
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        
        using var borderPaint = new SKPaint
        {
            Color = SKColor.Parse("#2a2a4e"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 20);
        using var descFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal), 16);
        
        for (int i = 0; i < maxCards; i++)
        {
            var delta = deltas[i];
            var y = startY + i * cardSpacing;
            var cardRect = new SKRect(100, y, 100 + cardWidth, y + cardHeight);
            
            // Card background
            canvas.DrawRoundRect(cardRect, 8, 8, cardPaint);
            canvas.DrawRoundRect(cardRect, 8, 8, borderPaint);
            
            // Delta type accent bar
            var accentColor = GetDeltaTypeColor(delta.DeltaType);
            using var accentPaint = new SKPaint
            {
                Color = accentColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(new SKRect(100, y, 108, y + cardHeight), new SKSize(4, 4), accentPaint);
            
            // Delta name
            using var namePaint = new SKPaint { Color = accentColor, IsAntialias = true };
            canvas.DrawText(delta.Name, 130, y + 30, SKTextAlign.Left, nameFont, namePaint);
            
            // Delta explanation
            using var descPaint = new SKPaint { Color = SKColor.Parse("#aaa"), IsAntialias = true };
            var maxChars = 70;
            var explanation = delta.Explanation.Length > maxChars 
                ? delta.Explanation[..maxChars] + "..." 
                : delta.Explanation;
            canvas.DrawText(explanation, 130, y + 55, SKTextAlign.Left, descFont, descPaint);
            
            // Confidence badge
            var tier = ConfidenceTokens.GetTierFromConfidence(delta.Confidence);
            DrawConfidenceBadge(canvas, cardRect.Right - 80, y + 25, tier);
        }
        
        // "And X more..." if there are more deltas
        if (deltas.Count > maxCards)
        {
            using var morePaint = new SKPaint { Color = SKColor.Parse("#666"), IsAntialias = true };
            using var moreFont = new SKFont(SKTypeface.Default, 14);
            canvas.DrawText($"+ {deltas.Count - maxCards} more findings...", 
                width / 2, startY + maxCards * cardSpacing + 20, SKTextAlign.Center, moreFont, morePaint);
        }
    }
    
    private static void DrawConfidenceBadge(SKCanvas canvas, float x, float y, ConfidenceTokens.ConfidenceTier tier)
    {
        var (bgColor, textColor, text) = tier switch
        {
            ConfidenceTokens.ConfidenceTier.High => (SKColor.Parse("#1a4d1a"), SKColor.Parse("#4ade80"), "HIGH"),
            ConfidenceTokens.ConfidenceTier.Medium => (SKColor.Parse("#3d3d1a"), SKColor.Parse("#fbbf24"), "MED"),
            ConfidenceTokens.ConfidenceTier.Low => (SKColor.Parse("#4d3d1a"), SKColor.Parse("#fb923c"), "LOW"),
            _ => (SKColor.Parse("#2a2a2a"), SKColor.Parse("#888"), "???")
        };
        
        var badgeRect = new SKRect(x, y, x + 50, y + 24);
        
        using var bgPaint = new SKPaint { Color = bgColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var textPaint = new SKPaint { Color = textColor, IsAntialias = true };
        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 10);
        
        canvas.DrawRoundRect(badgeRect, 4, 4, bgPaint);
        canvas.DrawText(text, badgeRect.MidX, badgeRect.MidY + 4, SKTextAlign.Center, font, textPaint);
    }
    
    private static void DrawFooter(SKCanvas canvas, int width, int height)
    {
        // Gradient line
        using var linePaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(100, 0),
                new SKPoint(width - 100, 0),
                [SKColor.Parse("#00d9ff"), SKColor.Parse("#a29bfe")],
                [0f, 1f],
                SKShaderTileMode.Clamp),
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawLine(100, height - 80, width - 100, height - 80, linePaint);
        
        // Branding
        using var brandFont = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 20);
        using var brandPaint = new SKPaint { Color = SKColor.Parse("#00d9ff"), IsAntialias = true };
        canvas.DrawText("ScalarScope", 100, height - 40, SKTextAlign.Left, brandFont, brandPaint);
        
        // Timestamp/attribution
        using var timestampFont = new SKFont(SKTypeface.Default, 12);
        using var timestampPaint = new SKPaint { Color = SKColor.Parse("#666"), IsAntialias = true };
        canvas.DrawText($"Generated {DateTime.Now:yyyy-MM-dd}", width - 100, height - 40, SKTextAlign.Right, timestampFont, timestampPaint);
    }
    
    private static SKColor GetDeltaTypeColor(DeltaType type) => type switch
    {
        DeltaType.Event => SKColor.Parse("#ff6b6b"),
        DeltaType.Timing => SKColor.Parse("#4ecdc4"),
        DeltaType.Structure => SKColor.Parse("#a29bfe"),
        DeltaType.Behavior => SKColor.Parse("#ffd93d"),
        _ => SKColor.Parse("#888")
    };
    
    /// <summary>
    /// Save social card to file.
    /// </summary>
    public static async Task SaveToFileAsync(
        IEnumerable<CanonicalDelta> deltas,
        string leftRunName,
        string rightRunName,
        string outputPath,
        SocialCardFormat format = SocialCardFormat.Twitter)
    {
        var imageData = await GenerateSocialCardAsync(deltas, leftRunName, rightRunName, format);
        await File.WriteAllBytesAsync(outputPath, imageData);
    }
}

/// <summary>
/// Social card format presets.
/// </summary>
public enum SocialCardFormat
{
    /// <summary>Twitter/X optimal: 1200x628</summary>
    Twitter,
    
    /// <summary>LinkedIn optimal: 1200x627</summary>
    LinkedIn,
    
    /// <summary>Presentation slide: 1920x1080</summary>
    Slide
}
