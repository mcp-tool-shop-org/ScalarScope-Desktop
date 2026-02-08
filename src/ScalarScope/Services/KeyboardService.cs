using ScalarScope.ViewModels;

namespace ScalarScope.Services;

/// <summary>
/// Handles global keyboard shortcuts for the application.
/// Note: Full keyboard support requires platform-specific implementation.
/// This service provides the action handlers that can be wired to platform events.
/// </summary>
public class KeyboardService
{
    private readonly VortexSessionViewModel _session;
    private readonly ExportService _exportService;

    public event Action<string>? ShortcutTriggered;

    public KeyboardService(VortexSessionViewModel session)
    {
        _session = session;
        _exportService = new ExportService();
    }

    /// <summary>
    /// Handle a key press. Returns true if handled.
    /// </summary>
    public bool HandleKeyPress(string key, bool ctrl = false, bool shift = false)
    {
        // Playback controls
        if (key == "Space")
        {
            _session.Player.PlayPauseCommand.Execute(null);
            var state = _session.Player.IsPlaying ? "Playing" : "Paused";
            ShortcutTriggered?.Invoke(state);
            return true;
        }

        if (key == "Left")
        {
            // Shift+Left for fine step (0.1%), regular Left for normal step (1%)
            var stepSize = shift ? 0.001 : 0.01;
            _session.Player.JumpToTimeCommand.Execute(Math.Max(0.0, _session.Player.Time - stepSize));
            var frame = _session.Player.CurrentFrame;
            ShortcutTriggered?.Invoke($"Frame {frame}");
            return true;
        }

        if (key == "Right")
        {
            // Shift+Right for fine step (0.1%), regular Right for normal step (1%)
            var stepSize = shift ? 0.001 : 0.01;
            _session.Player.JumpToTimeCommand.Execute(Math.Min(1.0, _session.Player.Time + stepSize));
            var frame = _session.Player.CurrentFrame;
            ShortcutTriggered?.Invoke($"Frame {frame}");
            return true;
        }

        if (key == "Home")
        {
            _session.Player.JumpToTimeCommand.Execute(0.0);
            ShortcutTriggered?.Invoke("Start");
            return true;
        }

        if (key == "End")
        {
            _session.Player.JumpToTimeCommand.Execute(1.0);
            ShortcutTriggered?.Invoke("End");
            return true;
        }

        // Speed controls with visual feedback (Up/Down arrows or +/-)
        if (key == "OemPlus" || key == "Add" || key == "Up")
        {
            _session.Player.IncreaseSpeed();
            ShortcutTriggered?.Invoke(_session.Player.SpeedDisplay);
            return true;
        }

        if (key == "OemMinus" || key == "Subtract" || key == "Down")
        {
            _session.Player.DecreaseSpeed();
            ShortcutTriggered?.Invoke(_session.Player.SpeedDisplay);
            return true;
        }

        // Help shortcut (?)
        if (key == "OemQuestion")
        {
            Shell.Current.GoToAsync("//help");
            ShortcutTriggered?.Invoke("Help");
            return true;
        }

        // Reset speed to 1x
        if (key == "D0" && !ctrl)
        {
            _session.Player.SetSpeed(1.0);
            ShortcutTriggered?.Invoke("Speed: 1×");
            return true;
        }

        // Export shortcut
        if (key == "S" && !ctrl)
        {
            _ = SafeQuickExportAsync();
            return true;
        }

        // Ctrl+S for save/export dialog
        if (key == "S" && ctrl)
        {
            _ = SafeQuickExportAsync();
            ShortcutTriggered?.Invoke("Screenshot saved");
            return true;
        }

        // Ctrl+E for export (common convention)
        if (key == "E" && ctrl)
        {
            _ = SafeQuickExportAsync();
            ShortcutTriggered?.Invoke("Screenshot saved");
            return true;
        }

        // Tab navigation (1-6)
        if (key is "D1" or "D2" or "D3" or "D4" or "D5" or "D6")
        {
            var tabIndex = int.Parse(key[1..]) - 1;
            NavigateToTab(tabIndex);
            return true;
        }

        return false;
    }

    private async Task SafeQuickExportAsync()
    {
        try
        {
            await QuickExportAsync();
        }
        catch (Exception ex)
        {
            ShortcutTriggered?.Invoke($"Export failed: {ex.Message}");
        }
    }

    private async Task QuickExportAsync()
    {
        if (_session.Run == null) return;

        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var scalarScopeExports = Path.Combine(documentsPath, "ScalarScope Exports");
        Directory.CreateDirectory(scalarScopeExports);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputPath = Path.Combine(scalarScopeExports, $"scalarscope_quick_{timestamp}.png");

        await _exportService.ExportStillAsync(_session.Run, _session.Player.Time, outputPath);

        ShortcutTriggered?.Invoke($"Saved: {Path.GetFileName(outputPath)}");
    }

    private static void NavigateToTab(int index)
    {
        var routes = new[] { "overview", "trajectory", "scalars", "geometry", "compare", "failures" };
        if (index >= 0 && index < routes.Length)
        {
            Shell.Current.GoToAsync($"//{routes[index]}");
        }
    }

    private static string GetSpeedLabel(int speedIndex) => speedIndex switch
    {
        0 => "0.25x",
        1 => "0.5x",
        2 => "1x",
        3 => "2x",
        4 => "4x",
        _ => "1x"
    };
}
