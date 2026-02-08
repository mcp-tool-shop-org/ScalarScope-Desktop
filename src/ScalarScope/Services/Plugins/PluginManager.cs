using System.Reflection;
using SkiaSharp;

namespace ScalarScope.Services.Plugins;

/// <summary>
/// Plugin manager for loading and managing extensions.
/// Phase 5.2 - Plugin Architecture
/// </summary>
public class PluginManager : IDisposable
{
    private static PluginManager? _instance;
    public static PluginManager Instance => _instance ??= new PluginManager();

    private readonly string _pluginsPath;
    private readonly List<IScalarScopePlugin> _loadedPlugins = [];
    private readonly List<IAnalyzerPlugin> _analyzers = [];
    private readonly List<IThemePlugin> _themes = [];
    private readonly List<IExportPlugin> _exporters = [];
    private readonly List<IRenderPlugin> _renderers = [];

    public IReadOnlyList<IAnalyzerPlugin> Analyzers => _analyzers.AsReadOnly();
    public IReadOnlyList<IThemePlugin> Themes => _themes.AsReadOnly();
    public IReadOnlyList<IExportPlugin> Exporters => _exporters.AsReadOnly();
    public IReadOnlyList<IRenderPlugin> Renderers => _renderers.AsReadOnly();

    public event Action<IScalarScopePlugin>? PluginLoaded;
    public event Action<IScalarScopePlugin>? PluginUnloaded;
    public event Action<string>? PluginError;

    public PluginManager()
    {
        _pluginsPath = Path.Combine(FileSystem.AppDataDirectory, "plugins");
        Directory.CreateDirectory(_pluginsPath);
    }

    /// <summary>
    /// Load all plugins from the plugins directory.
    /// </summary>
    public async Task LoadAllPluginsAsync()
    {
        var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories);

        foreach (var dll in dllFiles)
        {
            try
            {
                await LoadPluginAsync(dll);
            }
            catch (Exception ex)
            {
                PluginError?.Invoke($"Failed to load {Path.GetFileName(dll)}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Load a specific plugin assembly.
    /// </summary>
    public async Task<IReadOnlyList<IScalarScopePlugin>> LoadPluginAsync(string dllPath)
    {
        var loaded = new List<IScalarScopePlugin>();

        await Task.Run(() =>
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IScalarScopePlugin).IsAssignableFrom(t));

            foreach (var type in pluginTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is not IScalarScopePlugin plugin)
                        continue;

                    RegisterPlugin(plugin);
                    loaded.Add(plugin);
                }
                catch (Exception ex)
                {
                    PluginError?.Invoke($"Failed to instantiate {type.Name}: {ex.Message}");
                }
            }
        });

        return loaded;
    }

    /// <summary>
    /// Register a plugin instance.
    /// </summary>
    public void RegisterPlugin(IScalarScopePlugin plugin)
    {
        _loadedPlugins.Add(plugin);

        switch (plugin)
        {
            case IAnalyzerPlugin analyzer:
                _analyzers.Add(analyzer);
                break;
            case IThemePlugin theme:
                _themes.Add(theme);
                break;
            case IExportPlugin exporter:
                _exporters.Add(exporter);
                break;
            case IRenderPlugin renderer:
                _renderers.Add(renderer);
                break;
        }

        plugin.Initialize();
        PluginLoaded?.Invoke(plugin);
    }

    /// <summary>
    /// Unload a plugin.
    /// </summary>
    public void UnloadPlugin(IScalarScopePlugin plugin)
    {
        plugin.Shutdown();
        _loadedPlugins.Remove(plugin);

        switch (plugin)
        {
            case IAnalyzerPlugin analyzer:
                _analyzers.Remove(analyzer);
                break;
            case IThemePlugin theme:
                _themes.Remove(theme);
                break;
            case IExportPlugin exporter:
                _exporters.Remove(exporter);
                break;
            case IRenderPlugin renderer:
                _renderers.Remove(renderer);
                break;
        }

        PluginUnloaded?.Invoke(plugin);
    }

    /// <summary>
    /// Get plugin info for all loaded plugins.
    /// </summary>
    public IEnumerable<PluginInfo> GetPluginInfos()
    {
        return _loadedPlugins.Select(p => new PluginInfo
        {
            Id = p.Id,
            Name = p.Name,
            Version = p.Version,
            Author = p.Author,
            Description = p.Description,
            Type = p switch
            {
                IAnalyzerPlugin => PluginType.Analyzer,
                IThemePlugin => PluginType.Theme,
                IExportPlugin => PluginType.Export,
                IRenderPlugin => PluginType.Render,
                _ => PluginType.Unknown
            }
        });
    }

    public void Dispose()
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                plugin.Shutdown();
            }
            catch
            {
                // Ignore shutdown errors
            }
        }

        _loadedPlugins.Clear();
        _analyzers.Clear();
        _themes.Clear();
        _exporters.Clear();
        _renderers.Clear();
        _instance = null;

        GC.SuppressFinalize(this);
    }
}

#region Plugin Interfaces

/// <summary>
/// Base interface for all ScalarScope plugins.
/// </summary>
public interface IScalarScopePlugin
{
    string Id { get; }
    string Name { get; }
    string Version { get; }
    string Author { get; }
    string Description { get; }

    void Initialize();
    void Shutdown();
}

/// <summary>
/// Plugin for custom analyzers/metrics.
/// </summary>
public interface IAnalyzerPlugin : IScalarScopePlugin
{
    /// <summary>
    /// Analyze the trajectory and return computed metrics.
    /// </summary>
    AnalyzerResult Analyze(TrajectoryData data);

    /// <summary>
    /// Get the settings UI for this analyzer.
    /// </summary>
    IReadOnlyList<AnalyzerSetting> GetSettings();
}

/// <summary>
/// Plugin for custom themes/color schemes.
/// </summary>
public interface IThemePlugin : IScalarScopePlugin
{
    /// <summary>
    /// Get the color palette for this theme.
    /// </summary>
    ThemeColors GetColors();

    /// <summary>
    /// Preview image/thumbnail.
    /// </summary>
    byte[]? GetPreviewImage();
}

/// <summary>
/// Plugin for additional export formats.
/// </summary>
public interface IExportPlugin : IScalarScopePlugin
{
    /// <summary>
    /// File extension for this exporter (e.g., ".svg").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// MIME type for this format.
    /// </summary>
    string MimeType { get; }

    /// <summary>
    /// Export the trajectory to this format.
    /// </summary>
    Task<byte[]> ExportAsync(TrajectoryData data, ExportSettings settings);
}

/// <summary>
/// Plugin for alternative rendering backends.
/// </summary>
public interface IRenderPlugin : IScalarScopePlugin
{
    /// <summary>
    /// Render the trajectory to a bitmap.
    /// </summary>
    SKBitmap Render(TrajectoryData data, RenderSettings settings);

    /// <summary>
    /// Check if this renderer is available on the current system.
    /// </summary>
    bool IsAvailable();
}

#endregion

#region Data Models

public class PluginInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public PluginType Type { get; set; }
}

public enum PluginType
{
    Unknown,
    Analyzer,
    Theme,
    Export,
    Render
}

public class AnalyzerResult
{
    public string AnalyzerId { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, object> Metrics { get; set; } = [];
    public List<AnalyzerAlert> Alerts { get; set; } = [];
    public List<TimeSeriesData> TimeSeries { get; set; } = [];
}

public class AnalyzerAlert
{
    public int? TimestepIndex { get; set; }
    public string Message { get; set; } = "";
    public AlertSeverity Severity { get; set; }
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error
}

public class TimeSeriesData
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public List<(int Index, double Value)> Values { get; set; } = [];
}

public class AnalyzerSetting
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public SettingType Type { get; set; }
    public object? DefaultValue { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public List<object>? Options { get; set; }
}

public enum SettingType
{
    Boolean,
    Integer,
    Float,
    String,
    Choice
}

public class ThemeColors
{
    public SKColor Background { get; set; }
    public SKColor Surface { get; set; }
    public SKColor Primary { get; set; }
    public SKColor Secondary { get; set; }
    public SKColor Accent { get; set; }
    public SKColor Text { get; set; }
    public SKColor TextSecondary { get; set; }
    public SKColor Success { get; set; }
    public SKColor Warning { get; set; }
    public SKColor Error { get; set; }

    // Trajectory-specific
    public SKColor TrajectoryStart { get; set; }
    public SKColor TrajectoryEnd { get; set; }
    public SKColor HighVelocity { get; set; }
    public SKColor LowVelocity { get; set; }
    public SKColor HighCurvature { get; set; }
}

public class TrajectoryData
{
    public int PointCount { get; set; }
    public List<(float X, float Y)> Points { get; set; } = [];
    public List<float> Velocities { get; set; } = [];
    public List<float> Curvatures { get; set; } = [];
    public List<double[]> EigenValues { get; set; } = [];
    public Dictionary<string, object> Metadata { get; set; } = [];
}

public class ExportSettings
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public bool IncludeAnnotations { get; set; } = true;
    public bool IncludeTimeline { get; set; }
    public bool TransparentBackground { get; set; }
    public Dictionary<string, object> CustomSettings { get; set; } = [];
}

public class RenderSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public float Zoom { get; set; } = 1f;
    public float PanX { get; set; }
    public float PanY { get; set; }
    public bool ShowVelocity { get; set; } = true;
    public bool ShowCurvature { get; set; } = true;
    public bool AntiAlias { get; set; } = true;
}

#endregion
