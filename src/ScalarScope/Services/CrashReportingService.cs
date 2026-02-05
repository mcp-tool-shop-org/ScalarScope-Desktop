using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Service for crash reporting and support bundle generation.
/// </summary>
public class CrashReportingService
{
    private static readonly string AppDataPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ScalarScope");

    private static readonly string CrashLogPath = Path.Combine(AppDataPath, "crash.json");
    private static readonly string SessionStatePath = Path.Combine(AppDataPath, "session_state.json");
    private static readonly string LogsPath = Path.Combine(AppDataPath, "logs");

    private static string AppVersion => Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "1.0.0";

    /// <summary>
    /// Initialize crash reporting on app startup.
    /// </summary>
    public static void Initialize()
    {
        Directory.CreateDirectory(AppDataPath);
        Directory.CreateDirectory(LogsPath);

        // Set up unhandled exception handlers
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Check if we recovered from a crash on last run.
    /// </summary>
    public static bool DidRecoverFromCrash()
    {
        return File.Exists(CrashLogPath);
    }

    /// <summary>
    /// Get the last crash info.
    /// </summary>
    public static CrashInfo? GetLastCrashInfo()
    {
        if (!File.Exists(CrashLogPath)) return null;

        try
        {
            var json = File.ReadAllText(CrashLogPath);
            return JsonSerializer.Deserialize<CrashInfo>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read crash info: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clear the crash marker after user acknowledges.
    /// </summary>
    public static void AcknowledgeCrash()
    {
        if (File.Exists(CrashLogPath))
        {
            // Move to history instead of deleting
            var historyPath = Path.Combine(AppDataPath, $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
            File.Move(CrashLogPath, historyPath);
        }
    }

    /// <summary>
    /// Save current session state for recovery.
    /// </summary>
    public static void SaveSessionState(SessionState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SessionStatePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save session state: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the last session state for recovery.
    /// </summary>
    public static SessionState? GetLastSessionState()
    {
        if (!File.Exists(SessionStatePath)) return null;

        try
        {
            var json = File.ReadAllText(SessionStatePath);
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read session state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clear session state after successful recovery.
    /// </summary>
    public static void ClearSessionState()
    {
        if (File.Exists(SessionStatePath))
        {
            File.Delete(SessionStatePath);
        }
    }

    /// <summary>
    /// Mark a clean shutdown (no crash).
    /// </summary>
    public static void MarkCleanShutdown()
    {
        ClearSessionState();
        // Remove any crash markers from previous crashes that weren't acknowledged
        if (File.Exists(CrashLogPath))
        {
            AcknowledgeCrash();
        }
    }

    /// <summary>
    /// Generate a support bundle.
    /// </summary>
    public static async Task<string> GenerateSupportBundleAsync()
    {
        var bundlePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"ScalarScope_Support_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                ScalarScope Support Bundle                  ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        // System info
        sb.AppendLine("## System Information");
        sb.AppendLine($"  Generated:     {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"  OS:            {Environment.OSVersion}");
        sb.AppendLine($"  Machine:       {Environment.MachineName}");
        sb.AppendLine($"  .NET Version:  {Environment.Version}");
        sb.AppendLine($"  64-bit OS:     {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"  64-bit Proc:   {Environment.Is64BitProcess}");
        sb.AppendLine($"  Processors:    {Environment.ProcessorCount}");
        sb.AppendLine();

        // App info
        sb.AppendLine("## Application Information");
        sb.AppendLine($"  Version:       {AppVersion}");
        sb.AppendLine($"  Working Dir:   {Environment.CurrentDirectory}");
        sb.AppendLine($"  App Data:      {AppDataPath}");
        sb.AppendLine();

        // Memory info
        using var process = Process.GetCurrentProcess();
        sb.AppendLine("## Memory Usage");
        sb.AppendLine($"  Working Set:   {process.WorkingSet64 / (1024 * 1024):F1} MB");
        sb.AppendLine($"  Private Mem:   {process.PrivateMemorySize64 / (1024 * 1024):F1} MB");
        sb.AppendLine($"  GC Total:      {GC.GetTotalMemory(false) / (1024 * 1024):F1} MB");
        sb.AppendLine();

        // Last crash info (if any)
        var crashInfo = GetLastCrashInfo();
        if (crashInfo != null)
        {
            sb.AppendLine("## Last Crash");
            sb.AppendLine($"  Time:          {crashInfo.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"  Version:       {crashInfo.Version}");
            sb.AppendLine($"  Type:          {crashInfo.ExceptionType}");
            sb.AppendLine($"  Message:       {crashInfo.Message}");
            sb.AppendLine();
            sb.AppendLine("  Stack Trace:");
            sb.AppendLine(crashInfo.StackTrace);
            sb.AppendLine();
        }

        // Session state (if any)
        var sessionState = GetLastSessionState();
        if (sessionState != null)
        {
            sb.AppendLine("## Last Session State");
            sb.AppendLine($"  Loaded File:   {sessionState.LoadedFilePath ?? "(none)"}");
            sb.AppendLine($"  Current Page:  {sessionState.CurrentPage}");
            sb.AppendLine($"  Playback Time: {sessionState.PlaybackTime:F3}");
            sb.AppendLine($"  Theme:         {sessionState.Theme}");
            sb.AppendLine();
        }

        // Recent logs
        if (Directory.Exists(LogsPath))
        {
            var logFiles = Directory.GetFiles(LogsPath, "*.log")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(3);

            foreach (var logFile in logFiles)
            {
                sb.AppendLine($"## Log: {Path.GetFileName(logFile)}");
                var content = await File.ReadAllTextAsync(logFile);
                // Truncate if too long
                if (content.Length > 10000)
                {
                    content = content[^10000..];
                    sb.AppendLine("  (truncated, showing last 10000 characters)");
                }
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                     End of Support Bundle                      ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        await File.WriteAllTextAsync(bundlePath, sb.ToString());
        return bundlePath;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        RecordCrash(ex, "UnhandledException");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        RecordCrash(e.Exception, "UnobservedTaskException");
        e.SetObserved(); // Prevent process termination
    }

    private static void RecordCrash(Exception? ex, string source)
    {
        try
        {
            var crashInfo = new CrashInfo
            {
                Timestamp = DateTime.UtcNow,
                Version = AppVersion,
                Source = source,
                ExceptionType = ex?.GetType().FullName ?? "Unknown",
                Message = ex?.Message ?? "No message",
                StackTrace = ex?.StackTrace ?? "No stack trace"
            };

            var json = JsonSerializer.Serialize(crashInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CrashLogPath, json);
        }
        catch
        {
            // Can't log the crash - nothing we can do
        }
    }
}

/// <summary>
/// Information about a crash.
/// </summary>
public record CrashInfo
{
    public DateTime Timestamp { get; init; }
    public string Version { get; init; } = "";
    public string Source { get; init; } = "";
    public string ExceptionType { get; init; } = "";
    public string Message { get; init; } = "";
    public string StackTrace { get; init; } = "";
}

/// <summary>
/// Session state for recovery.
/// </summary>
public record SessionState
{
    public string? LoadedFilePath { get; init; }
    public string CurrentPage { get; init; } = "Trajectory";
    public double PlaybackTime { get; init; }
    public bool IsPlaying { get; init; }
    public string Theme { get; init; } = "dark";
    public DateTime SavedAt { get; init; } = DateTime.UtcNow;
}
