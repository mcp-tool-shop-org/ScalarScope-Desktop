// Phase 6.3.3: Resumable Operation Service
// Provides infrastructure for resumable long-running operations.

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.3: Service for running operations that can be paused and resumed.
/// Integrates CheckpointService with actual operation execution.
/// </summary>
public static class ResumableOperationService
{
    /// <summary>
    /// Run a resumable batch operation.
    /// </summary>
    public static async Task<ResumableOperationResult> RunBatchAsync<TItem>(
        string operationType,
        IReadOnlyList<TItem> items,
        Func<TItem, int, CancellationToken, Task<bool>> processItem,
        ResumableOperationOptions? options = null,
        string? checkpointId = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ResumableOperationOptions();
        var service = CheckpointService.Instance;
        
        // Resume or create checkpoint
        OperationCheckpoint checkpoint;
        int startIndex = 0;
        
        if (!string.IsNullOrEmpty(checkpointId))
        {
            var resumed = service.Resume(checkpointId);
            if (resumed is null)
            {
                return new ResumableOperationResult
                {
                    Success = false,
                    ErrorMessage = "Could not resume checkpoint"
                };
            }
            checkpoint = resumed;
            startIndex = resumed.CurrentStep;
        }
        else
        {
            checkpoint = service.CreateCheckpoint(
                operationType,
                Guid.NewGuid().ToString("N"),
                new Dictionary<string, object>
                {
                    ["TotalItems"] = items.Count,
                    ["StartedAt"] = DateTime.UtcNow.ToString("O")
                });
        }
        
        var result = new ResumableOperationResult
        {
            CheckpointId = checkpoint.Id,
            TotalItems = items.Count,
            StartIndex = startIndex
        };
        
        try
        {
            for (int i = startIndex; i < items.Count; i++)
            {
                // Check for pause/cancel
                if (cancellationToken.IsCancellationRequested)
                {
                    if (options.PauseOnCancel)
                    {
                        service.Pause(checkpoint.Id);
                        result.WasPaused = true;
                        result.ProcessedItems = i - startIndex;
                        result.LastProcessedIndex = i - 1;
                        return result;
                    }
                    break;
                }
                
                // Update progress
                service.UpdateProgress(
                    checkpoint.Id,
                    i + 1,
                    items.Count,
                    $"Processing item {i + 1} of {items.Count}");
                
                // Report progress to callback
                options.OnProgress?.Invoke(new OperationProgress
                {
                    Current = i + 1,
                    Total = items.Count,
                    PercentComplete = (double)(i + 1) / items.Count * 100,
                    CurrentItem = items[i]
                });
                
                // Process item
                try
                {
                    var itemSuccess = await processItem(items[i], i, cancellationToken);
                    
                    if (!itemSuccess)
                    {
                        result.FailedItems.Add(i);
                        
                        if (options.StopOnFirstError)
                        {
                            service.Fail(checkpoint.Id, $"Item {i} failed to process");
                            result.Success = false;
                            result.ErrorMessage = $"Failed at item {i}";
                            return result;
                        }
                    }
                    else
                    {
                        result.SuccessfulItems.Add(i);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedItems.Add(i);
                    
                    if (options.StopOnFirstError)
                    {
                        service.Fail(checkpoint.Id, ex.Message, ex);
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        result.Exception = ex;
                        return result;
                    }
                    
                    // Log but continue
                    ErrorLoggingService.Instance.Log(ex, $"ResumableOp:{operationType}:Item{i}");
                }
                
                // Optional delay between items
                if (options.DelayBetweenItemsMs > 0 && i < items.Count - 1)
                {
                    await Task.Delay(options.DelayBetweenItemsMs, cancellationToken);
                }
            }
            
            // Completed successfully
            service.Complete(checkpoint.Id, new Dictionary<string, object>
            {
                ["CompletedAt"] = DateTime.UtcNow.ToString("O"),
                ["SuccessCount"] = result.SuccessfulItems.Count,
                ["FailCount"] = result.FailedItems.Count
            });
            
            result.Success = result.FailedItems.Count == 0;
            result.ProcessedItems = items.Count - startIndex;
            result.LastProcessedIndex = items.Count - 1;
            result.WasCompleted = true;
            
            return result;
        }
        catch (OperationCanceledException)
        {
            if (options.PauseOnCancel)
            {
                var currentIndex = result.SuccessfulItems.Count + result.FailedItems.Count + startIndex;
                service.Pause(checkpoint.Id);
                result.WasPaused = true;
                result.ProcessedItems = currentIndex - startIndex;
            }
            else
            {
                service.Fail(checkpoint.Id, "Operation was cancelled");
            }
            return result;
        }
        catch (Exception ex)
        {
            service.Fail(checkpoint.Id, ex.Message, ex);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            return result;
        }
    }
    
    /// <summary>
    /// Run a resumable sequential operation with steps.
    /// </summary>
    public static async Task<ResumableOperationResult> RunStepsAsync(
        string operationType,
        IReadOnlyList<OperationStep> steps,
        ResumableOperationOptions? options = null,
        string? checkpointId = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ResumableOperationOptions();
        var service = CheckpointService.Instance;
        
        OperationCheckpoint checkpoint;
        int startIndex = 0;
        
        if (!string.IsNullOrEmpty(checkpointId))
        {
            var resumed = service.Resume(checkpointId);
            if (resumed is null)
            {
                return new ResumableOperationResult
                {
                    Success = false,
                    ErrorMessage = "Could not resume checkpoint"
                };
            }
            checkpoint = resumed;
            startIndex = resumed.CurrentStep;
        }
        else
        {
            checkpoint = service.CreateCheckpoint(operationType, Guid.NewGuid().ToString("N"));
        }
        
        var result = new ResumableOperationResult
        {
            CheckpointId = checkpoint.Id,
            TotalItems = steps.Count,
            StartIndex = startIndex
        };
        
        try
        {
            for (int i = startIndex; i < steps.Count; i++)
            {
                var step = steps[i];
                
                if (cancellationToken.IsCancellationRequested)
                {
                    if (options.PauseOnCancel)
                    {
                        service.Pause(checkpoint.Id);
                        result.WasPaused = true;
                        return result;
                    }
                    break;
                }
                
                service.UpdateProgress(checkpoint.Id, i + 1, steps.Count, step.Name);
                
                options.OnProgress?.Invoke(new OperationProgress
                {
                    Current = i + 1,
                    Total = steps.Count,
                    PercentComplete = (double)(i + 1) / steps.Count * 100,
                    StepName = step.Name
                });
                
                try
                {
                    var stepResult = await step.Execute(cancellationToken);
                    
                    if (stepResult.Success)
                    {
                        result.SuccessfulItems.Add(i);
                        service.UpdateState(checkpoint.Id, $"Step{i}Result", stepResult.OutputData ?? new object());
                    }
                    else
                    {
                        result.FailedItems.Add(i);
                        
                        if (step.IsRequired || options.StopOnFirstError)
                        {
                            service.Fail(checkpoint.Id, $"Step '{step.Name}' failed");
                            result.Success = false;
                            result.ErrorMessage = $"Step '{step.Name}' failed";
                            return result;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.FailedItems.Add(i);
                    
                    if (step.IsRequired || options.StopOnFirstError)
                    {
                        service.Fail(checkpoint.Id, ex.Message, ex);
                        result.Success = false;
                        result.ErrorMessage = ex.Message;
                        result.Exception = ex;
                        return result;
                    }
                    
                    ErrorLoggingService.Instance.Log(ex, $"ResumableOp:{operationType}:Step:{step.Name}");
                }
            }
            
            service.Complete(checkpoint.Id);
            result.Success = result.FailedItems.Count == 0;
            result.WasCompleted = true;
            return result;
        }
        catch (OperationCanceledException)
        {
            if (options.PauseOnCancel)
            {
                service.Pause(checkpoint.Id);
                result.WasPaused = true;
            }
            return result;
        }
        catch (Exception ex)
        {
            service.Fail(checkpoint.Id, ex.Message, ex);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;
            return result;
        }
    }
    
    /// <summary>
    /// Get resumable operations of a specific type.
    /// </summary>
    public static IReadOnlyList<OperationCheckpoint> GetResumable(string operationType)
    {
        return CheckpointService.Instance.GetResumable(operationType);
    }
    
    /// <summary>
    /// Check if there are resumable operations of a type.
    /// </summary>
    public static bool HasResumable(string operationType)
    {
        return CheckpointService.Instance.GetResumable(operationType).Count > 0;
    }
}

/// <summary>
/// Options for resumable operations.
/// </summary>
public record ResumableOperationOptions
{
    /// <summary>
    /// Stop processing on first item/step failure.
    /// </summary>
    public bool StopOnFirstError { get; init; } = false;
    
    /// <summary>
    /// Pause (not fail) when cancellation is requested.
    /// </summary>
    public bool PauseOnCancel { get; init; } = true;
    
    /// <summary>
    /// Delay between items in milliseconds.
    /// </summary>
    public int DelayBetweenItemsMs { get; init; } = 0;
    
    /// <summary>
    /// Progress callback.
    /// </summary>
    public Action<OperationProgress>? OnProgress { get; init; }
}

/// <summary>
/// Progress information for an operation.
/// </summary>
public record OperationProgress
{
    public int Current { get; init; }
    public int Total { get; init; }
    public double PercentComplete { get; init; }
    public string? StepName { get; init; }
    public object? CurrentItem { get; init; }
}

/// <summary>
/// Result of a resumable operation.
/// </summary>
public record ResumableOperationResult
{
    public bool Success { get; set; }
    public string? CheckpointId { get; init; }
    public int TotalItems { get; init; }
    public int StartIndex { get; init; }
    public int ProcessedItems { get; set; }
    public int LastProcessedIndex { get; set; } = -1;
    public bool WasPaused { get; set; }
    public bool WasCompleted { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public List<int> SuccessfulItems { get; } = new();
    public List<int> FailedItems { get; } = new();
    
    public bool CanResume => WasPaused && !WasCompleted;
    public int RemainingItems => TotalItems - (StartIndex + ProcessedItems);
}

/// <summary>
/// A single step in a multi-step operation.
/// </summary>
public class OperationStep
{
    public required string Name { get; init; }
    public required Func<CancellationToken, Task<StepResult>> Execute { get; init; }
    public bool IsRequired { get; init; } = true;
    
    public static OperationStep Create(
        string name, 
        Func<CancellationToken, Task<StepResult>> execute, 
        bool required = true)
    {
        return new OperationStep
        {
            Name = name,
            Execute = execute,
            IsRequired = required
        };
    }
    
    public static OperationStep Create(
        string name,
        Func<CancellationToken, Task> execute,
        bool required = true)
    {
        return new OperationStep
        {
            Name = name,
            Execute = async ct =>
            {
                await execute(ct);
                return StepResult.Ok();
            },
            IsRequired = required
        };
    }
}

/// <summary>
/// Result of a single step execution.
/// </summary>
public record StepResult
{
    public bool Success { get; init; }
    public object? OutputData { get; init; }
    public string? ErrorMessage { get; init; }
    
    public static StepResult Ok(object? data = null) => new() { Success = true, OutputData = data };
    public static StepResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}
