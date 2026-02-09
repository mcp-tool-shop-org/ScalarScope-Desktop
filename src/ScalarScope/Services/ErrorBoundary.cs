using System.Diagnostics;

namespace ScalarScope.Services;

/// <summary>
/// Phase 5.5: Error boundary service for graceful error handling.
/// Provides safe execution wrappers that prevent UI crashes.
/// </summary>
public static class ErrorBoundary
{
    /// <summary>
    /// Execute an action with error boundary protection.
    /// Logs errors and returns success/failure.
    /// </summary>
    public static bool TrySafe(Action action, string? context = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return false;
        }
    }
    
    /// <summary>
    /// Execute an async action with error boundary protection.
    /// </summary>
    public static async Task<bool> TrySafeAsync(Func<Task> action, string? context = null)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return false;
        }
    }
    
    /// <summary>
    /// Execute a function with error boundary protection.
    /// Returns default(T) on failure.
    /// </summary>
    public static T? TrySafe<T>(Func<T> func, string? context = null)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return default;
        }
    }
    
    /// <summary>
    /// Execute an async function with error boundary protection.
    /// </summary>
    public static async Task<T?> TrySafeAsync<T>(Func<Task<T>> func, string? context = null)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return default;
        }
    }
    
    /// <summary>
    /// Execute with error boundary and custom fallback.
    /// </summary>
    public static T TrySafe<T>(Func<T> func, T fallback, string? context = null)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return fallback;
        }
    }
    
    /// <summary>
    /// Execute UI-safe action on main thread with error protection.
    /// </summary>
    public static void TrySafeOnMainThread(Action action, string? context = null)
    {
        MainThread.BeginInvokeOnMainThread(() => TrySafe(action, context));
    }
    
    /// <summary>
    /// Execute with retry on transient failures.
    /// </summary>
    public static async Task<T?> TryWithRetryAsync<T>(
        Func<Task<T>> func, 
        int maxRetries = 3,
        int delayMs = 100,
        string? context = null)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await func();
            }
            catch (Exception ex) when (IsTransient(ex) && i < maxRetries - 1)
            {
                Debug.WriteLine($"[ErrorBoundary] Retry {i + 1}/{maxRetries} for {context}: {ex.Message}");
                await Task.Delay(delayMs * (i + 1)); // Exponential backoff
            }
            catch (Exception ex)
            {
                LogError(ex, context);
                return default;
            }
        }
        return default;
    }
    
    /// <summary>
    /// Execute with timeout protection.
    /// </summary>
    public static async Task<T?> TryWithTimeoutAsync<T>(
        Func<Task<T>> func,
        TimeSpan timeout,
        string? context = null)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var task = func();
            var completed = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            
            if (completed == task)
            {
                return await task;
            }
            
            Debug.WriteLine($"[ErrorBoundary] Timeout after {timeout.TotalMilliseconds}ms for {context}");
            return default;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"[ErrorBoundary] Operation cancelled for {context}");
            return default;
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            return default;
        }
    }
    
    /// <summary>
    /// Execute command safely and show user-friendly error message.
    /// </summary>
    public static async Task<bool> TryCommandAsync(
        Func<Task> command,
        string operationName,
        Action<string>? onError = null)
    {
        try
        {
            await command();
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, operationName);
            var userMessage = GetUserFriendlyMessage(ex, operationName);
            onError?.Invoke(userMessage);
            return false;
        }
    }
    
    private static bool IsTransient(Exception ex)
    {
        // Common transient exception types
        return ex is IOException or 
               TimeoutException or
               HttpRequestException or
               TaskCanceledException;
    }
    
    private static string GetUserFriendlyMessage(Exception ex, string operation)
    {
        return ex switch
        {
            FileNotFoundException => $"Could not find the requested file during {operation}.",
            DirectoryNotFoundException => $"Could not find the requested folder during {operation}.",
            UnauthorizedAccessException => $"Permission denied during {operation}. Try running as administrator.",
            IOException io when io.Message.Contains("being used") => $"File is in use. Close other applications and try again.",
            OutOfMemoryException => $"Not enough memory to complete {operation}. Close some applications and try again.",
            TimeoutException => $"Operation timed out during {operation}. Please try again.",
            InvalidOperationException => $"Invalid operation during {operation}: {ex.Message}",
            _ => $"An error occurred during {operation}: {ex.Message}"
        };
    }
    
    private static void LogError(Exception ex, string? context)
    {
        var contextStr = string.IsNullOrEmpty(context) ? "" : $" [{context}]";
        Debug.WriteLine($"[ErrorBoundary]{contextStr} {ex.GetType().Name}: {ex.Message}");
        
        // Log full stack trace in debug builds
#if DEBUG
        Debug.WriteLine(ex.StackTrace);
#endif

        // Record for crash reporting (non-fatal)
        try
        {
            var errorLog = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ScalarScope", "error_log.txt");
            
            var entry = $"[{DateTime.UtcNow:O}]{contextStr} {ex.GetType().Name}: {ex.Message}\n";
            File.AppendAllText(errorLog, entry);
        }
        catch
        {
            // Don't throw when logging fails
        }
    }
}

/// <summary>
/// Result wrapper with error information.
/// </summary>
public readonly struct SafeResult<T>
{
    public T? Value { get; }
    public Exception? Error { get; }
    public bool IsSuccess => Error == null;
    public bool IsFailure => Error != null;
    
    private SafeResult(T? value, Exception? error)
    {
        Value = value;
        Error = error;
    }
    
    public static SafeResult<T> Success(T value) => new(value, null);
    public static SafeResult<T> Failure(Exception error) => new(default, error);
    
    public T GetValueOrDefault(T fallback) => IsSuccess ? Value! : fallback;
    
    public SafeResult<TOut> Map<TOut>(Func<T, TOut> mapper)
    {
        if (IsFailure) return SafeResult<TOut>.Failure(Error!);
        try
        {
            return SafeResult<TOut>.Success(mapper(Value!));
        }
        catch (Exception ex)
        {
            return SafeResult<TOut>.Failure(ex);
        }
    }
}
