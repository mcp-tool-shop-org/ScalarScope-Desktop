// ComparisonBundle v1.0.0 Importer
// Reads and validates bundle ZIP files.

using System.IO.Compression;
using System.Text.Json;
using ScalarScope.Models;

namespace ScalarScope.Services.Bundles;

/// <summary>
/// Imports ComparisonBundle v1.0.0 from ZIP file.
/// </summary>
public sealed class BundleImporter
{
    private static readonly Lazy<BundleImporter> _instance = new(() => new BundleImporter());
    public static BundleImporter Instance => _instance.Value;
    
    private BundleImporter() { }
    
    /// <summary>
    /// Currently loaded bundle.
    /// </summary>
    public LoadedBundleV1? CurrentBundle { get; private set; }
    
    /// <summary>
    /// Whether a bundle is loaded.
    /// </summary>
    public bool HasLoadedBundle => CurrentBundle != null;
    
    /// <summary>
    /// Event raised when bundle is loaded.
    /// </summary>
    public event EventHandler<LoadedBundleV1>? BundleLoaded;
    
    /// <summary>
    /// Event raised when bundle is unloaded.
    /// </summary>
    public event EventHandler? BundleUnloaded;
    
    /// <summary>
    /// Import a bundle from file.
    /// </summary>
    public async Task<BundleImportResult> ImportAsync(string bundlePath)
    {
        var result = new BundleImportResult
        {
            BundlePath = bundlePath,
            ImportedAt = DateTimeOffset.UtcNow
        };
        
        try
        {
            // 1. Validate first
            var validation = await ValidateAsync(bundlePath);
            result.Validation = validation;
            
            if (!validation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = validation.Errors.FirstOrDefault() ?? "Validation failed";
                return result;
            }
            
            // 2. Load bundle
            var bundle = await LoadAsync(bundlePath);
            if (bundle == null)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to load bundle contents";
                return result;
            }
            
            // 3. Store and raise event
            CurrentBundle = bundle;
            result.Success = true;
            result.LoadedBundle = bundle;
            
            BundleLoaded?.Invoke(this, bundle);
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
    /// Unload current bundle.
    /// </summary>
    public void Unload()
    {
        CurrentBundle = null;
        BundleUnloaded?.Invoke(this, EventArgs.Empty);
    }
    
    /// <summary>
    /// Validate bundle without loading.
    /// </summary>
    public async Task<BundleValidationResult> ValidateAsync(string bundlePath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        
        if (!File.Exists(bundlePath))
        {
            return new BundleValidationResult
            {
                IsValid = false,
                Errors = new[] { "Bundle file not found" }
            };
        }
        
        try
        {
            using var archive = ZipFile.OpenRead(bundlePath);
            var options = BundleHashAlgorithm.GetCanonicalOptions();
            
            // Check required files
            foreach (var required in BundleBuilder.RequiredFiles)
            {
                if (archive.GetEntry(required) == null)
                {
                    errors.Add($"Missing required file: {required}");
                }
            }
            
            // Load manifest
            var manifestEntry = archive.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                errors.Add("Missing manifest.json");
                return new BundleValidationResult { IsValid = false, Errors = errors };
            }
            
            ComparisonBundleManifest? manifest;
            using (var stream = manifestEntry.Open())
            {
                manifest = await JsonSerializer.DeserializeAsync<ComparisonBundleManifest>(stream, options);
            }
            
            if (manifest == null)
            {
                errors.Add("Failed to parse manifest.json");
                return new BundleValidationResult { IsValid = false, Errors = errors };
            }
            
            // Version check
            if (!manifest.BundleVersion.StartsWith("1."))
            {
                errors.Add($"Unsupported bundle version: {manifest.BundleVersion}");
            }
            
            // Verify integrity
            var integrityResult = await VerifyIntegrityAsync(archive, manifest.Integrity);
            if (!integrityResult.IsValid)
            {
                errors.AddRange(integrityResult.Errors);
            }
            
            // Profile-specific validation
            if (manifest.Profile == BundleProfile.Audit)
            {
                if (archive.GetEntry("audit/audit.json") == null &&
                    archive.GetEntry("audit/audit.zip") == null)
                {
                    errors.Add("Audit profile requires audit/audit.json or audit/audit.zip");
                }
            }
            
            return new BundleValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings,
                Manifest = manifest
            };
        }
        catch (Exception ex)
        {
            return new BundleValidationResult
            {
                IsValid = false,
                Errors = new[] { $"Validation error: {ex.Message}" }
            };
        }
    }
    
    /// <summary>
    /// Load bundle contents.
    /// </summary>
    private async Task<LoadedBundleV1?> LoadAsync(string bundlePath)
    {
        using var archive = ZipFile.OpenRead(bundlePath);
        var options = BundleHashAlgorithm.GetCanonicalOptions();
        
        // Load manifest
        var manifest = await ReadJsonAsync<ComparisonBundleManifest>(archive, "manifest.json", options);
        if (manifest == null) return null;
        
        // Load repro
        var repro = await ReadJsonAsync<ReproPayload>(archive, "repro/repro.json", options);
        if (repro == null) return null;
        
        // Load deltas
        var deltas = await ReadJsonAsync<DeltasPayload>(archive, "findings/deltas.json", options);
        if (deltas == null) return null;
        
        // Load why
        var why = await ReadJsonAsync<WhyPayload>(archive, "findings/why.json", options);
        if (why == null) return null;
        
        // Load summary
        var summary = await ReadTextAsync(archive, "findings/summary.md");
        
        // Load insights (optional)
        var insights = await ReadJsonAsync<InsightsPayload>(archive, "insights/insights.json", options);
        
        // Load audit (optional)
        object? auditPayload = null;
        if (manifest.Profile == BundleProfile.Audit)
        {
            auditPayload = await ReadJsonAsync<object>(archive, "audit/audit.json", options);
        }
        
        // Convert to UI-compatible types
        var canonicalDeltas = deltas.Deltas.Select(ConvertToCanonicalDelta).ToList();
        var insightEvents = insights?.Events.Select(ConvertToInsightEvent).ToList();
        
        return new LoadedBundleV1
        {
            FilePath = bundlePath,
            Manifest = manifest,
            Repro = repro,
            Deltas = deltas,
            Why = why,
            SummaryMarkdown = summary,
            Insights = insights,
            AuditPayload = auditPayload,
            
            // UI-ready data
            CanonicalDeltas = canonicalDeltas,
            InsightEvents = insightEvents,
            
            LoadedAt = DateTimeOffset.UtcNow
        };
    }
    
    /// <summary>
    /// Verify bundle integrity hashes.
    /// </summary>
    private async Task<BundleValidationResult> VerifyIntegrityAsync(
        ZipArchive archive,
        IntegrityInfo integrity)
    {
        var errors = new List<string>();
        
        foreach (var fileEntry in integrity.Files)
        {
            var entry = archive.GetEntry(fileEntry.Path);
            if (entry == null)
            {
                errors.Add($"Missing file in bundle: {fileEntry.Path}");
                continue;
            }
            
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            
            var actualHash = BundleHashAlgorithm.ComputeSha256Hex(ms.ToArray());
            if (!string.Equals(actualHash, fileEntry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Hash mismatch for {fileEntry.Path}: expected {fileEntry.Sha256[..12]}..., got {actualHash[..12]}...");
            }
        }
        
        return new BundleValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    #region Helpers
    
    private static async Task<T?> ReadJsonAsync<T>(ZipArchive archive, string path, JsonSerializerOptions options)
    {
        var entry = archive.GetEntry(path);
        if (entry == null) return default;
        
        using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream, options);
    }
    
    private static async Task<string?> ReadTextAsync(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry == null) return null;
        
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
    
    private static CanonicalDelta ConvertToCanonicalDelta(DeltaEntry d)
    {
        return new CanonicalDelta
        {
            Id = d.Id,
            Name = d.Name,
            Explanation = d.Explanation,
            SummarySentence = d.SummarySentence,
            Status = d.Status switch
            {
                DeltaStatus.Present => Services.DeltaStatus.Present,
                DeltaStatus.Suppressed => Services.DeltaStatus.Suppressed,
                _ => Services.DeltaStatus.Indeterminate
            },
            DeltaType = d.Id switch
            {
                "delta_f" or "failurePresence" => Services.DeltaType.Event,       // Failure = discrete event
                "delta_tc" or "convergenceTiming" => Services.DeltaType.Timing,    // Convergence = timing
                "delta_td" or "structuralEmergence" => Services.DeltaType.Structure, // Emergence = structural
                "delta_a" or "evaluatorAlignment" => Services.DeltaType.Behavior,  // Alignment = behavioral
                "delta_o" or "stabilityOscillation" => Services.DeltaType.Behavior, // Oscillation = behavioral
                _ => Services.DeltaType.Behavior
            },
            Confidence = d.Confidence,
            Delta = d.DeltaValue ?? 0,
            Units = d.Units,
            VisualAnchorTime = d.Anchors?.FirstOrDefault()?.CompareIndex / 100.0 ?? 0,
            Notes = d.Notes?.ToList() ?? new List<string>()
        };
    }
    
    private static InsightEvent ConvertToInsightEvent(InsightEntry e)
    {
        return new InsightEvent
        {
            Id = e.Id,
            Category = e.Type switch
            {
                InsightEventType.TrainingEvent => InsightCategory.TrainingEvent,
                _ => e.Subtype switch
                {
                    "DeltaTc" => InsightCategory.DeltaConvergence,
                    "DeltaTd" => InsightCategory.DeltaEmergence,
                    "DeltaA" => InsightCategory.DeltaAlignment,
                    "DeltaO" => InsightCategory.DeltaStability,
                    "DeltaF" => InsightCategory.DeltaFailure,
                    _ => InsightCategory.TrainingEvent
                }
            },
            Title = e.Title,
            Description = e.Body,
            TriggerType = e.Meta?.TriggerType?.ToString(),
            Confidence = e.Meta?.Confidence,
            DeltaId = e.Meta?.DeltaId,
            AnchorTime = e.ShowMe?.MarkerA / 100.0,
            TargetView = e.ShowMe?.PreferredTargetView.ToString().ToLowerInvariant(),
            Timestamp = e.CreatedUtc.UtcDateTime
        };
    }
    
    #endregion
}

/// <summary>
/// Fully loaded bundle with both raw and UI-ready data.
/// </summary>
public sealed record LoadedBundleV1
{
    // File info
    public required string FilePath { get; init; }
    public required DateTimeOffset LoadedAt { get; init; }
    
    // Raw payloads (v1.0.0 schema)
    public required ComparisonBundleManifest Manifest { get; init; }
    public required ReproPayload Repro { get; init; }
    public required DeltasPayload Deltas { get; init; }
    public required WhyPayload Why { get; init; }
    public string? SummaryMarkdown { get; init; }
    public InsightsPayload? Insights { get; init; }
    public object? AuditPayload { get; init; }
    
    // UI-ready conversions
    public required List<CanonicalDelta> CanonicalDeltas { get; init; }
    public List<InsightEvent>? InsightEvents { get; init; }
    
    // Convenience properties
    public string BundleHash => Manifest.Integrity.BundleHash;
    public BundleProfile Profile => Manifest.Profile;
    public string LabelA => Manifest.Comparison.LabelA;
    public string LabelB => Manifest.Comparison.LabelB;
    public ReproStatus ReproStatus => Manifest.Reproducibility.Status;
}

/// <summary>
/// Bundle import result.
/// </summary>
public sealed record BundleImportResult
{
    public required string BundlePath { get; init; }
    public required DateTimeOffset ImportedAt { get; init; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public BundleValidationResult? Validation { get; set; }
    public LoadedBundleV1? LoadedBundle { get; set; }
}

/// <summary>
/// Bundle validation result.
/// </summary>
public sealed record BundleValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public ComparisonBundleManifest? Manifest { get; init; }
}
