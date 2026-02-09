using ScalarScope.Models;

namespace ScalarScope.Services;

/// <summary>
/// Phase 6.1: Determinism test suite for verifying reproducibility.
/// Can be run from the app or as part of CI.
/// </summary>
public static class DeterminismTestSuite
{
    /// <summary>
    /// Run all determinism tests and return results.
    /// </summary>
    public static DeterminismTestResults RunAllTests()
    {
        var results = new DeterminismTestResults();
        
        results.Add(TestInputNormalization());
        results.Add(TestFingerprintDeterminism());
        results.Add(TestDeltaHashDeterminism());
        results.Add(TestDoubleNormalization());
        results.Add(TestRunIdNormalization());
        
        return results;
    }
    
    /// <summary>
    /// Test that input normalization is consistent.
    /// </summary>
    public static DeterminismTestResult TestInputNormalization()
    {
        try
        {
            // Same logical input with different formatting
            var input1 = InputNormalizer.NormalizeComparisonInput(
                "Run_A ", "  run-b", 100, 100, 0);
            var input2 = InputNormalizer.NormalizeComparisonInput(
                "run_a", "RUN-B", 100, 100, 0);
            
            var passed = input1.CanonicalForm == input2.CanonicalForm;
            
            return new DeterminismTestResult
            {
                TestName = "InputNormalization",
                Passed = passed,
                Message = passed 
                    ? "Normalized inputs match" 
                    : $"Mismatch: '{input1.CanonicalForm}' vs '{input2.CanonicalForm}'"
            };
        }
        catch (Exception ex)
        {
            return new DeterminismTestResult
            {
                TestName = "InputNormalization",
                Passed = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Test that fingerprints are deterministic across calls.
    /// </summary>
    public static DeterminismTestResult TestFingerprintDeterminism()
    {
        try
        {
            var fp1 = DeterminismService.ComputeInputFingerprint("run_a", "run_b", 0, 100, 100);
            var fp2 = DeterminismService.ComputeInputFingerprint("run_a", "run_b", 0, 100, 100);
            var fp3 = DeterminismService.ComputeInputFingerprint("run_a", "run_b", 0, 100, 100);
            
            var passed = fp1 == fp2 && fp2 == fp3;
            
            return new DeterminismTestResult
            {
                TestName = "FingerprintDeterminism",
                Passed = passed,
                Message = passed 
                    ? $"Fingerprint consistent: {fp1}" 
                    : $"Fingerprint varied: {fp1}, {fp2}, {fp3}"
            };
        }
        catch (Exception ex)
        {
            return new DeterminismTestResult
            {
                TestName = "FingerprintDeterminism",
                Passed = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Test that delta hashes are deterministic for same input.
    /// </summary>
    public static DeterminismTestResult TestDeltaHashDeterminism()
    {
        try
        {
            var testDeltas = new[]
            {
                new { Id = "delta_f", Status = "Present", Confidence = 0.95, Explanation = "Test" },
                new { Id = "delta_tc", Status = "Absent", Confidence = 0.0, Explanation = "" }
            };
            
            var hash1 = DeterminismService.ComputeDeltaHash(testDeltas);
            var hash2 = DeterminismService.ComputeDeltaHash(testDeltas);
            var hash3 = DeterminismService.ComputeDeltaHash(testDeltas);
            
            var passed = hash1 == hash2 && hash2 == hash3;
            
            return new DeterminismTestResult
            {
                TestName = "DeltaHashDeterminism",
                Passed = passed,
                Message = passed 
                    ? $"Delta hash consistent: {hash1}" 
                    : $"Delta hash varied: {hash1}, {hash2}, {hash3}"
            };
        }
        catch (Exception ex)
        {
            return new DeterminismTestResult
            {
                TestName = "DeltaHashDeterminism",
                Passed = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Test double normalization handles edge cases.
    /// </summary>
    public static DeterminismTestResult TestDoubleNormalization()
    {
        try
        {
            var tests = new (double input, double expected)[]
            {
                (0.123456789012345, 0.1234567890),
                (double.NaN, 0.0),
                (double.PositiveInfinity, double.MaxValue),
                (double.NegativeInfinity, double.MinValue),
                (1.0 / 3.0, 0.3333333333)
            };
            
            var failures = new List<string>();
            foreach (var (input, expected) in tests)
            {
                var actual = InputNormalizer.NormalizeDouble(input);
                if (Math.Abs(actual - expected) > 1e-12)
                {
                    failures.Add($"{input} -> {actual} (expected {expected})");
                }
            }
            
            var passed = failures.Count == 0;
            
            return new DeterminismTestResult
            {
                TestName = "DoubleNormalization",
                Passed = passed,
                Message = passed 
                    ? "All double normalizations correct" 
                    : $"Failures: {string.Join(", ", failures)}"
            };
        }
        catch (Exception ex)
        {
            return new DeterminismTestResult
            {
                TestName = "DoubleNormalization",
                Passed = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }
    
    /// <summary>
    /// Test run ID normalization handles edge cases.
    /// </summary>
    public static DeterminismTestResult TestRunIdNormalization()
    {
        try
        {
            var tests = new (string? input, string expected)[]
            {
                (null, "UNKNOWN"),
                ("", "UNKNOWN"),
                ("  ", "UNKNOWN"),
                ("Run_A", "run_a"),
                ("  Run-B  ", "run-b"),
                ("RUN C", "run_c")
            };
            
            var failures = new List<string>();
            foreach (var (input, expected) in tests)
            {
                var actual = InputNormalizer.NormalizeRunId(input);
                if (actual != expected)
                {
                    failures.Add($"'{input}' -> '{actual}' (expected '{expected}')");
                }
            }
            
            var passed = failures.Count == 0;
            
            return new DeterminismTestResult
            {
                TestName = "RunIdNormalization",
                Passed = passed,
                Message = passed 
                    ? "All run ID normalizations correct" 
                    : $"Failures: {string.Join(", ", failures)}"
            };
        }
        catch (Exception ex)
        {
            return new DeterminismTestResult
            {
                TestName = "RunIdNormalization",
                Passed = false,
                Message = $"Exception: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Result of a single determinism test.
/// </summary>
public record DeterminismTestResult
{
    public required string TestName { get; init; }
    public bool Passed { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Collection of determinism test results.
/// </summary>
public class DeterminismTestResults
{
    private readonly List<DeterminismTestResult> _results = new();
    
    public IReadOnlyList<DeterminismTestResult> Results => _results;
    public int TotalTests => _results.Count;
    public int PassedTests => _results.Count(r => r.Passed);
    public int FailedTests => _results.Count(r => !r.Passed);
    public bool AllPassed => _results.All(r => r.Passed);
    
    public void Add(DeterminismTestResult result) => _results.Add(result);
    
    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Determinism Test Results: {PassedTests}/{TotalTests} passed");
        sb.AppendLine(new string('=', 50));
        
        foreach (var result in _results)
        {
            var status = result.Passed ? "✓" : "✗";
            sb.AppendLine($"{status} {result.TestName}: {result.Message}");
        }
        
        return sb.ToString();
    }
}
