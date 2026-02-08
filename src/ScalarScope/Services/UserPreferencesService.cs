using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Manages user preferences and dismissed hints.
/// Stores settings in local app data.
/// </summary>
public static class UserPreferencesService
{
    private static readonly string PreferencesPath = Path.Combine(
        FileSystem.AppDataDirectory, "preferences.json");

    private static UserPreferences? _cached;

    /// <summary>
    /// Check if a specific hint has been dismissed.
    /// </summary>
    public static bool IsHintDismissed(string hintId)
    {
        var prefs = Load();
        return prefs.DismissedHints.Contains(hintId);
    }

    /// <summary>
    /// Dismiss a hint so it won't show again.
    /// </summary>
    public static void DismissHint(string hintId)
    {
        var prefs = Load();
        if (!prefs.DismissedHints.Contains(hintId))
        {
            prefs.DismissedHints.Add(hintId);
            Save(prefs);
        }
    }

    /// <summary>
    /// Reset all dismissed hints (for testing or user request).
    /// </summary>
    public static void ResetHints()
    {
        var prefs = Load();
        prefs.DismissedHints.Clear();
        Save(prefs);
    }

    /// <summary>
    /// Check if this is the user's first run (never seen the demo).
    /// </summary>
    public static bool IsFirstRun
    {
        get
        {
            var prefs = Load();
            return !prefs.HasSeenDemo;
        }
    }

    /// <summary>
    /// Check if the user has completed the demo (watched to completion).
    /// </summary>
    public static bool HasCompletedDemo
    {
        get
        {
            var prefs = Load();
            return prefs.DemoCompletedAt.HasValue;
        }
    }

    /// <summary>
    /// Check if the user skipped the demo.
    /// </summary>
    public static bool HasSkippedDemo
    {
        get
        {
            var prefs = Load();
            return prefs.DemoSkippedAt.HasValue;
        }
    }

    /// <summary>
    /// Mark that the user has seen (started) the demo.
    /// </summary>
    public static void MarkDemoSeen()
    {
        var prefs = Load();
        if (!prefs.HasSeenDemo)
        {
            prefs.HasSeenDemo = true;
            prefs.DemoSeenAt = DateTime.UtcNow;
            Save(prefs);
        }
    }

    /// <summary>
    /// Mark that the user completed the demo.
    /// </summary>
    public static void MarkDemoCompleted()
    {
        var prefs = Load();
        prefs.HasSeenDemo = true;
        prefs.DemoCompletedAt = DateTime.UtcNow;
        Save(prefs);
    }

    /// <summary>
    /// Mark that the user skipped the demo (chose not to see it).
    /// </summary>
    public static void MarkDemoSkipped()
    {
        var prefs = Load();
        prefs.HasSeenDemo = true;
        prefs.DemoSkippedAt = DateTime.UtcNow;
        Save(prefs);
    }

    /// <summary>
    /// Reset first-run state (for development/testing only).
    /// </summary>
    public static void ResetFirstRunState()
    {
        var prefs = Load();
        prefs.HasSeenDemo = false;
        prefs.DemoSeenAt = null;
        prefs.DemoCompletedAt = null;
        prefs.DemoSkippedAt = null;
        Save(prefs);
    }

    /// <summary>
    /// Mark first run as complete (legacy compatibility).
    /// </summary>
    public static void MarkFirstRunComplete()
    {
        MarkDemoSeen();
    }

    /// <summary>
    /// Get the annotation density level.
    /// </summary>
    public static AnnotationDensity GetAnnotationDensity()
    {
        var prefs = Load();
        return prefs.AnnotationDensity;
    }

    /// <summary>
    /// Set the annotation density level.
    /// </summary>
    public static void SetAnnotationDensity(AnnotationDensity density)
    {
        var prefs = Load();
        prefs.AnnotationDensity = density;
        Save(prefs);
    }

    // --- Theme Settings ---

    public static AppTheme GetTheme()
    {
        var prefs = Load();
        return prefs.Theme;
    }

    public static void SetTheme(AppTheme theme)
    {
        var prefs = Load();
        prefs.Theme = theme;
        Save(prefs);
    }

    public static bool GetHighContrastMode()
    {
        var prefs = Load();
        return prefs.HighContrastMode;
    }

    public static void SetHighContrastMode(bool enabled)
    {
        var prefs = Load();
        prefs.HighContrastMode = enabled;
        Save(prefs);
    }

    public static bool GetReduceAnimations()
    {
        var prefs = Load();
        return prefs.ReduceAnimations;
    }

    public static void SetReduceAnimations(bool reduce)
    {
        var prefs = Load();
        prefs.ReduceAnimations = reduce;
        Save(prefs);
    }

    // --- Playback Settings ---

    public static float GetDefaultPlaybackSpeed()
    {
        var prefs = Load();
        return prefs.DefaultPlaybackSpeed;
    }

    public static void SetDefaultPlaybackSpeed(float speed)
    {
        var prefs = Load();
        prefs.DefaultPlaybackSpeed = speed;
        Save(prefs);
    }

    public static bool GetAutoPlayOnLoad()
    {
        var prefs = Load();
        return prefs.AutoPlayOnLoad;
    }

    public static void SetAutoPlayOnLoad(bool autoPlay)
    {
        var prefs = Load();
        prefs.AutoPlayOnLoad = autoPlay;
        Save(prefs);
    }

    // --- Session Settings ---

    public static bool GetAutoLoadLastSession()
    {
        var prefs = Load();
        return prefs.AutoLoadLastSession;
    }

    public static void SetAutoLoadLastSession(bool autoLoad)
    {
        var prefs = Load();
        prefs.AutoLoadLastSession = autoLoad;
        Save(prefs);
    }

    public static int GetRecentFilesLimit()
    {
        var prefs = Load();
        return prefs.RecentFilesLimit;
    }

    public static void SetRecentFilesLimit(int limit)
    {
        var prefs = Load();
        prefs.RecentFilesLimit = limit;
        // Trim existing list if needed
        if (prefs.RecentFiles.Count > limit)
        {
            prefs.RecentFiles = prefs.RecentFiles.Take(limit).ToList();
        }
        Save(prefs);
    }

    // --- Export Settings ---

    public static string GetDefaultExportPath()
    {
        var prefs = Load();
        return prefs.DefaultExportPath;
    }

    public static void SetDefaultExportPath(string path)
    {
        var prefs = Load();
        prefs.DefaultExportPath = path;
        Save(prefs);
    }

    public static (int width, int height) GetDefaultExportResolution()
    {
        var prefs = Load();
        return (prefs.DefaultExportWidth, prefs.DefaultExportHeight);
    }

    public static void SetDefaultExportResolution(int width, int height)
    {
        var prefs = Load();
        prefs.DefaultExportWidth = width;
        prefs.DefaultExportHeight = height;
        Save(prefs);
    }

    // --- Reset ---

    public static void ResetAllSettings()
    {
        _cached = new UserPreferences();
        Save(_cached);
    }

    /// <summary>
    /// Get the list of recently opened files.
    /// </summary>
    public static IReadOnlyList<RecentFileEntry> GetRecentFiles()
    {
        var prefs = Load();
        // Filter out files that no longer exist
        var validFiles = prefs.RecentFiles
            .Where(f => File.Exists(f.Path))
            .ToList();
        
        // Update stored list if some files were removed
        if (validFiles.Count != prefs.RecentFiles.Count)
        {
            prefs.RecentFiles = validFiles;
            Save(prefs);
        }
        
        return validFiles.AsReadOnly();
    }

    /// <summary>
    /// Add a file to the recent files list.
    /// </summary>
    public static void AddRecentFile(string path, string? runName = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        var prefs = Load();
        
        // Remove existing entry for this path (if any)
        prefs.RecentFiles.RemoveAll(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase));
        
        // Add to the front of the list
        prefs.RecentFiles.Insert(0, new RecentFileEntry
        {
            Path = path,
            Name = runName ?? Path.GetFileNameWithoutExtension(path),
            LastOpened = DateTime.UtcNow
        });
        
        // Keep only up to the configured limit
        if (prefs.RecentFiles.Count > prefs.RecentFilesLimit)
        {
            prefs.RecentFiles = prefs.RecentFiles.Take(prefs.RecentFilesLimit).ToList();
        }
        
        Save(prefs);
    }

    /// <summary>
    /// Clear the recent files list.
    /// </summary>
    public static void ClearRecentFiles()
    {
        var prefs = Load();
        prefs.RecentFiles.Clear();
        Save(prefs);
    }

    private static UserPreferences Load()
    {
        if (_cached != null)
            return _cached;

        try
        {
            if (File.Exists(PreferencesPath))
            {
                var json = File.ReadAllText(PreferencesPath);
                _cached = JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
            }
            else
            {
                _cached = new UserPreferences();
            }
        }
        catch
        {
            _cached = new UserPreferences();
        }

        return _cached;
    }

    private static void Save(UserPreferences prefs)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PreferencesPath, json);
            _cached = prefs;
        }
        catch
        {
            // Silently fail - preferences are not critical
        }
    }
}

public class UserPreferences
{
    public HashSet<string> DismissedHints { get; set; } = [];

    // First-run / demo state
    public bool HasSeenDemo { get; set; }
    public DateTime? DemoSeenAt { get; set; }
    public DateTime? DemoCompletedAt { get; set; }
    public DateTime? DemoSkippedAt { get; set; }

    // Recent files
    public List<RecentFileEntry> RecentFiles { get; set; } = [];
    public int RecentFilesLimit { get; set; } = 10;

    // Legacy field (kept for backwards compatibility)
    public bool HasCompletedFirstRun
    {
        get => HasSeenDemo;
        set => HasSeenDemo = value;
    }

    public AnnotationDensity AnnotationDensity { get; set; } = AnnotationDensity.Standard;

    // Theme settings
    public AppTheme Theme { get; set; } = AppTheme.Unspecified;
    public bool HighContrastMode { get; set; }
    public bool ReduceAnimations { get; set; }

    // Playback settings
    public float DefaultPlaybackSpeed { get; set; } = 1f;
    public bool AutoPlayOnLoad { get; set; }

    // Session settings
    public bool AutoLoadLastSession { get; set; }

    // Export settings
    public string DefaultExportPath { get; set; } = "";
    public int DefaultExportWidth { get; set; } = 1920;
    public int DefaultExportHeight { get; set; } = 1080;
}

public enum AnnotationDensity
{
    Minimal,
    Standard,
    Full
}

public class RecentFileEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime LastOpened { get; set; }
}
