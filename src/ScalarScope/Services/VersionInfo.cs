using System.Reflection;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.5: Provides application version information for watermarks and exports.
/// </summary>
public static class VersionInfo
{
    private static readonly Lazy<string> _version = new(LoadVersion);
    private static readonly Lazy<string> _fullVersion = new(LoadFullVersion);
    
    /// <summary>
    /// Application display version (e.g., "1.3.0").
    /// </summary>
    public static string Version => _version.Value;
    
    /// <summary>
    /// Full version string including build metadata (e.g., "1.3.0+b55ca8c").
    /// </summary>
    public static string FullVersion => _fullVersion.Value;
    
    /// <summary>
    /// Short git commit hash if available.
    /// </summary>
    public static string GitCommitShort { get; } = GetGitCommitShort();
    
    /// <summary>
    /// Build date/time.
    /// </summary>
    public static DateTime BuildDate { get; } = GetBuildDate();
    
    /// <summary>
    /// Application name.
    /// </summary>
    public const string AppName = "ScalarScope";
    
    /// <summary>
    /// Organization name.
    /// </summary>
    public const string Organization = "mcp-tool-shop-org";
    
    /// <summary>
    /// Get version watermark text suitable for exports.
    /// </summary>
    public static string GetWatermarkText(bool includeCommit = false)
    {
        if (includeCommit && !string.IsNullOrEmpty(GitCommitShort))
        {
            return $"{AppName} v{Version}+{GitCommitShort}";
        }
        return $"{AppName} v{Version}";
    }
    
    /// <summary>
    /// Get footer text for exports with date.
    /// </summary>
    public static string GetExportFooter()
    {
        return $"{AppName} v{Version} • {DateTime.Now:yyyy-MM-dd}";
    }
    
    /// <summary>
    /// Get metadata dictionary for inclusion in exports.
    /// </summary>
    public static Dictionary<string, string> GetExportMetadata()
    {
        return new Dictionary<string, string>
        {
            ["Generator"] = AppName,
            ["GeneratorVersion"] = Version,
            ["GeneratorFullVersion"] = FullVersion,
            ["ExportDate"] = DateTime.Now.ToString("O"),
            ["GitCommit"] = GitCommitShort
        };
    }
    
    private static string LoadVersion()
    {
        // Try to get from assembly attributes first
        var assembly = typeof(VersionInfo).Assembly;
        
        // Check for informational version (e.g., "1.3.0+abc1234")
        var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoAttr != null)
        {
            var version = infoAttr.InformationalVersion;
            // Strip build metadata for display version
            var plusIndex = version.IndexOf('+');
            if (plusIndex > 0)
                return version[..plusIndex];
            return version;
        }
        
        // Fall back to assembly version
        var ver = assembly.GetName().Version;
        return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.0.0";
    }
    
    private static string LoadFullVersion()
    {
        var assembly = typeof(VersionInfo).Assembly;
        var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        
        if (infoAttr != null)
            return infoAttr.InformationalVersion;
            
        return Version;
    }
    
    private static string GetGitCommitShort()
    {
        // Try to extract from informational version (e.g., "1.3.0+abc1234")
        var assembly = typeof(VersionInfo).Assembly;
        var infoAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        
        if (infoAttr != null)
        {
            var version = infoAttr.InformationalVersion;
            var plusIndex = version.IndexOf('+');
            if (plusIndex > 0 && version.Length > plusIndex + 1)
            {
                return version[(plusIndex + 1)..];
            }
        }
        
        // Could also try to load from a build-time generated file
        return string.Empty;
    }
    
    private static DateTime GetBuildDate()
    {
        // Use linker timestamp from PE header (approximate)
        try
        {
            var assembly = typeof(VersionInfo).Assembly;
            var location = assembly.Location;
            
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return File.GetLastWriteTime(location);
            }
        }
        catch
        {
            // Ignore any reflection/file errors
        }
        
        return DateTime.Now;
    }
}
