// Phase 6.3.1: Retry Policy Service
// Provides configurable retry logic with exponential backoff and circuit breaker.

using System.Diagnostics;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.3: Service for executing operations with retry logic.
/// Supports exponential backoff, jitter, and circuit breaker patterns.
/// </summary>
public static class RetryPolicyService
{
    private static readonly Dictionary<string, CircuitState> _circuits = new();
    private static readonly object _circuitLock = new();
    
    /// <summary>
    /// Default retry policy for transient failures.
    /// </summary>
    public static RetryPolicy DefaultPolicy { get; } = new()
    {
        MaxAttempts = 3,
        InitialDelayMs = 100,
        MaxDelayMs = 5000,
        BackoffMultiplier = 2.0,
        UseJitter = true,
        RetryableExceptions = new[] 
        { 
            typeof(IOException),
            typeof(TimeoutException),
            typeof(HttpRequestException)
        }
    };
    
    /// <summary>
    /// Policy for file operations.
    /// </summary>
    public static RetryPolicy FileOperationPolicy { get; } = new()
    {
        MaxAttempts = 3,
        InitialDelayMs = 200,
        MaxDelayMs = 2000,
        BackoffMultiplier = 2.0,
        UseJitter = true,
        RetryableExceptions = new[] 
        { 
            typeof(IOException),
            typeof(UnauthorizedAccessException)
        }
    };
    
    /// <summary>
    /// Policy for computation operations (no retry by default).
    /// </summary>
    public static RetryPolicy ComputationPolicy { get; } = new()
    {
        MaxAttempts = 1,
        InitialDelayMs = 0,
        MaxDelayMs = 0,
        BackoffMultiplier = 1.0,
        UseJitter = false,
        RetryableExceptions = Array.Empty<Type>()
    };
    
    /// <summary>
    /// Execute an action with retry logic.
    /// </summary>
    public static async Task<RetryResult> ExecuteAsync(
        Func<Task> action,
        RetryPolicy? policy = null,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= DefaultPolicy;
        operationName ??= "Operation";
        
        var result = new RetryResult
        {
            OperationName = operationName,
            Policy = policy
        };
        
        var sw = Stopwatch.StartNew();
        
        for (int attempt = 1; attempt <= policy.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var attemptInfo = new RetryAttempt
            {
                AttemptNumber = attempt,
                StartTime = DateTime.UtcNow
            };
            
            try
            {
                await action();
                
                attemptInfo.Succeeded = true;
                attemptInfo.Duration = sw.Elapsed;
                result.Attempts.Add(attemptInfo);
                result.Succeeded = true;
                result.TotalDuration = sw.Elapsed;
                
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                attemptInfo.Succeeded = false;
                attemptInfo.Exception = ex;
                attemptInfo.Duration = sw.Elapsed;
                result.Attempts.Add(attemptInfo);
                
                // Check if we should retry
                if (attempt >= policy.MaxAttempts || !IsRetryable(ex, policy))
                {
                    result.Succeeded = false;
                    result.FinalException = ex;
                    result.TotalDuration = sw.Elapsed;
                    
                    // Log the failure
                    ErrorLoggingService.Instance.Log(
                        ErrorSeverity.Warning,
                        $"Retry exhausted for {operationName} after {attempt} attempts: {ex.Message}",
                        "RetryPolicy");
                    
                    return result;
                }
                
                // Calculate delay with exponential backoff
                var delay = CalculateDelay(attempt, policy);
                attemptInfo.DelayBeforeNextMs = delay;
                
                Debug.WriteLine($"[Retry] {operationName} attempt {attempt} failed, retrying in {delay}ms");
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        result.TotalDuration = sw.Elapsed;
        return result;
    }
    
    /// <summary>
    /// Execute a function with retry logic and return a value.
    /// </summary>
    public static async Task<RetryResult<T>> ExecuteAsync<T>(
        Func<Task<T>> func,
        RetryPolicy? policy = null,
        string? operationName = null,
        CancellationToken cancellationToken = default)
    {
        policy ??= DefaultPolicy;
        operationName ??= "Operation";
        
        var result = new RetryResult<T>
        {
            OperationName = operationName,
            Policy = policy
        };
        
        var sw = Stopwatch.StartNew();
        
        for (int attempt = 1; attempt <= policy.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            var attemptInfo = new RetryAttempt
            {
                AttemptNumber = attempt,
                StartTime = DateTime.UtcNow
            };
            
            try
            {
                var value = await func();
                
                attemptInfo.Succeeded = true;
                attemptInfo.Duration = sw.Elapsed;
                result.Attempts.Add(attemptInfo);
                result.Succeeded = true;
                result.Value = value;
                result.TotalDuration = sw.Elapsed;
                
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                attemptInfo.Succeeded = false;
                attemptInfo.Exception = ex;
                attemptInfo.Duration = sw.Elapsed;
                result.Attempts.Add(attemptInfo);
                
                if (attempt >= policy.MaxAttempts || !IsRetryable(ex, policy))
                {
                    result.Succeeded = false;
                    result.FinalException = ex;
                    result.TotalDuration = sw.Elapsed;
                    return result;
                }
                
                var delay = CalculateDelay(attempt, policy);
                attemptInfo.DelayBeforeNextMs = delay;
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        result.TotalDuration = sw.Elapsed;
        return result;
    }
    
    /// <summary>
    /// Execute with circuit breaker pattern.
    /// </summary>
    public static async Task<RetryResult> ExecuteWithCircuitBreakerAsync(
        string circuitName,
        Func<Task> action,
        RetryPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        var circuit = GetOrCreateCircuit(circuitName);
        
        // Check if circuit is open
        if (circuit.IsOpen)
        {
            if (circuit.ShouldAttemptReset())
            {
                circuit.State = CircuitBreakerState.HalfOpen;
            }
            else
            {
                return new RetryResult
                {
                    OperationName = circuitName,
                    Succeeded = false,
                    FinalException = new CircuitBreakerOpenException(circuitName),
                    CircuitState = circuit.State
                };
            }
        }
        
        var result = await ExecuteAsync(action, policy, circuitName, cancellationToken);
        
        // Update circuit state
        if (result.Succeeded)
        {
            circuit.RecordSuccess();
        }
        else
        {
            circuit.RecordFailure();
        }
        
        result.CircuitState = circuit.State;
        return result;
    }
    
    private static bool IsRetryable(Exception ex, RetryPolicy policy)
    {
        // Check configured retryable exceptions
        foreach (var type in policy.RetryableExceptions)
        {
            if (type.IsAssignableFrom(ex.GetType()))
            {
                return true;
            }
        }
        
        // Check custom predicate
        return policy.RetryPredicate?.Invoke(ex) ?? false;
    }
    
    private static int CalculateDelay(int attempt, RetryPolicy policy)
    {
        var delay = policy.InitialDelayMs * Math.Pow(policy.BackoffMultiplier, attempt - 1);
        delay = Math.Min(delay, policy.MaxDelayMs);
        
        if (policy.UseJitter)
        {
            var jitter = Random.Shared.NextDouble() * 0.3 * delay;
            delay += jitter;
        }
        
        return (int)delay;
    }
    
    private static CircuitState GetOrCreateCircuit(string name)
    {
        lock (_circuitLock)
        {
            if (!_circuits.TryGetValue(name, out var circuit))
            {
                circuit = new CircuitState(name);
                _circuits[name] = circuit;
            }
            return circuit;
        }
    }
}

/// <summary>
/// Configuration for retry behavior.
/// </summary>
public record RetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public int InitialDelayMs { get; init; } = 100;
    public int MaxDelayMs { get; init; } = 5000;
    public double BackoffMultiplier { get; init; } = 2.0;
    public bool UseJitter { get; init; } = true;
    public Type[] RetryableExceptions { get; init; } = Array.Empty<Type>();
    public Func<Exception, bool>? RetryPredicate { get; init; }
    
    /// <summary>
    /// Create a custom policy builder.
    /// </summary>
    public static RetryPolicyBuilder Builder() => new();
}

/// <summary>
/// Builder for creating custom retry policies.
/// </summary>
public class RetryPolicyBuilder
{
    private int _maxAttempts = 3;
    private int _initialDelay = 100;
    private int _maxDelay = 5000;
    private double _multiplier = 2.0;
    private bool _jitter = true;
    private readonly List<Type> _retryable = new();
    private Func<Exception, bool>? _predicate;
    
    public RetryPolicyBuilder MaxAttempts(int attempts)
    {
        _maxAttempts = attempts;
        return this;
    }
    
    public RetryPolicyBuilder InitialDelay(int ms)
    {
        _initialDelay = ms;
        return this;
    }
    
    public RetryPolicyBuilder MaxDelay(int ms)
    {
        _maxDelay = ms;
        return this;
    }
    
    public RetryPolicyBuilder ExponentialBackoff(double multiplier)
    {
        _multiplier = multiplier;
        return this;
    }
    
    public RetryPolicyBuilder WithJitter(bool enabled = true)
    {
        _jitter = enabled;
        return this;
    }
    
    public RetryPolicyBuilder RetryOn<TException>() where TException : Exception
    {
        _retryable.Add(typeof(TException));
        return this;
    }
    
    public RetryPolicyBuilder RetryWhen(Func<Exception, bool> predicate)
    {
        _predicate = predicate;
        return this;
    }
    
    public RetryPolicy Build() => new()
    {
        MaxAttempts = _maxAttempts,
        InitialDelayMs = _initialDelay,
        MaxDelayMs = _maxDelay,
        BackoffMultiplier = _multiplier,
        UseJitter = _jitter,
        RetryableExceptions = _retryable.ToArray(),
        RetryPredicate = _predicate
    };
}

/// <summary>
/// Result of a retry operation.
/// </summary>
public record RetryResult
{
    public string OperationName { get; init; } = "";
    public bool Succeeded { get; set; }
    public Exception? FinalException { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<RetryAttempt> Attempts { get; } = new();
    public RetryPolicy? Policy { get; init; }
    public CircuitBreakerState? CircuitState { get; set; }
    
    public int AttemptCount => Attempts.Count;
    public bool WasRetried => Attempts.Count > 1;
}

/// <summary>
/// Typed result with return value.
/// </summary>
public record RetryResult<T> : RetryResult
{
    public T? Value { get; set; }
}

/// <summary>
/// Information about a single retry attempt.
/// </summary>
public record RetryAttempt
{
    public int AttemptNumber { get; init; }
    public DateTime StartTime { get; init; }
    public bool Succeeded { get; set; }
    public Exception? Exception { get; set; }
    public TimeSpan Duration { get; set; }
    public int DelayBeforeNextMs { get; set; }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Tracks circuit breaker state for an operation.
/// </summary>
internal class CircuitState
{
    private int _failureCount;
    private DateTime? _lastFailureTime;
    private readonly int _failureThreshold = 5;
    private readonly TimeSpan _resetTimeout = TimeSpan.FromSeconds(30);
    
    public string Name { get; }
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
    
    public bool IsOpen => State == CircuitBreakerState.Open;
    
    public CircuitState(string name)
    {
        Name = name;
    }
    
    public void RecordSuccess()
    {
        _failureCount = 0;
        State = CircuitBreakerState.Closed;
    }
    
    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
        
        if (_failureCount >= _failureThreshold)
        {
            State = CircuitBreakerState.Open;
        }
    }
    
    public bool ShouldAttemptReset()
    {
        if (_lastFailureTime is null) return true;
        return DateTime.UtcNow - _lastFailureTime.Value > _resetTimeout;
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open.
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    public string CircuitName { get; }
    
    public CircuitBreakerOpenException(string circuitName)
        : base($"Circuit breaker '{circuitName}' is open")
    {
        CircuitName = circuitName;
    }
}
