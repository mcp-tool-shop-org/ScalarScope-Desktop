// Phase 6.3.4: Auto-Save Service
// Automatic state persistence for comparison sessions.

using System.Text.Json;
using System.Timers;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.3: Service for automatic state persistence.
/// Saves comparison session state at regular intervals and on key events.
/// </summary>
public sealed class AutoSaveService : IDisposable
{
    private static readonly Lazy<AutoSaveService> _instance = 
        new(() => new AutoSaveService());
    
    public static AutoSaveService Instance => _instance.Value;
    
    private readonly string _autoSaveDirectory;
    private readonly System.Timers.Timer _autoSaveTimer;
    private readonly object _lock = new();
    
    private AutoSaveSessionState? _currentState;
    private string? _currentSessionId;
    private DateTime _lastSaveTime;
    private bool _isDirty;
    private bool _isEnabled = true;
    private bool _disposed;
    
    /// <summary>
    /// Auto-save interval in seconds.
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Maximum auto-save files to keep.
    /// </summary>
    public int MaxAutoSaveFiles { get; set; } = 10;
    
    /// <summary>
    /// Whether auto-save is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            if (value)
            {
                _autoSaveTimer.Start();
            }
            else
            {
                _autoSaveTimer.Stop();
            }
        }
    }
    
    /// <summary>
    /// Event fired when state is auto-saved.
    /// </summary>
    public event EventHandler<AutoSaveEventArgs>? StateSaved;
    
    /// <summary>
    /// Event fired when state is restored.
    /// </summary>
    public event EventHandler<AutoSaveEventArgs>? StateRestored;
    
    private AutoSaveService()
    {
        _autoSaveDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ScalarScope", "autosave");
        
        Directory.CreateDirectory(_autoSaveDirectory);
        
        _autoSaveTimer = new System.Timers.Timer(AutoSaveIntervalSeconds * 1000);
        _autoSaveTimer.Elapsed += OnAutoSaveTimer;
        _autoSaveTimer.AutoReset = true;
        _autoSaveTimer.Start();
    }
    
    /// <summary>
    /// Start tracking a new session.
    /// </summary>
    public void StartSession(string? sessionName = null)
    {
        lock (_lock)
        {
            _currentSessionId = Guid.NewGuid().ToString("N")[..8];
            _currentState = new AutoSaveSessionState
            {
                SessionId = _currentSessionId,
                SessionName = sessionName ?? $"Session {DateTime.Now:HH:mm}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// Update session state with comparison info.
    /// </summary>
    public void UpdateComparison(
        string? trajectory1Path,
        string? trajectory2Path,
        int? timestepCount = null,
        Dictionary<string, object>? additionalState = null)
    {
        lock (_lock)
        {
            if (_currentState is null) return;
            
            _currentState.Trajectory1Path = trajectory1Path;
            _currentState.Trajectory2Path = trajectory2Path;
            _currentState.TimestepCount = timestepCount;
            _currentState.UpdatedAt = DateTime.UtcNow;
            
            if (additionalState is not null)
            {
                foreach (var kvp in additionalState)
                {
                    _currentState.CustomState[kvp.Key] = kvp.Value;
                }
            }
            
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// Update view state (scroll position, zoom, etc.).
    /// </summary>
    public void UpdateViewState(
        int? currentTimestep = null,
        float? zoomLevel = null,
        float? scrollPosition = null,
        string? selectedScalar = null)
    {
        lock (_lock)
        {
            if (_currentState is null) return;
            
            if (currentTimestep.HasValue)
                _currentState.CurrentTimestep = currentTimestep.Value;
            if (zoomLevel.HasValue)
                _currentState.ZoomLevel = zoomLevel.Value;
            if (scrollPosition.HasValue)
                _currentState.ScrollPosition = scrollPosition.Value;
            if (selectedScalar is not null)
                _currentState.SelectedScalar = selectedScalar;
            
            _currentState.UpdatedAt = DateTime.UtcNow;
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// Update custom state data.
    /// </summary>
    public void UpdateCustomState(string key, object value)
    {
        lock (_lock)
        {
            if (_currentState is null) return;
            
            _currentState.CustomState[key] = value;
            _currentState.UpdatedAt = DateTime.UtcNow;
            _isDirty = true;
        }
    }
    
    /// <summary>
    /// Force an immediate save.
    /// </summary>
    public void SaveNow()
    {
        PerformSave();
    }
    
    /// <summary>
    /// End current session and save final state.
    /// </summary>
    public void EndSession()
    {
        lock (_lock)
        {
            if (_currentState is not null)
            {
                _currentState.UpdatedAt = DateTime.UtcNow;
                _isDirty = true;
                PerformSave();
            }
            
            _currentState = null;
            _currentSessionId = null;
        }
    }
    
    /// <summary>
    /// Get list of available auto-saves.
    /// </summary>
    public IReadOnlyList<AutoSaveSessionState> GetAvailableSaves()
    {
        var saves = new List<AutoSaveSessionState>();
        
        var files = Directory.GetFiles(_autoSaveDirectory, "*.autosave.json")
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc);
        
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var state = JsonSerializer.Deserialize<AutoSaveSessionState>(json);
                if (state is not null)
                {
                    saves.Add(state);
                }
            }
            catch
            {
                // Skip invalid files
            }
        }
        
        return saves.AsReadOnly();
    }
    
    /// <summary>
    /// Get the most recent auto-save.
    /// </summary>
    public AutoSaveSessionState? GetLatestSave()
    {
        return GetAvailableSaves().FirstOrDefault();
    }
    
    /// <summary>
    /// Restore a session state.
    /// </summary>
    public AutoSaveSessionState? RestoreSession(string sessionId)
    {
        var file = Path.Combine(_autoSaveDirectory, $"{sessionId}.autosave.json");
        if (!File.Exists(file)) return null;
        
        try
        {
            var json = File.ReadAllText(file);
            var state = JsonSerializer.Deserialize<AutoSaveSessionState>(json);
            
            if (state is not null)
            {
                lock (_lock)
                {
                    _currentState = state;
                    _currentSessionId = state.SessionId;
                    _isDirty = false;
                }
                
                StateRestored?.Invoke(this, new AutoSaveEventArgs
                {
                    SessionId = state.SessionId,
                    Timestamp = DateTime.UtcNow
                });
                
                return state;
            }
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "AutoSave.Restore");
        }
        
        return null;
    }
    
    /// <summary>
    /// Delete an auto-save.
    /// </summary>
    public void DeleteSave(string sessionId)
    {
        var file = Path.Combine(_autoSaveDirectory, $"{sessionId}.autosave.json");
        if (File.Exists(file))
        {
            File.Delete(file);
        }
    }
    
    /// <summary>
    /// Delete all auto-saves.
    /// </summary>
    public void ClearAllSaves()
    {
        var files = Directory.GetFiles(_autoSaveDirectory, "*.autosave.json");
        foreach (var file in files)
        {
            try { File.Delete(file); } catch { }
        }
    }
    
    /// <summary>
    /// Check if there are any unsaved changes.
    /// </summary>
    public bool HasUnsavedChanges => _isDirty;
    
    /// <summary>
    /// Get current session ID.
    /// </summary>
    public string? CurrentSessionId => _currentSessionId;
    
    private void OnAutoSaveTimer(object? sender, ElapsedEventArgs e)
    {
        if (_isEnabled && _isDirty)
        {
            PerformSave();
        }
    }
    
    private void PerformSave()
    {
        AutoSaveSessionState? stateToSave;
        string? sessionId;
        
        lock (_lock)
        {
            if (_currentState is null || !_isDirty) return;
            
            stateToSave = _currentState with { UpdatedAt = DateTime.UtcNow };
            sessionId = _currentSessionId;
            _isDirty = false;
            _lastSaveTime = DateTime.UtcNow;
        }
        
        if (stateToSave is null || sessionId is null) return;
        
        try
        {
            var path = Path.Combine(_autoSaveDirectory, $"{sessionId}.autosave.json");
            var json = JsonSerializer.Serialize(stateToSave, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(path, json);
            
            CleanupOldSaves();
            
            StateSaved?.Invoke(this, new AutoSaveEventArgs
            {
                SessionId = sessionId,
                Timestamp = DateTime.UtcNow,
                FilePath = path
            });
        }
        catch (Exception ex)
        {
            ErrorLoggingService.Instance.Log(ex, "AutoSave.Save");
            
            // Mark dirty again so we retry
            lock (_lock)
            {
                _isDirty = true;
            }
        }
    }
    
    private void CleanupOldSaves()
    {
        try
        {
            var files = Directory.GetFiles(_autoSaveDirectory, "*.autosave.json")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Skip(MaxAutoSaveFiles)
                .ToList();
            
            foreach (var file in files)
            {
                // Don't delete current session
                if (_currentSessionId is not null && 
                    file.Name.Contains(_currentSessionId))
                {
                    continue;
                }
                
                file.Delete();
            }
        }
        catch
        {
            // Cleanup failures are non-critical
        }
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        // Save any pending changes
        if (_isDirty)
        {
            PerformSave();
        }
        
        _autoSaveTimer.Stop();
        _autoSaveTimer.Dispose();
    }
}

/// <summary>
/// Saved auto-save session state.
/// </summary>
public record AutoSaveSessionState
{
    public string SessionId { get; init; } = "";
    public string SessionName { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
    
    // Comparison data
    public string? Trajectory1Path { get; set; }
    public string? Trajectory2Path { get; set; }
    public int? TimestepCount { get; set; }
    
    // View state
    public int CurrentTimestep { get; set; }
    public float ZoomLevel { get; set; } = 1.0f;
    public float ScrollPosition { get; set; }
    public string? SelectedScalar { get; set; }
    
    // Custom state data
    public Dictionary<string, object> CustomState { get; init; } = new();
    
    /// <summary>
    /// Get typed custom state value.
    /// </summary>
    public T? GetCustomState<T>(string key)
    {
        if (!CustomState.TryGetValue(key, out var value)) return default;
        
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
    /// Get summary for display.
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrEmpty(Trajectory1Path))
        {
            parts.Add($"T1: {Path.GetFileName(Trajectory1Path)}");
        }
        if (!string.IsNullOrEmpty(Trajectory2Path))
        {
            parts.Add($"T2: {Path.GetFileName(Trajectory2Path)}");
        }
        if (TimestepCount.HasValue)
        {
            parts.Add($"{TimestepCount} timesteps");
        }
        
        return parts.Count > 0 
            ? string.Join(" | ", parts) 
            : "Empty session";
    }
}

/// <summary>
/// Event args for auto-save events.
/// </summary>
public class AutoSaveEventArgs : EventArgs
{
    public required string SessionId { get; init; }
    public DateTime Timestamp { get; init; }
    public string? FilePath { get; init; }
}
