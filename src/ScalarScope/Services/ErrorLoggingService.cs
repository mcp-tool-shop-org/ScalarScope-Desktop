// Phase 6.2.5: Error Logging Service
// Structured error logging and export capabilities.

using System.Text;
using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.2: Service for structured error logging and export.
/// Provides persistent error history, export capabilities, and crash reporting prep.
/// </summary>
public sealed class ErrorLoggingService
{
    private static readonly Lazy<ErrorLoggingService> _instance = 
        new(() => new ErrorLoggingService());
    
    public static ErrorLoggingService Instance => _instance.Value;
    
    private readonly string _logDirectory;
    private readonly string _currentSessionLog;
    private readonly List<ErrorLogEntry> _sessionErrors = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Maximum entries to keep in memory.
    /// </summary>
    public int MaxSessionEntries { get; set; } = 100;
    
    /// <summary>
    /// Maximum log file size before rotation (bytes).
    /// </summary>
    public long MaxLogFileSize { get; set; } = 5 * 1024 * 1024; // 5MB
    
    /// <summary>
    /// Number of rotated log files to keep.
    /// </summary>
    public int MaxLogFiles { get; set; } = 5;
    
    private ErrorLoggingService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "logs");
        
        Directory.CreateDirectory(_logDirectory);
        
        _currentSessionLog = Path.Combine(
            _logDirectory, 
            $"scalarscope_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");
    }
    
    /// <summary>
    /// Log an error state.
    /// </summary>
    public void Log(ErrorState error, string? context = null, Exception? exception = null)
    {
        var entry = new ErrorLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Code = error.Code,
            Category = error.Category.ToString(),
            Severity = error.Severity.ToString(),
            Message = error.TechnicalMessage,
            UserMessage = error.UserExplanation,
            Context = context,
            ExceptionType = exception?.GetType().FullName,
            ExceptionMessage = exception?.Message,
            StackTrace = exception?.StackTrace,
            SessionId = AppSession.Id,
            AppVersion = AppSession.Version
        };
        
        lock (_lock)
        {
            _sessionErrors.Add(entry);
            if (_sessionErrors.Count > MaxSessionEntries)
            {
                _sessionErrors.RemoveAt(0);
            }
        }
        
        WriteToFile(entry);
    }
    
    /// <summary>
    /// Log an exception (will be mapped to ErrorState).
    /// </summary>
    public void Log(Exception ex, string? context = null)
    {
        var errorState = ErrorStateMapping.MapException(ex);
        Log(errorState, context, ex);
    }
    
    /// <summary>
    /// Log a simple message at a severity level.
    /// </summary>
    public void Log(ErrorSeverity severity, string message, string? context = null)
    {
        var entry = new ErrorLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Code = "LOG_MESSAGE",
            Category = "System",
            Severity = severity.ToString(),
            Message = message,
            Context = context,
            SessionId = AppSession.Id,
            AppVersion = AppSession.Version
        };
        
        lock (_lock)
        {
            _sessionErrors.Add(entry);
        }
        
        WriteToFile(entry);
    }
    
    /// <summary>
    /// Get recent errors from the current session.
    /// </summary>
    public IReadOnlyList<ErrorLogEntry> GetRecentErrors(int count = 20)
    {
        lock (_lock)
        {
            return _sessionErrors
                .OrderByDescending(e => e.Timestamp)
                .Take(count)
                .ToList()
                .AsReadOnly();
        }
    }
    
    /// <summary>
    /// Get errors filtered by category.
    /// </summary>
    public IReadOnlyList<ErrorLogEntry> GetErrorsByCategory(string category)
    {
        lock (_lock)
        {
            return _sessionErrors
                .Where(e => e.Category == category)
                .OrderByDescending(e => e.Timestamp)
                .ToList()
                .AsReadOnly();
        }
    }
    
    /// <summary>
    /// Get error count by severity for current session.
    /// </summary>
    public Dictionary<string, int> GetErrorStats()
    {
        lock (_lock)
        {
            return _sessionErrors
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
    
    /// <summary>
    /// Export error log to a file.
    /// </summary>
    public async Task<string> ExportLogAsync(string destinationPath, ErrorLogFormat format = ErrorLogFormat.Json)
    {
        IReadOnlyList<ErrorLogEntry> errors;
        lock (_lock)
        {
            errors = _sessionErrors.ToList().AsReadOnly();
        }
        
        var content = format switch
        {
            ErrorLogFormat.Json => SerializeJson(errors),
            ErrorLogFormat.Text => SerializeText(errors),
            ErrorLogFormat.Csv => SerializeCsv(errors),
            _ => SerializeJson(errors)
        };
        
        var extension = format switch
        {
            ErrorLogFormat.Json => ".json",
            ErrorLogFormat.Text => ".txt",
            ErrorLogFormat.Csv => ".csv",
            _ => ".json"
        };
        
        var fullPath = destinationPath.EndsWith(extension) 
            ? destinationPath 
            : destinationPath + extension;
        
        await File.WriteAllTextAsync(fullPath, content);
        return fullPath;
    }
    
    /// <summary>
    /// Get path to current session log file.
    /// </summary>
    public string GetCurrentLogPath() => _currentSessionLog;
    
    /// <summary>
    /// Get all log files in the log directory.
    /// </summary>
    public IReadOnlyList<string> GetLogFiles()
    {
        return Directory.GetFiles(_logDirectory, "scalarscope_*.log")
            .OrderByDescending(f => f)
            .ToList()
            .AsReadOnly();
    }
    
    /// <summary>
    /// Clear old log files, keeping only the most recent ones.
    /// </summary>
    public void RotateLogs()
    {
        var files = Directory.GetFiles(_logDirectory, "scalarscope_*.log")
            .OrderByDescending(f => f)
            .Skip(MaxLogFiles)
            .ToList();
        
        foreach (var file in files)
        {
            try { File.Delete(file); } 
            catch { /* ignore cleanup failures */ }
        }
    }
    
    /// <summary>
    /// Generate a crash report for the given exception.
    /// </summary>
    public async Task<string> GenerateCrashReportAsync(Exception ex, string destinationPath)
    {
        var report = new CrashReport
        {
            Timestamp = DateTime.UtcNow,
            SessionId = AppSession.Id,
            AppVersion = AppSession.Version,
            OsVersion = Environment.OSVersion.ToString(),
            DotNetVersion = Environment.Version.ToString(),
            ExceptionType = ex.GetType().FullName ?? "Unknown",
            ExceptionMessage = ex.Message,
            StackTrace = ex.StackTrace ?? "",
            InnerExceptionType = ex.InnerException?.GetType().FullName,
            InnerExceptionMessage = ex.InnerException?.Message,
            RecentErrors = GetRecentErrors(10).ToList()
        };
        
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(destinationPath, json);
        return destinationPath;
    }
    
    private void WriteToFile(ErrorLogEntry entry)
    {
        try
        {
            // Check if rotation needed
            if (File.Exists(_currentSessionLog))
            {
                var fileInfo = new FileInfo(_currentSessionLog);
                if (fileInfo.Length > MaxLogFileSize)
                {
                    RotateLogs();
                }
            }
            
            var line = $"[{entry.Timestamp:O}] [{entry.Severity}] [{entry.Code}] {entry.Message}";
            if (!string.IsNullOrEmpty(entry.Context))
            {
                line += $" (Context: {entry.Context})";
            }
            if (!string.IsNullOrEmpty(entry.ExceptionType))
            {
                line += $"\n  Exception: {entry.ExceptionType}: {entry.ExceptionMessage}";
            }
            line += "\n";
            
            File.AppendAllText(_currentSessionLog, line);
        }
        catch
        {
            // Don't throw from logging
        }
    }
    
    private static string SerializeJson(IReadOnlyList<ErrorLogEntry> entries)
    {
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }
    
    private static string SerializeText(IReadOnlyList<ErrorLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ScalarScope Error Log");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();
        
        foreach (var entry in entries)
        {
            sb.AppendLine($"[{entry.Timestamp:O}]");
            sb.AppendLine($"  Code:     {entry.Code}");
            sb.AppendLine($"  Severity: {entry.Severity}");
            sb.AppendLine($"  Category: {entry.Category}");
            sb.AppendLine($"  Message:  {entry.Message}");
            
            if (!string.IsNullOrEmpty(entry.Context))
            {
                sb.AppendLine($"  Context:  {entry.Context}");
            }
            
            if (!string.IsNullOrEmpty(entry.ExceptionType))
            {
                sb.AppendLine($"  Exception: {entry.ExceptionType}");
                sb.AppendLine($"  Details:   {entry.ExceptionMessage}");
            }
            
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    private static string SerializeCsv(IReadOnlyList<ErrorLogEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,Code,Severity,Category,Message,Context,ExceptionType");
        
        foreach (var entry in entries)
        {
            var message = entry.Message?.Replace("\"", "\"\"") ?? "";
            var context = entry.Context?.Replace("\"", "\"\"") ?? "";
            
            sb.AppendLine($"\"{entry.Timestamp:O}\",\"{entry.Code}\",\"{entry.Severity}\",\"{entry.Category}\",\"{message}\",\"{context}\",\"{entry.ExceptionType ?? ""}\"");
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// A single error log entry.
/// </summary>
public record ErrorLogEntry
{
    public DateTime Timestamp { get; init; }
    public required string Code { get; init; }
    public required string Category { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public string? UserMessage { get; init; }
    public string? Context { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? StackTrace { get; init; }
    public string? SessionId { get; init; }
    public string? AppVersion { get; init; }
}

/// <summary>
/// Crash report for unhandled exceptions.
/// </summary>
public record CrashReport
{
    public DateTime Timestamp { get; init; }
    public required string SessionId { get; init; }
    public required string AppVersion { get; init; }
    public required string OsVersion { get; init; }
    public required string DotNetVersion { get; init; }
    public required string ExceptionType { get; init; }
    public required string ExceptionMessage { get; init; }
    public required string StackTrace { get; init; }
    public string? InnerExceptionType { get; init; }
    public string? InnerExceptionMessage { get; init; }
    public List<ErrorLogEntry> RecentErrors { get; init; } = new();
}

/// <summary>
/// Export formats for error logs.
/// </summary>
public enum ErrorLogFormat
{
    Json,
    Text,
    Csv
}

/// <summary>
/// Application session information for logging.
/// </summary>
public static class AppSession
{
    private static readonly string _id = Guid.NewGuid().ToString("N")[..8];
    
    public static string Id => _id;
    public static string Version => "1.5.0"; // Will be injected at build time
}
