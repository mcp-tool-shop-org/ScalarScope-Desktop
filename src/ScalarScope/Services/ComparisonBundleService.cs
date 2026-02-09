// Phase 7.2.1: Comparison Bundle Service
// Defines canonical ComparisonBundle format with versioning.
// Central service for creating, validating, and managing bundles.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.2: Service for creating and managing comparison bundles.
/// Bundles contain everything needed to share or archive a comparison.
/// </summary>
public sealed class ComparisonBundleService
{
    private static readonly Lazy<ComparisonBundleService> _instance = 
        new(() => new ComparisonBundleService());
    
    public static ComparisonBundleService Instance => _instance.Value;
    
    private readonly string _bundleDirectory;
    
    private ComparisonBundleService()
    {
        _bundleDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "bundles");
        
        Directory.CreateDirectory(_bundleDirectory);
    }
    
    /// <summary>
    /// Current bundle spec version.
    /// </summary>
    public const string BundleSpecVersion = "1.0.0";
    
    /// <summary>
    /// Bundle file extension.
    /// </summary>
    public const string BundleExtension = ".scbundle";
    
    /// <summary>
    /// Create a comparison bundle from a computation result.
    /// </summary>
    public ComparisonBundle CreateBundle(
        DeltaComputationResult result,
        string? trajectory1Path,
        string? trajectory2Path,
        BundleProfile profile,
        ImportPreset? preset = null,
        List<InsightEvent>? insights = null,
        string? notes = null)
    {
        var bundleId = Guid.NewGuid().ToString("N");
        var createdAt = DateTime.UtcNow;
        
        // Build manifest
        var manifest = new BundleManifest
        {
            BundleId = bundleId,
            BundleVersion = BundleSpecVersion,
            Profile = profile,
            CreatedAt = createdAt,
            AppVersion = AppSession.Version,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            
            // Privacy policy
            Privacy = new BundlePrivacyPolicy
            {
                IncludesRawData = false,
                IncludesFilePaths = profile == BundleProfile.Audit,
                IncludesMachineName = profile == BundleProfile.Audit,
                DataClassification = "Internal"
            },
            
            // Contents manifest (files to include)
            Contents = BuildContentsManifest(profile)
        };
        
        // Build findings section
        var findings = new BundleFindings
        {
            Deltas = result.Deltas?.Select(d => new BundleDelta
            {
                Id = d.Id,
                Name = d.Name,
                Explanation = d.Explanation,
                SummarySentence = d.SummarySentence,
                DeltaType = d.DeltaType.ToString(),
                Status = d.Status.ToString(),
                LeftValue = d.LeftValue,
                RightValue = d.RightValue,
                Delta = d.Delta,
                Magnitude = d.Magnitude,
                Units = d.Units,
                Confidence = d.Confidence,
                VisualAnchorTime = d.VisualAnchorTime,
                IsMeaningful = d.IsMeaningful,
                Notes = d.Notes
            }).ToList() ?? [],
            
            // Build Why explanations
            WhyExplanations = BuildWhyExplanations(result.Deltas),
            
            // Comparative summary from result
            ComparativeSummary = result.ComparativeSummary
        };
        
        // Build repro section
        var repro = new BundleRepro
        {
            InputFingerprint = result.InputFingerprint ?? "",
            DeltaHash = result.DeltaHash ?? "",
            DeterminismEnabled = DeterminismService.IsDeterministic,
            DeterminismSeed = DeterminismService.Seed,
            DeltaSpecVersion = ComparisonReplayService.DeltaSpecVersion,
            PresetId = preset?.Id,
            PresetHash = preset?.GetSettingsFingerprint(),
            ReproducibilityBadge = result.GetReproducibilityStatus().Level.ToString(),
            AlignmentMode = result.Alignment.Mode.ToString(),
            TimestepCount = result.Alignment.CompareIndex.Length
        };
        
        // Build environment section
        var environment = new BundleEnvironment
        {
            AppVersion = AppSession.Version,
            Platform = Environment.OSVersion.ToString(),
            DotNetVersion = Environment.Version.ToString(),
            Is64BitProcess = Environment.Is64BitProcess,
            ProcessorCount = Environment.ProcessorCount,
            MachineName = profile == BundleProfile.Audit ? Environment.MachineName : null
        };
        
        // Build insights section (optional for Share profile)
        var bundleInsights = profile != BundleProfile.Share && insights is not null
            ? insights.Select(i => new BundleInsight
            {
                Id = i.Id,
                Category = i.Category.ToString(),
                Title = i.Title,
                Description = i.Description,
                WhyFired = i.WhyFired,
                TriggerType = i.TriggerType,
                Parameters = i.Parameters,
                Confidence = i.Confidence,
                AnchorTime = i.AnchorTime,
                DeltaId = i.DeltaId,
                Timestamp = i.Timestamp
            }).ToList()
            : null;
        
        return new ComparisonBundle
        {
            Manifest = manifest,
            Findings = findings,
            Repro = repro,
            Environment = environment,
            Insights = bundleInsights,
            Notes = notes
        };
    }
    
    /// <summary>
    /// Export bundle to ZIP file.
    /// </summary>
    public async Task<BundleExportResult> ExportAsync(
        ComparisonBundle bundle,
        string? outputPath = null,
        List<BundleAsset>? assets = null,
        ReproAuditBundle? auditBundle = null)
    {
        var fileName = $"comparison-{bundle.Manifest.BundleId[..8]}-{DateTime.UtcNow:yyyyMMdd-HHmmss}{BundleExtension}";
        var path = outputPath ?? Path.Combine(_bundleDirectory, fileName);
        
        var fileHashes = new Dictionary<string, string>();
        
        try
        {
            using var stream = new FileStream(path, FileMode.Create);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            
            // 1. Write manifest.json
            var manifestJson = JsonSerializer.Serialize(bundle.Manifest, GetJsonOptions());
            await WriteEntryAsync(archive, "manifest.json", manifestJson);
            fileHashes["manifest.json"] = BundleIntegrityService.ComputeHash(manifestJson);
            
            // 2. Write findings/
            var deltasJson = JsonSerializer.Serialize(bundle.Findings.Deltas, GetJsonOptions());
            await WriteEntryAsync(archive, "findings/deltas.json", deltasJson);
            fileHashes["findings/deltas.json"] = BundleIntegrityService.ComputeHash(deltasJson);
            
            var whyJson = JsonSerializer.Serialize(bundle.Findings.WhyExplanations, GetJsonOptions());
            await WriteEntryAsync(archive, "findings/why.json", whyJson);
            fileHashes["findings/why.json"] = BundleIntegrityService.ComputeHash(whyJson);
            
            // Generate summary markdown
            var summaryMd = GenerateSummaryMarkdown(bundle);
            await WriteEntryAsync(archive, "findings/summary.md", summaryMd);
            fileHashes["findings/summary.md"] = BundleIntegrityService.ComputeHash(summaryMd);
            
            // 3. Write insights/ (if present)
            if (bundle.Insights is not null && bundle.Insights.Count > 0)
            {
                var insightsJson = JsonSerializer.Serialize(bundle.Insights, GetJsonOptions());
                await WriteEntryAsync(archive, "insights/insights.json", insightsJson);
                fileHashes["insights/insights.json"] = BundleIntegrityService.ComputeHash(insightsJson);
            }
            
            // 4. Write repro/
            var reproJson = JsonSerializer.Serialize(bundle.Repro, GetJsonOptions());
            await WriteEntryAsync(archive, "repro/repro.json", reproJson);
            fileHashes["repro/repro.json"] = BundleIntegrityService.ComputeHash(reproJson);
            
            // 5. Write environment/
            var envJson = JsonSerializer.Serialize(bundle.Environment, GetJsonOptions());
            await WriteEntryAsync(archive, "environment/environment.json", envJson);
            fileHashes["environment/environment.json"] = BundleIntegrityService.ComputeHash(envJson);
            
            // 6. Write assets/ (for Review and Audit profiles)
            if (assets is not null && bundle.Manifest.Profile != BundleProfile.Share)
            {
                foreach (var asset in assets)
                {
                    var assetPath = $"assets/{asset.FileName}";
                    if (asset.Data is not null)
                    {
                        var entry = archive.CreateEntry(assetPath);
                        using var entryStream = entry.Open();
                        await entryStream.WriteAsync(asset.Data);
                        fileHashes[assetPath] = BundleIntegrityService.ComputeHash(asset.Data);
                    }
                }
            }
            
            // 7. Write audit/ (for Audit profile)
            if (bundle.Manifest.Profile == BundleProfile.Audit && auditBundle is not null)
            {
                var auditJson = JsonSerializer.Serialize(auditBundle, GetJsonOptions());
                await WriteEntryAsync(archive, "audit/audit.json", auditJson);
                fileHashes["audit/audit.json"] = BundleIntegrityService.ComputeHash(auditJson);
            }
            
            // 8. Write README
            var readme = GenerateReadme(bundle);
            await WriteEntryAsync(archive, "README.md", readme);
            fileHashes["README.md"] = BundleIntegrityService.ComputeHash(readme);
            
            // 9. Update manifest with file hashes and write integrity file
            var integrityInfo = new BundleIntegrityInfo
            {
                FileHashes = fileHashes,
                BundleHash = BundleIntegrityService.ComputeBundleHash(fileHashes),
                ComputedAt = DateTime.UtcNow
            };
            
            var integrityJson = JsonSerializer.Serialize(integrityInfo, GetJsonOptions());
            await WriteEntryAsync(archive, "integrity.json", integrityJson);
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "BundleExport");
            return new BundleExportResult
            {
                Success = false,
                ErrorMessage = $"Export failed: {ex.Message}",
                BundleId = bundle.Manifest.BundleId
            };
        }
        
        return new BundleExportResult
        {
            Success = true,
            FilePath = path,
            BundleId = bundle.Manifest.BundleId,
            BundleHash = BundleIntegrityService.ComputeBundleHash(fileHashes),
            FileCount = fileHashes.Count + 1 // +1 for integrity.json
        };
    }
    
    /// <summary>
    /// Validate a bundle file.
    /// </summary>
    public async Task<BundleValidationResult> ValidateAsync(string bundlePath)
    {
        return await BundleSchemaValidator.ValidateAsync(bundlePath);
    }
    
    /// <summary>
    /// Get all exported bundles.
    /// </summary>
    public IReadOnlyList<BundleManifest> GetExportedBundles()
    {
        var bundles = new List<BundleManifest>();
        var files = Directory.GetFiles(_bundleDirectory, $"*{BundleExtension}");
        
        foreach (var file in files)
        {
            try
            {
                using var archive = ZipFile.OpenRead(file);
                var manifestEntry = archive.GetEntry("manifest.json");
                if (manifestEntry is not null)
                {
                    using var stream = manifestEntry.Open();
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var manifest = JsonSerializer.Deserialize<BundleManifest>(json, GetJsonOptions());
                    if (manifest is not null)
                    {
                        bundles.Add(manifest);
                    }
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return bundles.OrderByDescending(b => b.CreatedAt).ToList().AsReadOnly();
    }
    
    private static List<WhyExplanation> BuildWhyExplanations(List<CanonicalDelta>? deltas)
    {
        if (deltas is null) return [];
        
        return deltas.Select(d => new WhyExplanation
        {
            DeltaId = d.Id,
            DeltaName = d.Name,
            Explanation = d.Explanation,
            SummarySentence = d.SummarySentence,
            Confidence = d.Confidence,
            Parameters = new Dictionary<string, string>
            {
                ["leftValue"] = d.LeftValue.ToString("G6"),
                ["rightValue"] = d.RightValue.ToString("G6"),
                ["delta"] = d.Delta.ToString("G6"),
                ["magnitude"] = d.Magnitude.ToString("G6")
            }
        }).ToList();
    }
    
    private static BundleContentsManifest BuildContentsManifest(BundleProfile profile)
    {
        return new BundleContentsManifest
        {
            IncludesFindings = true,
            IncludesRepro = true,
            IncludesEnvironment = true,
            IncludesInsights = profile != BundleProfile.Share,
            IncludesAssets = profile != BundleProfile.Share,
            IncludesAudit = profile == BundleProfile.Audit,
            IncludesEvidence = false // Reserved for future
        };
    }
    
    private static string GenerateSummaryMarkdown(ComparisonBundle bundle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Comparison Summary");
        sb.AppendLine();
        sb.AppendLine($"**Bundle ID:** `{bundle.Manifest.BundleId}`");
        sb.AppendLine($"**Created:** {bundle.Manifest.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Profile:** {bundle.Manifest.Profile}");
        sb.AppendLine($"**App Version:** {bundle.Manifest.AppVersion}");
        sb.AppendLine($"**Delta Spec:** {bundle.Manifest.DeltaSpecVersion}");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(bundle.Findings.ComparativeSummary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine(bundle.Findings.ComparativeSummary);
            sb.AppendLine();
        }
        
        sb.AppendLine("## Deltas Found");
        sb.AppendLine();
        
        var meaningfulDeltas = bundle.Findings.Deltas.Where(d => d.IsMeaningful).ToList();
        if (meaningfulDeltas.Count > 0)
        {
            sb.AppendLine($"**{meaningfulDeltas.Count} meaningful delta(s) detected:**");
            sb.AppendLine();
            
            foreach (var delta in meaningfulDeltas)
            {
                sb.AppendLine($"### {delta.Name}");
                sb.AppendLine();
                sb.AppendLine($"*{delta.Explanation}*");
                sb.AppendLine();
                sb.AppendLine($"- **Type:** {delta.DeltaType}");
                sb.AppendLine($"- **Delta:** {delta.Delta:G6} {delta.Units ?? ""}");
                sb.AppendLine($"- **Confidence:** {delta.Confidence:P0}");
                sb.AppendLine();
                if (!string.IsNullOrEmpty(delta.SummarySentence))
                {
                    sb.AppendLine($"> {delta.SummarySentence}");
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("No meaningful deltas detected.");
            sb.AppendLine();
        }
        
        sb.AppendLine("## Reproducibility");
        sb.AppendLine();
        sb.AppendLine($"- **Badge:** {bundle.Repro.ReproducibilityBadge}");
        sb.AppendLine($"- **Determinism:** {(bundle.Repro.DeterminismEnabled ? "Enabled" : "Disabled")}");
        if (bundle.Repro.DeterminismSeed.HasValue)
        {
            sb.AppendLine($"- **Seed:** {bundle.Repro.DeterminismSeed}");
        }
        sb.AppendLine($"- **Delta Spec:** {bundle.Repro.DeltaSpecVersion}");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(bundle.Notes))
        {
            sb.AppendLine("## Notes");
            sb.AppendLine();
            sb.AppendLine(bundle.Notes);
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine("*Generated by ScalarScope*");
        
        return sb.ToString();
    }
    
    private static string GenerateReadme(ComparisonBundle bundle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ScalarScope Comparison Bundle");
        sb.AppendLine();
        sb.AppendLine($"This bundle was exported from ScalarScope v{bundle.Manifest.AppVersion}.");
        sb.AppendLine();
        sb.AppendLine("## Bundle Contents");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("├── manifest.json      # Bundle metadata and version");
        sb.AppendLine("├── findings/");
        sb.AppendLine("│   ├── deltas.json    # Delta computation results");
        sb.AppendLine("│   ├── why.json       # Why explanations for each delta");
        sb.AppendLine("│   └── summary.md     # Human-readable summary");
        if (bundle.Manifest.Contents.IncludesInsights)
        {
            sb.AppendLine("├── insights/");
            sb.AppendLine("│   └── insights.json  # Teaching insights from comparison");
        }
        sb.AppendLine("├── repro/");
        sb.AppendLine("│   └── repro.json     # Reproducibility metadata");
        sb.AppendLine("├── environment/");
        sb.AppendLine("│   └── environment.json # Runtime environment info");
        if (bundle.Manifest.Contents.IncludesAssets)
        {
            sb.AppendLine("├── assets/            # Visual assets (screenshots, cards)");
        }
        if (bundle.Manifest.Contents.IncludesAudit)
        {
            sb.AppendLine("├── audit/");
            sb.AppendLine("│   └── audit.json     # Full audit bundle for reproducibility");
        }
        sb.AppendLine("├── integrity.json     # File hashes and bundle verification");
        sb.AppendLine("└── README.md          # This file");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Opening This Bundle");
        sb.AppendLine();
        sb.AppendLine("1. Open ScalarScope");
        sb.AppendLine("2. Select **File → Open Bundle…** or drag and drop this file");
        sb.AppendLine("3. The comparison will open in **Review Mode**");
        sb.AppendLine();
        sb.AppendLine("## Privacy Notice");
        sb.AppendLine();
        sb.AppendLine($"- Raw trajectory data included: **{bundle.Manifest.Privacy.IncludesRawData}**");
        sb.AppendLine($"- File paths included: **{bundle.Manifest.Privacy.IncludesFilePaths}**");
        sb.AppendLine($"- Machine name included: **{bundle.Manifest.Privacy.IncludesMachineName}**");
        sb.AppendLine();
        sb.AppendLine("## Verification");
        sb.AppendLine();
        sb.AppendLine("To verify bundle integrity, check the `integrity.json` file which contains");
        sb.AppendLine("SHA-256 hashes for all included files.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"*Bundle ID: {bundle.Manifest.BundleId}*");
        
        return sb.ToString();
    }
    
    private static async Task WriteEntryAsync(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(content);
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }
}

#region Bundle Models

/// <summary>
/// Complete comparison bundle.
/// </summary>
public record ComparisonBundle
{
    public required BundleManifest Manifest { get; init; }
    public required BundleFindings Findings { get; init; }
    public required BundleRepro Repro { get; init; }
    public required BundleEnvironment Environment { get; init; }
    public List<BundleInsight>? Insights { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Bundle manifest with version and metadata.
/// </summary>
public record BundleManifest
{
    public required string BundleId { get; init; }
    public required string BundleVersion { get; init; }
    public required BundleProfile Profile { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string AppVersion { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public required BundlePrivacyPolicy Privacy { get; init; }
    public required BundleContentsManifest Contents { get; init; }
}

/// <summary>
/// Bundle export profile.
/// </summary>
public enum BundleProfile
{
    /// <summary>Minimal: deltas + why + summary + repro.json</summary>
    Share,
    
    /// <summary>Share + key assets (screenshots, social cards)</summary>
    Review,
    
    /// <summary>Review + embedded audit bundle</summary>
    Audit
}

/// <summary>
/// Privacy policy for bundle contents.
/// </summary>
public record BundlePrivacyPolicy
{
    public bool IncludesRawData { get; init; }
    public bool IncludesFilePaths { get; init; }
    public bool IncludesMachineName { get; init; }
    public required string DataClassification { get; init; }
}

/// <summary>
/// Manifest of what's included in the bundle.
/// </summary>
public record BundleContentsManifest
{
    public bool IncludesFindings { get; init; }
    public bool IncludesRepro { get; init; }
    public bool IncludesEnvironment { get; init; }
    public bool IncludesInsights { get; init; }
    public bool IncludesAssets { get; init; }
    public bool IncludesAudit { get; init; }
    public bool IncludesEvidence { get; init; }
}

/// <summary>
/// Findings section: deltas and explanations.
/// </summary>
public record BundleFindings
{
    public required List<BundleDelta> Deltas { get; init; }
    public required List<WhyExplanation> WhyExplanations { get; init; }
    public string? ComparativeSummary { get; init; }
}

/// <summary>
/// Delta as stored in bundle.
/// </summary>
public record BundleDelta
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Explanation { get; init; }
    public string? SummarySentence { get; init; }
    public required string DeltaType { get; init; }
    public required string Status { get; init; }
    public double LeftValue { get; init; }
    public double RightValue { get; init; }
    public double Delta { get; init; }
    public double Magnitude { get; init; }
    public string? Units { get; init; }
    public double Confidence { get; init; }
    public double VisualAnchorTime { get; init; }
    public bool IsMeaningful { get; init; }
    public List<string> Notes { get; init; } = [];
}

/// <summary>
/// Why explanation for a delta.
/// </summary>
public record WhyExplanation
{
    public required string DeltaId { get; init; }
    public required string DeltaName { get; init; }
    public required string Explanation { get; init; }
    public string? SummarySentence { get; init; }
    public double Confidence { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
}

/// <summary>
/// Reproducibility section.
/// </summary>
public record BundleRepro
{
    public required string InputFingerprint { get; init; }
    public required string DeltaHash { get; init; }
    public bool DeterminismEnabled { get; init; }
    public int? DeterminismSeed { get; init; }
    public required string DeltaSpecVersion { get; init; }
    public string? PresetId { get; init; }
    public string? PresetHash { get; init; }
    public required string ReproducibilityBadge { get; init; }
    public required string AlignmentMode { get; init; }
    public int TimestepCount { get; init; }
}

/// <summary>
/// Environment section.
/// </summary>
public record BundleEnvironment
{
    public required string AppVersion { get; init; }
    public required string Platform { get; init; }
    public required string DotNetVersion { get; init; }
    public bool Is64BitProcess { get; init; }
    public int ProcessorCount { get; init; }
    public string? MachineName { get; init; }
}

/// <summary>
/// Insight as stored in bundle.
/// </summary>
public record BundleInsight
{
    public required string Id { get; init; }
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? WhyFired { get; init; }
    public string? TriggerType { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = new();
    public double? Confidence { get; init; }
    public double? AnchorTime { get; init; }
    public string? DeltaId { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Asset to include in bundle.
/// </summary>
public record BundleAsset
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public byte[]? Data { get; init; }
}

/// <summary>
/// Bundle export result.
/// </summary>
public record BundleExportResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? BundleId { get; init; }
    public string? BundleHash { get; init; }
    public int FileCount { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Bundle integrity information.
/// </summary>
public record BundleIntegrityInfo
{
    public required Dictionary<string, string> FileHashes { get; init; }
    public required string BundleHash { get; init; }
    public DateTime ComputedAt { get; init; }
}

#endregion
