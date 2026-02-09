// Phase 7.1.2: Import Preset System
// Named presets for consistent, repeatable imports.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScalarScope.Services;

/// <summary>
/// Phase 7.1: Service for managing import presets.
/// Presets store normalization, alignment, and processing defaults.
/// </summary>
public sealed class ImportPresetService
{
    private static readonly Lazy<ImportPresetService> _instance = 
        new(() => new ImportPresetService());
    
    public static ImportPresetService Instance => _instance.Value;
    
    private readonly string _presetsDirectory;
    private readonly Dictionary<string, ImportPreset> _presets = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Current preset schema version.
    /// </summary>
    public const string PresetSchemaVersion = "1.0.0";
    
    /// <summary>
    /// Event fired when presets change.
    /// </summary>
    public event EventHandler? PresetsChanged;
    
    private ImportPresetService()
    {
        _presetsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "presets");
        
        Directory.CreateDirectory(_presetsDirectory);
        LoadPresets();
        EnsureBuiltInPresets();
    }
    
    /// <summary>
    /// Get all available presets.
    /// </summary>
    public IReadOnlyList<ImportPreset> GetAll()
    {
        lock (_lock)
        {
            return _presets.Values
                .OrderBy(p => p.IsBuiltIn ? 0 : 1)
                .ThenBy(p => p.Name)
                .ToList()
                .AsReadOnly();
        }
    }
    
    /// <summary>
    /// Get a preset by ID.
    /// </summary>
    public ImportPreset? Get(string presetId)
    {
        lock (_lock)
        {
            return _presets.TryGetValue(presetId, out var preset) ? preset : null;
        }
    }
    
    /// <summary>
    /// Get the default preset.
    /// </summary>
    public ImportPreset GetDefault()
    {
        return Get("default") ?? CreateDefaultPreset();
    }
    
    /// <summary>
    /// Create a new preset.
    /// </summary>
    public ImportPreset Create(string name, string? description = null, ImportPreset? basedOn = null)
    {
        var preset = new ImportPreset
        {
            Id = GeneratePresetId(name),
            Name = name,
            Description = description ?? "",
            SchemaVersion = PresetSchemaVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = false,
            
            // Copy settings from base or use defaults
            Normalization = basedOn?.Normalization ?? new NormalizationSettings(),
            Alignment = basedOn?.Alignment ?? new AlignmentSettings(),
            Filtering = basedOn?.Filtering ?? new FilteringSettings(),
            ColumnMapping = basedOn?.ColumnMapping ?? new ColumnMappingSettings()
        };
        
        SavePreset(preset);
        
        lock (_lock)
        {
            _presets[preset.Id] = preset;
        }
        
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return preset;
    }
    
    /// <summary>
    /// Update an existing preset.
    /// </summary>
    public bool Update(ImportPreset preset)
    {
        if (preset.IsBuiltIn)
        {
            // Can't modify built-in presets, create a copy instead
            return false;
        }
        
        preset = preset with { UpdatedAt = DateTime.UtcNow };
        SavePreset(preset);
        
        lock (_lock)
        {
            _presets[preset.Id] = preset;
        }
        
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    
    /// <summary>
    /// Duplicate a preset with a new name.
    /// </summary>
    public ImportPreset Duplicate(string presetId, string newName)
    {
        var source = Get(presetId);
        if (source is null)
        {
            throw new ArgumentException($"Preset '{presetId}' not found");
        }
        
        return Create(newName, $"Copy of {source.Name}", source);
    }
    
    /// <summary>
    /// Delete a preset.
    /// </summary>
    public bool Delete(string presetId)
    {
        var preset = Get(presetId);
        if (preset is null || preset.IsBuiltIn)
        {
            return false;
        }
        
        var path = GetPresetPath(presetId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        
        lock (_lock)
        {
            _presets.Remove(presetId);
        }
        
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    
    /// <summary>
    /// Export a preset to a file.
    /// </summary>
    public async Task<string> ExportAsync(string presetId, string destinationPath)
    {
        var preset = Get(presetId);
        if (preset is null)
        {
            throw new ArgumentException($"Preset '{presetId}' not found");
        }
        
        var json = JsonSerializer.Serialize(preset, GetJsonOptions());
        await File.WriteAllTextAsync(destinationPath, json);
        return destinationPath;
    }
    
    /// <summary>
    /// Import a preset from a file.
    /// </summary>
    public async Task<ImportPreset> ImportAsync(string sourcePath)
    {
        var json = await File.ReadAllTextAsync(sourcePath);
        var preset = JsonSerializer.Deserialize<ImportPreset>(json, GetJsonOptions());
        
        if (preset is null)
        {
            throw new InvalidOperationException("Failed to parse preset file");
        }
        
        // Generate new ID to avoid conflicts
        var originalId = preset.Id;
        preset = preset with
        {
            Id = GeneratePresetId(preset.Name + "_imported"),
            IsBuiltIn = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ImportedFrom = originalId
        };
        
        SavePreset(preset);
        
        lock (_lock)
        {
            _presets[preset.Id] = preset;
        }
        
        PresetsChanged?.Invoke(this, EventArgs.Empty);
        return preset;
    }
    
    /// <summary>
    /// Rename a preset.
    /// </summary>
    public bool Rename(string presetId, string newName)
    {
        var preset = Get(presetId);
        if (preset is null || preset.IsBuiltIn)
        {
            return false;
        }
        
        var updated = preset with 
        { 
            Name = newName, 
            UpdatedAt = DateTime.UtcNow 
        };
        
        return Update(updated);
    }
    
    private void LoadPresets()
    {
        var files = Directory.GetFiles(_presetsDirectory, "*.preset.json");
        
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var preset = JsonSerializer.Deserialize<ImportPreset>(json, GetJsonOptions());
                
                if (preset is not null)
                {
                    lock (_lock)
                    {
                        _presets[preset.Id] = preset;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLoggingService.Instance.Log(
                    ErrorSeverity.Warning,
                    $"Failed to load preset {file}: {ex.Message}",
                    "ImportPresetService");
            }
        }
    }
    
    private void EnsureBuiltInPresets()
    {
        var builtIns = new[]
        {
            CreateDefaultPreset(),
            CreateTrainingRunPreset(),
            CreateEvaluationTracePreset(),
            CreateHighPrecisionPreset()
        };
        
        foreach (var preset in builtIns)
        {
            lock (_lock)
            {
                if (!_presets.ContainsKey(preset.Id))
                {
                    _presets[preset.Id] = preset;
                    SavePreset(preset);
                }
            }
        }
    }
    
    private static ImportPreset CreateDefaultPreset()
    {
        return new ImportPreset
        {
            Id = "default",
            Name = "Default",
            Description = "Standard import settings suitable for most comparisons",
            SchemaVersion = PresetSchemaVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = true,
            Normalization = new NormalizationSettings
            {
                Enabled = false,
                Method = NormalizationMethod.None,
                Precision = 10
            },
            Alignment = new AlignmentSettings
            {
                Mode = AlignmentMode.ByIndex,
                TimestepTolerance = 0.001
            },
            Filtering = new FilteringSettings
            {
                ExcludeNaN = false,
                ExcludeInf = false,
                ScalarFilter = null
            },
            ColumnMapping = new ColumnMappingSettings
            {
                TimestepColumn = "auto",
                ScalarColumns = "auto"
            }
        };
    }
    
    private static ImportPreset CreateTrainingRunPreset()
    {
        return new ImportPreset
        {
            Id = "training-run",
            Name = "Training Run Export",
            Description = "Optimized for comparing ML training runs with loss curves and metrics",
            SchemaVersion = PresetSchemaVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = true,
            Normalization = new NormalizationSettings
            {
                Enabled = true,
                Method = NormalizationMethod.ZScore,
                Precision = 6
            },
            Alignment = new AlignmentSettings
            {
                Mode = AlignmentMode.ByIndex,
                TimestepTolerance = 0.0
            },
            Filtering = new FilteringSettings
            {
                ExcludeNaN = true,
                ExcludeInf = true,
                ScalarFilter = null
            },
            ColumnMapping = new ColumnMappingSettings
            {
                TimestepColumn = "step|epoch|iteration",
                ScalarColumns = "auto"
            }
        };
    }
    
    private static ImportPreset CreateEvaluationTracePreset()
    {
        return new ImportPreset
        {
            Id = "evaluation-trace",
            Name = "Evaluation Trace",
            Description = "For comparing model evaluation results across different configurations",
            SchemaVersion = PresetSchemaVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = true,
            Normalization = new NormalizationSettings
            {
                Enabled = false,
                Method = NormalizationMethod.None,
                Precision = 10
            },
            Alignment = new AlignmentSettings
            {
                Mode = AlignmentMode.ByTimestamp,
                TimestepTolerance = 0.001
            },
            Filtering = new FilteringSettings
            {
                ExcludeNaN = false,
                ExcludeInf = false,
                ScalarFilter = null
            },
            ColumnMapping = new ColumnMappingSettings
            {
                TimestepColumn = "auto",
                ScalarColumns = "auto"
            }
        };
    }
    
    private static ImportPreset CreateHighPrecisionPreset()
    {
        return new ImportPreset
        {
            Id = "high-precision",
            Name = "High Precision",
            Description = "Maximum precision for scientific or numerical analysis",
            SchemaVersion = PresetSchemaVersion,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsBuiltIn = true,
            Normalization = new NormalizationSettings
            {
                Enabled = false,
                Method = NormalizationMethod.None,
                Precision = 15
            },
            Alignment = new AlignmentSettings
            {
                Mode = AlignmentMode.Exact,
                TimestepTolerance = 0.0
            },
            Filtering = new FilteringSettings
            {
                ExcludeNaN = false,
                ExcludeInf = false,
                ScalarFilter = null
            },
            ColumnMapping = new ColumnMappingSettings
            {
                TimestepColumn = "auto",
                ScalarColumns = "auto"
            }
        };
    }
    
    private void SavePreset(ImportPreset preset)
    {
        try
        {
            var path = GetPresetPath(preset.Id);
            var json = JsonSerializer.Serialize(preset, GetJsonOptions());
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "ImportPresetService.Save");
        }
    }
    
    private string GetPresetPath(string presetId)
    {
        return Path.Combine(_presetsDirectory, $"{presetId}.preset.json");
    }
    
    private static string GeneratePresetId(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-");
        
        // Remove non-alphanumeric except hyphens
        slug = string.Concat(slug.Where(c => char.IsLetterOrDigit(c) || c == '-'));
        
        // Add timestamp for uniqueness
        return $"{slug}-{DateTime.UtcNow.Ticks % 100000}";
    }
    
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }
}

/// <summary>
/// An import preset with all settings for a reproducible import.
/// </summary>
public record ImportPreset
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public required string SchemaVersion { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsBuiltIn { get; init; }
    public string? ImportedFrom { get; init; }
    
    public required NormalizationSettings Normalization { get; init; }
    public required AlignmentSettings Alignment { get; init; }
    public required FilteringSettings Filtering { get; init; }
    public required ColumnMappingSettings ColumnMapping { get; init; }
    
    /// <summary>
    /// Get a fingerprint of this preset's settings for reproducibility.
    /// </summary>
    public string GetSettingsFingerprint()
    {
        var data = $"{Normalization.Enabled}|{Normalization.Method}|{Normalization.Precision}|" +
                   $"{Alignment.Mode}|{Alignment.TimestepTolerance}|" +
                   $"{Filtering.ExcludeNaN}|{Filtering.ExcludeInf}|{Filtering.ScalarFilter ?? ""}|" +
                   $"{ColumnMapping.TimestepColumn}|{ColumnMapping.ScalarColumns}";
        
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}

/// <summary>
/// Normalization settings for import.
/// </summary>
public record NormalizationSettings
{
    public bool Enabled { get; init; }
    public NormalizationMethod Method { get; init; }
    public int Precision { get; init; } = 10;
}

/// <summary>
/// Normalization methods.
/// </summary>
public enum NormalizationMethod
{
    None,
    MinMax,
    ZScore,
    UnitVector
}

/// <summary>
/// Alignment settings for comparing trajectories.
/// </summary>
public record AlignmentSettings
{
    public AlignmentMode Mode { get; init; }
    public double TimestepTolerance { get; init; }
}

/// <summary>
/// Trajectory alignment modes.
/// </summary>
public enum AlignmentMode
{
    ByIndex,
    ByTimestamp,
    Exact
}

/// <summary>
/// Filtering settings for import.
/// </summary>
public record FilteringSettings
{
    public bool ExcludeNaN { get; init; }
    public bool ExcludeInf { get; init; }
    public string? ScalarFilter { get; init; }
}

/// <summary>
/// Column mapping settings for CSV imports.
/// </summary>
public record ColumnMappingSettings
{
    public string TimestepColumn { get; init; } = "auto";
    public string ScalarColumns { get; init; } = "auto";
}
