// ComparisonBundle v1.0.0 Exporter
// Writes bundle ZIP file with integrity verification.

using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ScalarScope.Services.Bundles;

/// <summary>
/// Exports ComparisonBundle v1.0.0 to ZIP file.
/// </summary>
public sealed class BundleExporter
{
    private static readonly Lazy<BundleExporter> _instance = new(() => new BundleExporter());
    public static BundleExporter Instance => _instance.Value;
    
    private readonly string _bundleDirectory;
    
    private BundleExporter()
    {
        _bundleDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "bundles");
        Directory.CreateDirectory(_bundleDirectory);
    }
    
    /// <summary>
    /// Export bundle payload to ZIP file.
    /// </summary>
    public async Task<BundleExportResult> ExportAsync(
        ComparisonBundlePayload payload,
        string? outputPath = null)
    {
        var bundleId = payload.Manifest.BundleId;
        var timestamp = payload.Manifest.CreatedUtc.ToString("yyyyMMdd-HHmmss");
        var fileName = $"comparison-{bundleId.ToString("N")[..8]}-{timestamp}{BundleBuilder.BundleExtension}";
        var path = outputPath ?? Path.Combine(_bundleDirectory, fileName);
        
        try
        {
            // Collect all file entries (for integrity computation)
            var fileEntries = new List<FileIntegrityEntry>();
            var zipEntries = new Dictionary<string, byte[]>();
            
            // 1. Serialize payloads to bytes
            var options = BundleHashAlgorithm.GetReadableOptions();
            
            // repro/repro.json
            var reproBytes = JsonSerializer.SerializeToUtf8Bytes(payload.Repro, options);
            zipEntries["repro/repro.json"] = reproBytes;
            fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("repro/repro.json", reproBytes, "application/json"));
            
            // findings/deltas.json
            var deltasBytes = JsonSerializer.SerializeToUtf8Bytes(payload.Deltas, options);
            zipEntries["findings/deltas.json"] = deltasBytes;
            fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("findings/deltas.json", deltasBytes, "application/json"));
            
            // findings/why.json
            var whyBytes = JsonSerializer.SerializeToUtf8Bytes(payload.Why, options);
            zipEntries["findings/why.json"] = whyBytes;
            fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("findings/why.json", whyBytes, "application/json"));
            
            // findings/summary.md
            var summaryBytes = Encoding.UTF8.GetBytes(payload.SummaryMarkdown);
            zipEntries["findings/summary.md"] = summaryBytes;
            fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("findings/summary.md", summaryBytes, "text/markdown"));
            
            // insights/insights.json (optional)
            if (payload.Insights != null)
            {
                var insightsBytes = JsonSerializer.SerializeToUtf8Bytes(payload.Insights, options);
                zipEntries["insights/insights.json"] = insightsBytes;
                fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("insights/insights.json", insightsBytes, "application/json"));
            }
            
            // assets/ (optional)
            if (payload.Assets != null)
            {
                foreach (var asset in payload.Assets)
                {
                    var assetPath = $"assets/{asset.FileName}";
                    zipEntries[assetPath] = asset.Data;
                    fileEntries.Add(BundleHashAlgorithm.CreateFileEntry(assetPath, asset.Data, asset.ContentType));
                }
            }
            
            // audit/audit.json (optional)
            if (payload.AuditPayload != null)
            {
                var auditBytes = JsonSerializer.SerializeToUtf8Bytes(payload.AuditPayload, options);
                zipEntries["audit/audit.json"] = auditBytes;
                fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("audit/audit.json", auditBytes, "application/json"));
            }
            
            // README.md
            var readme = GenerateReadme(payload.Manifest);
            var readmeBytes = Encoding.UTF8.GetBytes(readme);
            zipEntries["README.md"] = readmeBytes;
            fileEntries.Add(BundleHashAlgorithm.CreateFileEntry("README.md", readmeBytes, "text/markdown"));
            
            // 2. Build manifest core (without integrity) for hashing
            var manifestCore = payload.Manifest with
            {
                Integrity = payload.Manifest.Integrity with
                {
                    Files = Array.Empty<FileIntegrityEntry>(),
                    BundleHash = ""
                }
            };
            
            // 3. Compute bundleHash
            var bundleHash = BundleHashAlgorithm.ComputeBundleHash(manifestCore, fileEntries);
            
            // 4. Build final manifest with integrity
            var finalManifest = payload.Manifest with
            {
                Integrity = new IntegrityInfo
                {
                    HashAlgorithm = HashAlgorithm.Sha256,
                    Files = fileEntries.OrderBy(f => f.Path, StringComparer.Ordinal).ToList(),
                    BundleHash = bundleHash,
                    BundleHashDefinition = BundleHashAlgorithm.BundleHashDefinition
                }
            };
            
            // 5. Serialize manifest
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(finalManifest, options);
            zipEntries["manifest.json"] = manifestBytes;
            
            // 6. Write ZIP file
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);
            
            foreach (var (entryPath, data) in zipEntries.OrderBy(kv => kv.Key))
            {
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(data);
            }
            
            return new BundleExportResult
            {
                Success = true,
                FilePath = path,
                BundleId = bundleId.ToString(),
                BundleHash = bundleHash,
                FileCount = zipEntries.Count
            };
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "BundleExport");
            return new BundleExportResult
            {
                Success = false,
                ErrorMessage = $"Export failed: {ex.Message}",
                BundleId = bundleId.ToString()
            };
        }
    }
    
    /// <summary>
    /// Get all exported bundles.
    /// </summary>
    public IReadOnlyList<ComparisonBundleManifest> GetExportedBundles()
    {
        var bundles = new List<ComparisonBundleManifest>();
        var pattern = $"*{BundleBuilder.BundleExtension}";
        
        foreach (var file in Directory.GetFiles(_bundleDirectory, pattern))
        {
            try
            {
                using var archive = ZipFile.OpenRead(file);
                var entry = archive.GetEntry("manifest.json");
                if (entry == null) continue;
                
                using var stream = entry.Open();
                var manifest = JsonSerializer.Deserialize<ComparisonBundleManifest>(
                    stream, BundleHashAlgorithm.GetCanonicalOptions());
                
                if (manifest != null)
                {
                    bundles.Add(manifest);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return bundles.OrderByDescending(b => b.CreatedUtc).ToList();
    }
    
    private static string GenerateReadme(ComparisonBundleManifest manifest)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("# ScalarScope Comparison Bundle");
        sb.AppendLine();
        sb.AppendLine($"This bundle was exported from **ScalarScope v{manifest.App.AppVersion}**.");
        sb.AppendLine();
        sb.AppendLine("## Bundle Info");
        sb.AppendLine();
        sb.AppendLine($"- **Bundle ID:** `{manifest.BundleId}`");
        sb.AppendLine($"- **Profile:** {manifest.Profile}");
        sb.AppendLine($"- **Created:** {manifest.CreatedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"- **Bundle Version:** {manifest.BundleVersion}");
        sb.AppendLine($"- **Delta Spec:** {manifest.App.DeltaSpecVersion}");
        sb.AppendLine();
        
        sb.AppendLine("## Comparison");
        sb.AppendLine();
        sb.AppendLine($"- **Path A:** {manifest.Comparison.LabelA}");
        sb.AppendLine($"- **Path B:** {manifest.Comparison.LabelB}");
        sb.AppendLine($"- **Alignment:** {manifest.Comparison.AlignmentMode}");
        sb.AppendLine($"- **Length:** {manifest.Comparison.CompareLength} timesteps");
        sb.AppendLine();
        
        sb.AppendLine("## Bundle Contents");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine("├── manifest.json          # Bundle manifest and integrity");
        sb.AppendLine("├── repro/");
        sb.AppendLine("│   └── repro.json         # Reproducibility info");
        sb.AppendLine("├── findings/");
        sb.AppendLine("│   ├── deltas.json        # Delta results");
        sb.AppendLine("│   ├── why.json           # Why explanations");
        sb.AppendLine("│   └── summary.md         # Human-readable summary");
        
        if (manifest.Contents.Optional.Any(o => o.StartsWith("insights/")))
        {
            sb.AppendLine("├── insights/");
            sb.AppendLine("│   └── insights.json      # Teaching insights");
        }
        
        if (manifest.Contents.Optional.Any(o => o.StartsWith("assets/")))
        {
            sb.AppendLine("├── assets/                # Visual assets");
        }
        
        if (manifest.Contents.Optional.Any(o => o.StartsWith("audit/")))
        {
            sb.AppendLine("├── audit/");
            sb.AppendLine("│   └── audit.json         # Full audit bundle");
        }
        
        sb.AppendLine("└── README.md              # This file");
        sb.AppendLine("```");
        sb.AppendLine();
        
        sb.AppendLine("## Opening This Bundle");
        sb.AppendLine();
        sb.AppendLine("1. Open **ScalarScope**");
        sb.AppendLine("2. Go to **Compare Paths** tab");
        sb.AppendLine("3. Click **📦 Open Bundle...**");
        sb.AppendLine("4. Select this `.scbundle` file");
        sb.AppendLine();
        sb.AppendLine("The comparison will open in **Review Mode** (read-only).");
        sb.AppendLine();
        
        sb.AppendLine("## Verification");
        sb.AppendLine();
        sb.AppendLine($"Bundle hash: `{manifest.Integrity.BundleHash}`");
        sb.AppendLine();
        sb.AppendLine("All file hashes are listed in `manifest.json` → `integrity.files`.");
        sb.AppendLine();
        
        sb.AppendLine("## Privacy");
        sb.AppendLine();
        sb.AppendLine($"- Contains raw run data: **{manifest.Privacy.ContainsRawRunData}**");
        sb.AppendLine($"- Contains PII: **{manifest.Privacy.ContainsPII}**");
        sb.AppendLine($"- Redactions: {string.Join(", ", manifest.Privacy.Redactions)}");
        sb.AppendLine();
        
        sb.AppendLine("---");
        sb.AppendLine($"*Bundle ID: {manifest.BundleId}*");
        
        return sb.ToString();
    }
}

/// <summary>
/// Result of bundle export operation.
/// </summary>
public sealed record BundleExportResult
{
    public bool Success { get; init; }
    public string? FilePath { get; init; }
    public string? BundleId { get; init; }
    public string? BundleHash { get; init; }
    public int FileCount { get; init; }
    public string? ErrorMessage { get; init; }
}
