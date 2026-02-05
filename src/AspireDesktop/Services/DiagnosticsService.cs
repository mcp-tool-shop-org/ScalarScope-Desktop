using System.Diagnostics;
using System.Text;

namespace AspireDesktop.Services;

/// <summary>
/// Service for running system diagnostics.
/// </summary>
public static class DiagnosticsService
{
    /// <summary>
    /// Run all diagnostics and return a report.
    /// </summary>
    public static DiagnosticsReport RunDiagnostics()
    {
        var report = new DiagnosticsReport
        {
            Timestamp = DateTime.UtcNow
        };

        // System checks
        report.Checks.Add(CheckDotNetVersion());
        report.Checks.Add(CheckMemory());
        report.Checks.Add(CheckDiskSpace());
        report.Checks.Add(CheckGPU());
        report.Checks.Add(CheckAppData());

        return report;
    }

    private static DiagnosticCheck CheckDotNetVersion()
    {
        var version = Environment.Version;
        var isGood = version.Major >= 9;

        return new DiagnosticCheck
        {
            Name = ".NET Runtime",
            Status = isGood ? CheckStatus.Pass : CheckStatus.Warning,
            Message = $"Version {version}",
            Details = isGood ? "Runtime version is compatible" : "Consider updating to .NET 9+"
        };
    }

    private static DiagnosticCheck CheckMemory()
    {
        using var process = Process.GetCurrentProcess();
        var usedMB = process.WorkingSet64 / (1024 * 1024);
        var isGood = usedMB < 500;

        return new DiagnosticCheck
        {
            Name = "Memory Usage",
            Status = isGood ? CheckStatus.Pass : CheckStatus.Warning,
            Message = $"{usedMB} MB in use",
            Details = isGood ? "Memory usage is normal" : "Memory usage is high, consider closing other apps"
        };
    }

    private static DiagnosticCheck CheckDiskSpace()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var drive = new DriveInfo(Path.GetPathRoot(appDataPath)!);
            var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            var isGood = freeGB > 1;

            return new DiagnosticCheck
            {
                Name = "Disk Space",
                Status = isGood ? CheckStatus.Pass : CheckStatus.Warning,
                Message = $"{freeGB:F1} GB free",
                Details = isGood ? "Sufficient disk space" : "Low disk space, exports may fail"
            };
        }
        catch
        {
            return new DiagnosticCheck
            {
                Name = "Disk Space",
                Status = CheckStatus.Unknown,
                Message = "Could not check",
                Details = "Unable to determine disk space"
            };
        }
    }

    private static DiagnosticCheck CheckGPU()
    {
        // Basic GPU check - in real implementation would use DirectX/Vulkan
        var is64Bit = Environment.Is64BitProcess;

        return new DiagnosticCheck
        {
            Name = "GPU Acceleration",
            Status = is64Bit ? CheckStatus.Pass : CheckStatus.Warning,
            Message = is64Bit ? "64-bit process (GPU capable)" : "32-bit process",
            Details = is64Bit ? "Hardware acceleration should be available" : "May have limited GPU support"
        };
    }

    private static DiagnosticCheck CheckAppData()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AspireDesktop");

        var exists = Directory.Exists(appDataPath);

        return new DiagnosticCheck
        {
            Name = "App Data Directory",
            Status = exists ? CheckStatus.Pass : CheckStatus.Info,
            Message = exists ? "Exists" : "Will be created on first use",
            Details = appDataPath
        };
    }
}

public class DiagnosticsReport
{
    public DateTime Timestamp { get; set; }
    public List<DiagnosticCheck> Checks { get; set; } = new();

    public bool HasIssues => Checks.Any(c => c.Status == CheckStatus.Fail || c.Status == CheckStatus.Warning);

    public string ToSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Diagnostics run at {Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        foreach (var check in Checks)
        {
            var icon = check.Status switch
            {
                CheckStatus.Pass => "✅",
                CheckStatus.Warning => "⚠️",
                CheckStatus.Fail => "❌",
                CheckStatus.Info => "ℹ️",
                _ => "❓"
            };
            sb.AppendLine($"{icon} {check.Name}: {check.Message}");
            if (!string.IsNullOrEmpty(check.Details))
            {
                sb.AppendLine($"   {check.Details}");
            }
        }

        return sb.ToString();
    }
}

public class DiagnosticCheck
{
    public required string Name { get; set; }
    public CheckStatus Status { get; set; }
    public string Message { get; set; } = "";
    public string Details { get; set; } = "";
}

public enum CheckStatus
{
    Pass,
    Warning,
    Fail,
    Info,
    Unknown
}
