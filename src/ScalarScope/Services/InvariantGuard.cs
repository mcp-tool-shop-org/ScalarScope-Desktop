using System.Diagnostics;
using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Encodes functional invariants that must hold throughout the application.
/// Soft-fails in release (logs warning, disables feature).
/// Hard-fails in debug (asserts / throws).
/// </summary>
public static class InvariantGuard
{
    /// <summary>
    /// Event fired when an invariant violation is detected.
    /// UI can subscribe to show warnings.
    /// </summary>
    public static event Action<InvariantViolation>? ViolationDetected;

    /// <summary>
    /// Recent violations for diagnostics.
    /// </summary>
    private static readonly List<InvariantViolation> RecentViolations = new();
    private const int MaxStoredViolations = 50;

    // ═══════════════════════════════════════════════════════════════════════
    // TIME INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Time must always be in [0, 1].
    /// </summary>
    public static double ClampTime(double t, string context = "unknown")
    {
        if (t >= 0.0 && t <= 1.0)
            return t;

        var clamped = Math.Clamp(t, 0.0, 1.0);
        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.TimeInRange,
            Message = $"Time {t:F4} outside [0,1] range",
            Context = context,
            Severity = InvariantSeverity.Warning,
            AutoCorrected = true,
            CorrectedValue = clamped.ToString("F4")
        });

        return clamped;
    }

    /// <summary>
    /// Assert time is valid without correction (for read-only checks).
    /// </summary>
    public static bool AssertTimeValid(double t, string context = "unknown")
    {
        if (t >= 0.0 && t <= 1.0)
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.TimeInRange,
            Message = $"Time {t:F4} outside [0,1] range",
            Context = context,
            Severity = InvariantSeverity.Error
        });

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TRAJECTORY INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Trajectory must have at least one timestep before rendering.
    /// </summary>
    public static bool AssertTrajectoryNonEmpty(GeometryRun? run, string context = "unknown")
    {
        if (run?.Trajectory?.Timesteps is { Count: > 0 })
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.TrajectoryNonEmpty,
            Message = "Attempted to render empty trajectory",
            Context = context,
            Severity = InvariantSeverity.Error
        });

        return false;
    }

    /// <summary>
    /// Trajectory timesteps should be monotonically increasing in t.
    /// </summary>
    public static bool AssertTrajectoryMonotonic(IList<TrajectoryTimestep>? timesteps, string context = "unknown")
    {
        if (timesteps == null || timesteps.Count < 2)
            return true;

        for (int i = 1; i < timesteps.Count; i++)
        {
            if (timesteps[i].T < timesteps[i - 1].T)
            {
                ReportViolation(new InvariantViolation
                {
                    Rule = InvariantRule.TrajectoryMonotonic,
                    Message = $"Trajectory t[{i}]={timesteps[i].T:F4} < t[{i - 1}]={timesteps[i - 1].T:F4}",
                    Context = context,
                    Severity = InvariantSeverity.Warning
                });
                return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // EIGENVALUE INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Eigenvalues should be sorted descending (λ₁ ≥ λ₂ ≥ ... ≥ λₙ).
    /// </summary>
    public static bool AssertEigenvaluesSorted(IList<double>? eigenvalues, string context = "unknown")
    {
        if (eigenvalues == null || eigenvalues.Count < 2)
            return true;

        for (int i = 1; i < eigenvalues.Count; i++)
        {
            if (eigenvalues[i] > eigenvalues[i - 1] + 0.001) // Small epsilon for float comparison
            {
                ReportViolation(new InvariantViolation
                {
                    Rule = InvariantRule.EigenvaluesSorted,
                    Message = $"Eigenvalues not sorted: λ[{i}]={eigenvalues[i]:F4} > λ[{i - 1}]={eigenvalues[i - 1]:F4}",
                    Context = context,
                    Severity = InvariantSeverity.Warning
                });
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Eigenvalues should be non-negative.
    /// </summary>
    public static bool AssertEigenvaluesNonNegative(IList<double>? eigenvalues, string context = "unknown")
    {
        if (eigenvalues == null)
            return true;

        for (int i = 0; i < eigenvalues.Count; i++)
        {
            if (eigenvalues[i] < -0.001) // Small epsilon for float comparison
            {
                ReportViolation(new InvariantViolation
                {
                    Rule = InvariantRule.EigenvaluesNonNegative,
                    Message = $"Negative eigenvalue: λ[{i}]={eigenvalues[i]:F4}",
                    Context = context,
                    Severity = InvariantSeverity.Warning
                });
                return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DIMENSION INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Effective dimension must be ≤ total dimensions.
    /// </summary>
    public static bool AssertEffectiveDimValid(double effectiveDim, int totalDim, string context = "unknown")
    {
        if (effectiveDim <= totalDim + 0.001)
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.EffectiveDimBounded,
            Message = $"Effective dimension {effectiveDim:F2} > total dimensions {totalDim}",
            Context = context,
            Severity = InvariantSeverity.Warning
        });

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMPARISON VIEW INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Compare view requires both runs to be valid before rendering.
    /// </summary>
    public static bool AssertCompareRunsValid(GeometryRun? leftRun, GeometryRun? rightRun, string context = "unknown")
    {
        var leftValid = leftRun?.Trajectory?.Timesteps is { Count: > 0 };
        var rightValid = rightRun?.Trajectory?.Timesteps is { Count: > 0 };

        if (leftValid && rightValid)
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.CompareRunsValid,
            Message = $"Compare view requires both runs (left={leftValid}, right={rightValid})",
            Context = context,
            Severity = InvariantSeverity.Error
        });

        return false;
    }

    /// <summary>
    /// Compare view time must be synchronized (delta = 0).
    /// </summary>
    public static bool AssertCompareSynchronized(double leftTime, double rightTime, string context = "unknown")
    {
        var delta = Math.Abs(leftTime - rightTime);
        if (delta < 0.001)
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.CompareTimeSynced,
            Message = $"Compare time desync: left={leftTime:F4}, right={rightTime:F4}, delta={delta:F4}",
            Context = context,
            Severity = InvariantSeverity.Warning
        });

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DATA INTEGRITY INVARIANTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Run data arrays should have consistent lengths.
    /// </summary>
    public static bool AssertDataConsistentLengths(GeometryRun? run, string context = "unknown")
    {
        if (run == null)
            return true;

        var trajectoryCount = run.Trajectory?.Timesteps?.Count ?? 0;
        var scalarsCount = run.Scalars?.Values?.Count ?? 0;
        var eigenCount = run.Geometry?.Eigenvalues?.Count ?? 0;

        // Allow some flexibility - not all data series need same exact count
        // But they should be in the same order of magnitude
        var counts = new[] { trajectoryCount, scalarsCount, eigenCount }.Where(c => c > 0).ToList();

        if (counts.Count < 2)
            return true; // Not enough data to compare

        var min = counts.Min();
        var max = counts.Max();

        if (max <= min * 2) // Allow 2x difference
            return true;

        ReportViolation(new InvariantViolation
        {
            Rule = InvariantRule.DataLengthsConsistent,
            Message = $"Data length mismatch: trajectory={trajectoryCount}, scalars={scalarsCount}, eigen={eigenCount}",
            Context = context,
            Severity = InvariantSeverity.Warning
        });

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIOLATION REPORTING
    // ═══════════════════════════════════════════════════════════════════════

    private static void ReportViolation(InvariantViolation violation)
    {
        violation.Timestamp = DateTime.UtcNow;

        // Store for diagnostics
        lock (RecentViolations)
        {
            RecentViolations.Add(violation);
            if (RecentViolations.Count > MaxStoredViolations)
                RecentViolations.RemoveAt(0);
        }

        // Log
        var prefix = violation.Severity == InvariantSeverity.Error ? "ERROR" : "WARN";
        Debug.WriteLine($"[Invariant {prefix}] {violation.Rule}: {violation.Message} (context: {violation.Context})");

        // Notify subscribers
        ViolationDetected?.Invoke(violation);

        // In debug mode, break on errors
#if DEBUG
        if (violation.Severity == InvariantSeverity.Error)
        {
            Debug.Fail($"Invariant violation: {violation.Rule} - {violation.Message}");
        }
#endif
    }

    /// <summary>
    /// Get recent violations for diagnostics.
    /// </summary>
    public static IReadOnlyList<InvariantViolation> GetRecentViolations()
    {
        lock (RecentViolations)
        {
            return RecentViolations.ToList();
        }
    }

    /// <summary>
    /// Clear stored violations.
    /// </summary>
    public static void ClearViolations()
    {
        lock (RecentViolations)
        {
            RecentViolations.Clear();
        }
    }

    /// <summary>
    /// Check if any errors have occurred recently.
    /// </summary>
    public static bool HasRecentErrors()
    {
        lock (RecentViolations)
        {
            return RecentViolations.Any(v => v.Severity == InvariantSeverity.Error);
        }
    }
}

/// <summary>
/// Record of an invariant violation.
/// </summary>
public record InvariantViolation
{
    public required InvariantRule Rule { get; init; }
    public required string Message { get; init; }
    public required string Context { get; init; }
    public required InvariantSeverity Severity { get; init; }
    public DateTime Timestamp { get; set; }
    public bool AutoCorrected { get; init; }
    public string? CorrectedValue { get; init; }
}

/// <summary>
/// Categories of invariant rules.
/// </summary>
public enum InvariantRule
{
    TimeInRange,
    TrajectoryNonEmpty,
    TrajectoryMonotonic,
    EigenvaluesSorted,
    EigenvaluesNonNegative,
    EffectiveDimBounded,
    CompareRunsValid,
    CompareTimeSynced,
    DataLengthsConsistent
}

/// <summary>
/// Severity of invariant violations.
/// </summary>
public enum InvariantSeverity
{
    Warning,    // Non-fatal, operation can proceed with degraded behavior
    Error       // Fatal, operation must abort
}
