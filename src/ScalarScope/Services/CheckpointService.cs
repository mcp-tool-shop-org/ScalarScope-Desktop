// Phase 6.3.2: Operation Checkpoint System
// Enables saving and resuming long-running operations.

using System.Text.Json;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.3: Service for creating and managing operation checkpoints.
/// Enables resuming long-running operations after interruption.
/// </summary>
public sealed class CheckpointService
{
    private static readonly Lazy<CheckpointService> _instance = 
        new(() => new CheckpointService());
    
    public static CheckpointService Instance => _instance.Value;
    
    private readonly string _checkpointDirectory;
    private readonly Dictionary<string, OperationCheckpoint> _activeCheckpoints = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Maximum age of checkpoint files before cleanup.
    /// </summary>
    public TimeSpan MaxCheckpointAge { get; set; } = TimeSpan.FromDays(7);
    
    private CheckpointService()
    {
        _checkpointDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "checkpoints");
        
        Directory.CreateDirectory(_checkpointDirectory);
        CleanupOldCheckpoints();
    }
    
    /// <summary>
    /// Create a new checkpoint for an operation.
    /// </summary>
    public OperationCheckpoint CreateCheckpoint(
        string operationType,
        string operationId,
        Dictionary<string, object>? initialState = null)
    {
        var checkpoint = new OperationCheckpoint
        {
            Id = Guid.NewGuid().ToString("N"),
            OperationType = operationType,
            OperationId = operationId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            State = initialState ?? new Dictionary<string, object>(),
            Status = CheckpointStatus.InProgress
        };
        
        lock (_lock)
        {
            _activeCheckpoints[checkpoint.Id] = checkpoint;
        }
        
        SaveCheckpoint(checkpoint);
        return checkpoint;
    }
    
    /// <summary>
    /// Update checkpoint progress.
    /// </summary>
    public void UpdateProgress(
        string checkpointId,
        int currentStep,
        int totalSteps,
        string? stepDescription = null,
        Dictionary<string, object>? additionalState = null)
    {
        lock (_lock)
        {
            if (!_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                return;
            }
            
            checkpoint.CurrentStep = currentStep;
            checkpoint.TotalSteps = totalSteps;
            checkpoint.StepDescription = stepDescription;
            checkpoint.UpdatedAt = DateTime.UtcNow;
            checkpoint.Progress = totalSteps > 0 
                ? (double)currentStep / totalSteps 
                : 0;
            
            if (additionalState is not null)
            {
                foreach (var kvp in additionalState)
                {
                    checkpoint.State[kvp.Key] = kvp.Value;
                }
            }
            
            SaveCheckpoint(checkpoint);
        }
    }
    
    /// <summary>
    /// Update checkpoint state without changing progress.
    /// </summary>
    public void UpdateState(string checkpointId, string key, object value)
    {
        lock (_lock)
        {
            if (_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                checkpoint.State[key] = value;
                checkpoint.UpdatedAt = DateTime.UtcNow;
                SaveCheckpoint(checkpoint);
            }
        }
    }
    
    /// <summary>
    /// Mark checkpoint as completed successfully.
    /// </summary>
    public void Complete(string checkpointId, Dictionary<string, object>? resultState = null)
    {
        lock (_lock)
        {
            if (_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                checkpoint.Status = CheckpointStatus.Completed;
                checkpoint.CompletedAt = DateTime.UtcNow;
                checkpoint.UpdatedAt = DateTime.UtcNow;
                checkpoint.Progress = 1.0;
                
                if (resultState is not null)
                {
                    foreach (var kvp in resultState)
                    {
                        checkpoint.State[kvp.Key] = kvp.Value;
                    }
                }
                
                SaveCheckpoint(checkpoint);
                _activeCheckpoints.Remove(checkpointId);
            }
        }
    }
    
    /// <summary>
    /// Mark checkpoint as failed.
    /// </summary>
    public void Fail(string checkpointId, string errorMessage, Exception? exception = null)
    {
        lock (_lock)
        {
            if (_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                checkpoint.Status = CheckpointStatus.Failed;
                checkpoint.UpdatedAt = DateTime.UtcNow;
                checkpoint.ErrorMessage = errorMessage;
                checkpoint.ErrorType = exception?.GetType().FullName;
                
                SaveCheckpoint(checkpoint);
            }
        }
    }
    
    /// <summary>
    /// Mark checkpoint as paused (user-initiated).
    /// </summary>
    public void Pause(string checkpointId)
    {
        lock (_lock)
        {
            if (_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                checkpoint.Status = CheckpointStatus.Paused;
                checkpoint.UpdatedAt = DateTime.UtcNow;
                SaveCheckpoint(checkpoint);
                _activeCheckpoints.Remove(checkpointId);
            }
        }
    }
    
    /// <summary>
    /// Get a checkpoint by ID.
    /// </summary>
    public OperationCheckpoint? GetCheckpoint(string checkpointId)
    {
        // Check memory first
        lock (_lock)
        {
            if (_activeCheckpoints.TryGetValue(checkpointId, out var checkpoint))
            {
                return checkpoint;
            }
        }
        
        // Check disk
        return LoadCheckpoint(checkpointId);
    }
    
    /// <summary>
    /// Get all resumable checkpoints for an operation type.
    /// </summary>
    public IReadOnlyList<OperationCheckpoint> GetResumable(string operationType)
    {
        var resumable = new List<OperationCheckpoint>();
        
        var files = Directory.GetFiles(_checkpointDirectory, "*.checkpoint.json");
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<OperationCheckpoint>(json);
                
                if (checkpoint is not null && 
                    checkpoint.OperationType == operationType &&
                    checkpoint.Status is CheckpointStatus.Paused or CheckpointStatus.Failed or CheckpointStatus.InProgress)
                {
                    resumable.Add(checkpoint);
                }
            }
            catch
            {
                // Skip invalid checkpoint files
            }
        }
        
        return resumable.OrderByDescending(c => c.UpdatedAt).ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Get all checkpoints regardless of status.
    /// </summary>
    public IReadOnlyList<OperationCheckpoint> GetAll()
    {
        var all = new List<OperationCheckpoint>();
        
        var files = Directory.GetFiles(_checkpointDirectory, "*.checkpoint.json");
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var checkpoint = JsonSerializer.Deserialize<OperationCheckpoint>(json);
                if (checkpoint is not null)
                {
                    all.Add(checkpoint);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return all.OrderByDescending(c => c.UpdatedAt).ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Delete a checkpoint.
    /// </summary>
    public void Delete(string checkpointId)
    {
        lock (_lock)
        {
            _activeCheckpoints.Remove(checkpointId);
        }
        
        var path = GetCheckpointPath(checkpointId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    
    /// <summary>
    /// Delete all checkpoints for an operation type.
    /// </summary>
    public void DeleteAll(string operationType)
    {
        var toDelete = GetAll()
            .Where(c => c.OperationType == operationType)
            .ToList();
        
        foreach (var checkpoint in toDelete)
        {
            Delete(checkpoint.Id);
        }
    }
    
    /// <summary>
    /// Resume a paused/failed checkpoint by marking it in-progress.
    /// </summary>
    public OperationCheckpoint? Resume(string checkpointId)
    {
        var checkpoint = LoadCheckpoint(checkpointId);
        if (checkpoint is null) return null;
        
        checkpoint.Status = CheckpointStatus.InProgress;
        checkpoint.UpdatedAt = DateTime.UtcNow;
        checkpoint.ResumeCount++;
        checkpoint.ErrorMessage = null;
        checkpoint.ErrorType = null;
        
        lock (_lock)
        {
            _activeCheckpoints[checkpointId] = checkpoint;
        }
        
        SaveCheckpoint(checkpoint);
        return checkpoint;
    }
    
    private void SaveCheckpoint(OperationCheckpoint checkpoint)
    {
        try
        {
            var path = GetCheckpointPath(checkpoint.Id);
            var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(
                ErrorSeverity.Warning,
                $"Failed to save checkpoint: {ex.Message}",
                "CheckpointService");
        }
    }
    
    private OperationCheckpoint? LoadCheckpoint(string checkpointId)
    {
        try
        {
            var path = GetCheckpointPath(checkpointId);
            if (!File.Exists(path)) return null;
            
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OperationCheckpoint>(json);
        }
        catch
        {
            return null;
        }
    }
    
    private string GetCheckpointPath(string checkpointId)
    {
        return Path.Combine(_checkpointDirectory, $"{checkpointId}.checkpoint.json");
    }
    
    private void CleanupOldCheckpoints()
    {
        try
        {
            var cutoff = DateTime.UtcNow - MaxCheckpointAge;
            var files = Directory.GetFiles(_checkpointDirectory, "*.checkpoint.json");
            
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Cleanup failures are non-critical
        }
    }
}

/// <summary>
/// Represents a saved checkpoint for an operation.
/// </summary>
public class OperationCheckpoint
{
    public string Id { get; set; } = "";
    public string OperationType { get; set; } = "";
    public string OperationId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public CheckpointStatus Status { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double Progress { get; set; }
    public string? StepDescription { get; set; }
    public Dictionary<string, object> State { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ErrorType { get; set; }
    public int ResumeCount { get; set; }
    
    /// <summary>
    /// Get typed state value.
    /// </summary>
    public T? GetState<T>(string key)
    {
        if (!State.TryGetValue(key, out var value)) return default;
        
        // Handle JsonElement from deserialization
        if (value is JsonElement element)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(element.GetRawText());
            }
            catch
            {
                return default;
            }
        }
        
        if (value is T typed) return typed;
        return default;
    }
    
    /// <summary>
    /// Check if checkpoint can be resumed.
    /// </summary>
    public bool CanResume => Status is CheckpointStatus.Paused or CheckpointStatus.Failed;
    
    /// <summary>
    /// Get human-readable status description.
    /// </summary>
    public string StatusDescription => Status switch
    {
        CheckpointStatus.InProgress => $"In progress ({Progress:P0})",
        CheckpointStatus.Paused => $"Paused at {Progress:P0}",
        CheckpointStatus.Completed => "Completed",
        CheckpointStatus.Failed => $"Failed: {ErrorMessage ?? "Unknown error"}",
        CheckpointStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };
}

/// <summary>
/// Status of a checkpoint.
/// </summary>
public enum CheckpointStatus
{
    InProgress,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Extension methods for checkpoint-enabled operations.
/// </summary>
public static class CheckpointExtensions
{
    /// <summary>
    /// Create a checkpoint scope that auto-completes or fails.
    /// </summary>
    public static CheckpointScope BeginCheckpoint(
        this CheckpointService service,
        string operationType,
        string operationId,
        Dictionary<string, object>? initialState = null)
    {
        var checkpoint = service.CreateCheckpoint(operationType, operationId, initialState);
        return new CheckpointScope(service, checkpoint);
    }
}

/// <summary>
/// Disposable scope for automatic checkpoint management.
/// </summary>
public class CheckpointScope : IDisposable
{
    private readonly CheckpointService _service;
    private readonly OperationCheckpoint _checkpoint;
    private bool _completed;
    private bool _disposed;
    
    public string CheckpointId => _checkpoint.Id;
    
    internal CheckpointScope(CheckpointService service, OperationCheckpoint checkpoint)
    {
        _service = service;
        _checkpoint = checkpoint;
    }
    
    /// <summary>
    /// Report progress.
    /// </summary>
    public void Progress(int current, int total, string? description = null)
    {
        _service.UpdateProgress(_checkpoint.Id, current, total, description);
    }
    
    /// <summary>
    /// Update state.
    /// </summary>
    public void SetState(string key, object value)
    {
        _service.UpdateState(_checkpoint.Id, key, value);
    }
    
    /// <summary>
    /// Mark as completed.
    /// </summary>
    public void Complete(Dictionary<string, object>? result = null)
    {
        _service.Complete(_checkpoint.Id, result);
        _completed = true;
    }
    
    /// <summary>
    /// Mark as paused (for user-initiated pause).
    /// </summary>
    public void Pause()
    {
        _service.Pause(_checkpoint.Id);
        _completed = true;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // If not explicitly completed or paused, mark as failed
        if (!_completed)
        {
            _service.Fail(_checkpoint.Id, "Operation did not complete normally");
        }
    }
}
