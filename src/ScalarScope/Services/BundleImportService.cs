// Phase 7.2.4: Bundle Import Service
// Handles loading and parsing comparison bundles.

using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScalarScope.Models;

// Type aliases for clarity
using CanonicalDelta = ScalarScope.Services.CanonicalDelta;
using DeltaType = ScalarScope.Services.DeltaType;
using DeltaStatus = ScalarScope.Services.DeltaStatus;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.2: Service for importing comparison bundles.
/// Loads bundle contents and provides hydrated data for UI display.
/// </summary>
public sealed class BundleImportService
{
    private static readonly Lazy<BundleImportService> _instance = 
        new(() => new BundleImportService());
    
    public static BundleImportService Instance => _instance.Value;
    
    private BundleImportService() { }
    
    /// <summary>
    /// Currently loaded bundle (if any).
    /// </summary>
    public LoadedBundle? CurrentBundle { get; private set; }
    
    /// <summary>
    /// Whether a bundle is currently loaded.
    /// </summary>
    public bool HasLoadedBundle => CurrentBundle is not null;
    
    /// <summary>
    /// Event raised when a bundle is loaded.
    /// </summary>
    public event EventHandler<LoadedBundle>? BundleLoaded;
    
    /// <summary>
    /// Event raised when the bundle is unloaded.
    /// </summary>
    public event EventHandler? BundleUnloaded;
    
    /// <summary>
    /// Import a bundle from file path.
    /// </summary>
    public async Task<BundleImportResult> ImportAsync(string bundlePath)
    {
        var result = new BundleImportResult
        {
            BundlePath = bundlePath,
            ImportedAt = DateTime.UtcNow
        };
        
        try
        {
            // Validate first
            var validation = await BundleSchemaValidator.ValidateAsync(bundlePath);
            result.Validation = validation;
            
            if (!validation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = validation.Issues.FirstOrDefault(i => 
                    i.Severity == BundleValidationSeverity.Error)?.Message ?? "Validation failed";
                result.ErrorExplanation = validation.GetErrorExplanation();
                return result;
            }
            
            // Load bundle contents
            var loaded = await LoadBundleAsync(bundlePath);
            if (loaded is null)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to load bundle contents";
                return result;
            }
            
            // Store current bundle
            CurrentBundle = loaded;
            result.LoadedBundle = loaded;
            result.Success = true;
            
            // Raise event
            BundleLoaded?.Invoke(this, loaded);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Import failed: {ex.Message}";
            result.Exception = ex;
            
            ErrorLoggingService.Instance.Log(ex, "BundleImport");
        }
        
        return result;
    }
    
    /// <summary>
    /// Unload the current bundle.
    /// </summary>
    public void Unload()
    {
        CurrentBundle = null;
        BundleUnloaded?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Load bundle contents from file.
    /// </summary>
    private async Task<LoadedBundle?> LoadBundleAsync(string bundlePath)
    {
        using var archive = ZipFile.OpenRead(bundlePath);
        
        // Load manifest
        var manifest = await ReadJsonAsync<BundleManifest>(archive, "manifest.json");
        if (manifest is null) return null;
        
        // Load findings
        var deltas = await ReadJsonAsync<List<BundleDelta>>(archive, "findings/deltas.json") ?? [];
        var whyExplanations = await ReadJsonAsync<List<WhyExplanation>>(archive, "findings/why.json") ?? [];
        var summary = await ReadTextAsync(archive, "findings/summary.md");
        
        // Load repro
        var repro = await ReadJsonAsync<BundleRepro>(archive, "repro/repro.json");
        
        // Load environment
        var environment = await ReadJsonAsync<BundleEnvironment>(archive, "environment/environment.json");
        
        // Load insights (optional)
        var insights = await ReadJsonAsync<List<BundleInsight>>(archive, "insights/insights.json");
        
        // Load integrity
        var integrity = await ReadJsonAsync<BundleIntegrityInfo>(archive, "integrity.json");
        
        // Load audit bundle (if present)
        var auditBundle = await ReadJsonAsync<ReproAuditBundle>(archive, "audit/audit.json");
        
        // Convert BundleDeltas to CanonicalDeltas for UI display
        var canonicalDeltas = deltas.Select(ConvertToCanonicalDelta).ToList();
        
        // Convert BundleInsights to InsightEvents for UI display
        var insightEvents = insights?.Select(ConvertToInsightEvent).ToList();
        
        return new LoadedBundle
        {
            FilePath = bundlePath,
            Manifest = manifest,
            Deltas = canonicalDeltas,
            WhyExplanations = whyExplanations,
            SummaryMarkdown = summary,
            Repro = repro,
            Environment = environment,
            Insights = insightEvents,
            Integrity = integrity,
            AuditBundle = auditBundle,
            LoadedAt = DateTime.UtcNow
        };
    }
    
    private static CanonicalDelta ConvertToCanonicalDelta(BundleDelta bd)
    {
        return new CanonicalDelta
        {
            Id = bd.Id,
            Name = bd.Name,
            Explanation = bd.Explanation,
            SummarySentence = bd.SummarySentence,
            DeltaType = Enum.TryParse<DeltaType>(bd.DeltaType, out var dt) ? dt : DeltaType.Behavior,
            Status = Enum.TryParse<DeltaStatus>(bd.Status, out var ds) ? ds : DeltaStatus.Present,
            LeftValue = bd.LeftValue,
            RightValue = bd.RightValue,
            Delta = bd.Delta,
            Magnitude = bd.Magnitude,
            Units = bd.Units,
            Confidence = bd.Confidence,
            VisualAnchorTime = bd.VisualAnchorTime,
            Notes = bd.Notes
        };
    }
    
    private static InsightEvent ConvertToInsightEvent(BundleInsight bi)
    {
        return new InsightEvent
        {
            Id = bi.Id,
            Category = Enum.TryParse<InsightCategory>(bi.Category, out var cat) ? cat : InsightCategory.TrainingEvent,
            Title = bi.Title,
            Description = bi.Description,
            WhyFired = bi.WhyFired,
            TriggerType = bi.TriggerType,
            Parameters = bi.Parameters,
            Confidence = bi.Confidence,
            AnchorTime = bi.AnchorTime,
            DeltaId = bi.DeltaId,
            Timestamp = bi.Timestamp
        };
    }
    
    private static async Task<T?> ReadJsonAsync<T>(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry is null) return default;
        
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        
        return JsonSerializer.Deserialize<T>(json, GetJsonOptions());
    }
    
    private static async Task<string?> ReadTextAsync(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry is null) return null;
        
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

/// <summary>
/// A fully loaded bundle with hydrated data.
/// </summary>
public record LoadedBundle
{
    public required string FilePath { get; init; }
    public required BundleManifest Manifest { get; init; }
    public required List<CanonicalDelta> Deltas { get; init; }
    public required List<WhyExplanation> WhyExplanations { get; init; }
    public string? SummaryMarkdown { get; init; }
    public BundleRepro? Repro { get; init; }
    public BundleEnvironment? Environment { get; init; }
    public List<InsightEvent>? Insights { get; init; }
    public BundleIntegrityInfo? Integrity { get; init; }
    public ReproAuditBundle? AuditBundle { get; init; }
    public DateTime LoadedAt { get; init; }
    
    /// <summary>
    /// Get the bundle hash.
    /// </summary>
    public string? BundleHash => Integrity?.BundleHash;
    
    /// <summary>
    /// Get reproducibility badge state.
    /// </summary>
    public string ReproducibilityBadge => Repro?.ReproducibilityBadge ?? "Unknown";
    
    /// <summary>
    /// Check if this is an Audit bundle (has full audit data).
    /// </summary>
    public bool IsAuditBundle => AuditBundle is not null;
    
    /// <summary>
    /// Get Why explanation for a delta.
    /// </summary>
    public WhyExplanation? GetWhyExplanation(string deltaId)
    {
        return WhyExplanations.FirstOrDefault(w => w.DeltaId == deltaId);
    }
}

/// <summary>
/// Result of a bundle import operation.
/// </summary>
public record BundleImportResult
{
    public required string BundlePath { get; init; }
    public DateTime ImportedAt { get; init; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ErrorExplanation? ErrorExplanation { get; set; }
    public Exception? Exception { get; set; }
    public BundleValidationResult? Validation { get; set; }
    public LoadedBundle? LoadedBundle { get; set; }
    
    /// <summary>
    /// Get a summary of the import result.
    /// </summary>
    public string GetSummary()
    {
        if (Success)
        {
            var bundle = LoadedBundle!;
            return $"Loaded bundle: {bundle.Manifest.Profile} profile, " +
                   $"{bundle.Deltas.Count} deltas, " +
                   $"created {bundle.Manifest.CreatedAt:yyyy-MM-dd HH:mm}";
        }
        
        return $"Import failed: {ErrorMessage}";
    }
}
