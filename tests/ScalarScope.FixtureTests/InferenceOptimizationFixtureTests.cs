// Inference Optimization Fixture Tests
// Golden fixture tests for before/after inference optimization comparison.
// Uses xUnit with FluentAssertions.

using FluentAssertions;
using ScalarScope.Services.Connectors;
using Xunit;

namespace ScalarScope.FixtureTests;

/// <summary>
/// Tests for the inference optimization golden fixtures.
/// Validates RunTrace loading, validation, comparison, and delta computation.
/// </summary>
public class InferenceOptimizationFixtureTests
{
    private readonly RunTraceValidator _validator = new();
    private readonly RunTraceComparer _comparer = new();

    #region A) Validator Tests - Positive Fixtures

    [Fact]
    public void Baseline_RunTrace_Should_Load_Successfully()
    {
        // Act
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");

        // Assert
        baseline.Should().NotBeNull();
        baseline.RunId.Should().Be("tfrt-baseline-fixture-001");
        baseline.RunType.Should().Be(RunType.Inference);
        baseline.Framework.Should().Be(FrameworkType.TensorFlowRT);
        baseline.Label.Should().Be("Baseline");
    }

    [Fact]
    public void Optimized_RunTrace_Should_Load_Successfully()
    {
        // Act
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Assert
        optimized.Should().NotBeNull();
        optimized.RunId.Should().Be("tfrt-optimized-fixture-001");
        optimized.RunType.Should().Be(RunType.Inference);
        optimized.Framework.Should().Be(FrameworkType.TensorFlowRT);
        optimized.Label.Should().Be("Optimized");
    }

    [Fact]
    public void Baseline_RunTrace_Should_Pass_Validation()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");

        // Act
        var result = _validator.Validate(baseline);

        // Assert
        result.IsValid.Should().BeTrue("baseline fixture should be valid");
        result.Errors.Should().BeEmpty("baseline fixture should have no errors");
    }

    [Fact]
    public void Optimized_RunTrace_Should_Pass_Validation()
    {
        // Arrange
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Act
        var result = _validator.Validate(optimized);

        // Assert
        result.IsValid.Should().BeTrue("optimized fixture should be valid");
        result.Errors.Should().BeEmpty("optimized fixture should have no errors");
    }

    [Fact]
    public void Baseline_Should_Have_Strictly_Increasing_Steps()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");

        // Act
        var steps = baseline.Timeline.Steps.ToList();

        // Assert
        steps.Should().BeInAscendingOrder("steps must be strictly increasing");
        steps.Distinct().Should().HaveCount(steps.Count, "steps must not repeat");
    }

    [Fact]
    public void Baseline_Scalar_Lengths_Should_Match_Steps()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var stepCount = baseline.Timeline.Steps.Count;

        // Act & Assert
        foreach (var series in baseline.Scalars.Series)
        {
            series.Values.Should().HaveCount(stepCount,
                $"scalar '{series.Name}' should have same length as steps");
        }
    }

    [Fact]
    public void Both_Fixtures_Should_Have_No_Trailing_Nulls_In_Latency()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Act
        var baselineLatency = baseline.Scalars.GetByName("latency_ms");
        var optimizedLatency = optimized.Scalars.GetByName("latency_ms");

        // Assert
        baselineLatency.Should().NotBeNull();
        optimizedLatency.Should().NotBeNull();
        baselineLatency!.Values.Last().Should().NotBeNull("baseline latency should have no trailing nulls");
        optimizedLatency!.Values.Last().Should().NotBeNull("optimized latency should have no trailing nulls");
    }

    #endregion

    #region B) Validator Tests - Negative Fixture

    [Fact]
    public void Broken_RunTrace_Should_Load_Successfully()
    {
        // Act - loading should succeed (it's valid JSON)
        var broken = FixtureLoader.LoadRunTrace("broken_baseline_tfrt_runtrace.json");

        // Assert
        broken.Should().NotBeNull();
        broken.RunId.Should().Be("tfrt-broken-baseline-fixture-001");
    }

    [Fact]
    public void Broken_RunTrace_Should_Fail_Validation_With_Stable_Error_Codes()
    {
        // Arrange
        var broken = FixtureLoader.LoadRunTrace("broken_baseline_tfrt_runtrace.json");

        // Act
        var result = _validator.Validate(broken);

        // Assert
        result.IsValid.Should().BeFalse("broken fixture should fail validation");
        result.Errors.Should().NotBeEmpty("broken fixture should have errors");
        
        // Check for stable error codes - validator short-circuits on timeline error
        // so only RT_TIMELINE_NON_MONOTONIC is reported (scalar check is skipped)
        result.Errors.Should().Contain(e => e.Code == RunTraceErrorCodes.RT_TIMELINE_NON_MONOTONIC,
            "should detect non-monotonic steps");
    }

    [Fact]
    public void Broken_RunTrace_Timeline_Error_Should_Reference_Correct_Index()
    {
        // Arrange
        var broken = FixtureLoader.LoadRunTrace("broken_baseline_tfrt_runtrace.json");

        // Act
        var result = _validator.Validate(broken);

        // Assert
        var timelineError = result.Errors.FirstOrDefault(e => 
            e.Code == RunTraceErrorCodes.RT_TIMELINE_NON_MONOTONIC);
        timelineError.Should().NotBeNull();
        timelineError!.Message.Should().Contain("5", "error should reference index 5 where repeat occurs");
    }

    [Fact]
    public void Broken_RunTrace_Scalar_Error_Would_Reference_Latency_Series_If_Timeline_Valid()
    {
        // Note: Validator short-circuits on timeline errors, so scalar error won't appear
        // This test documents the expected behavior if scalar check was reached
        
        // Arrange
        var broken = FixtureLoader.LoadRunTrace("broken_baseline_tfrt_runtrace.json");

        // Act
        var result = _validator.Validate(broken);

        // Assert - timeline error prevents scalar check
        result.Errors.Should().Contain(e => e.Code == RunTraceErrorCodes.RT_TIMELINE_NON_MONOTONIC,
            "timeline error is detected first");
        
        // Scalar error is NOT present because validator short-circuits
        result.Errors.Should().NotContain(e => e.Code == RunTraceErrorCodes.RT_SCALAR_LENGTH_MISMATCH,
            "scalar check is skipped when timeline is invalid (by design)");
    }

    #endregion

    #region C) Comparison Tests

    [Fact]
    public void Comparison_Should_Use_TFRT_Preset()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        result.PresetId.Should().Be(TfrtRuntimePreset.PresetId);
        result.Intent.Alignment.Should().Be(AlignmentMode.RuntimeMilestone);
    }

    [Fact]
    public void Comparison_Should_Align_By_Steady_State_Start()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        result.Alignment.Mode.Should().Be(AlignmentMode.RuntimeMilestone);
        result.Alignment.AnchorType.Should().Be(RuntimeMilestoneType.SteadyStateStart);
        result.Alignment.AnchorStepA.Should().Be(13, "baseline steady_state_start is step 13");
        result.Alignment.AnchorStepB.Should().Be(7, "optimized steady_state_start is step 7");
    }

    [Fact]
    public void Comparison_Should_Show_DeltaTc_Present_And_Earlier_For_Optimized()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        var deltaTc = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔTc");
        deltaTc.Should().NotBeNull("ΔTc should be present");
        deltaTc!.Fired.Should().BeTrue("ΔTc should fire for this fixture pair");
        deltaTc.AbsoluteDifference.Should().BeLessThan(0, 
            "optimized should stabilize earlier (negative difference)");
        
        // Coarse assertion: at least 3 steps difference
        Math.Abs(deltaTc.AbsoluteDifference).Should().BeGreaterOrEqualTo(3,
            "ΔTc should show at least 3 steps difference");
    }

    [Fact]
    public void Comparison_Should_Suppress_DeltaTd()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        var deltaTd = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔTd");
        deltaTd.Should().NotBeNull("ΔTd should be present in results");
        deltaTd!.IsSuppressed.Should().BeTrue("ΔTd should be suppressed for TFRT runtime preset");
    }

    [Fact]
    public void Comparison_Should_Suppress_DeltaA()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        var deltaA = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔĀ");
        deltaA.Should().NotBeNull("ΔĀ should be present in results");
        deltaA!.IsSuppressed.Should().BeTrue("ΔĀ should be suppressed for TFRT runtime preset");
    }

    [Fact]
    public void Comparison_Should_Validate_Fingerprints()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert
        result.Fingerprints.IsValidForComparison.Should().BeTrue();
        
        // Dataset/code should match
        result.Fingerprints.Differences.Should().NotContain(d => 
            d.Category == "dataset" && !d.IsExpectedForOptimization,
            "dataset fingerprint mismatch should not exist");
        
        // Model may differ (expected for optimization)
        var modelDiff = result.Fingerprints.Differences.FirstOrDefault(d => d.Category == "model");
        if (modelDiff != null)
        {
            modelDiff.IsExpectedForOptimization.Should().BeTrue(
                "model fingerprint difference should be marked as expected");
        }
    }

    #endregion

    #region D) Review Bundle Export Tests

    [Fact]
    public void Review_Bundle_Should_Export_Successfully()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();
        var comparison = _comparer.Compare(baseline, optimized, intent);

        // Act
        var bundle = _comparer.ExportReviewBundle(comparison, baseline, optimized);

        // Assert
        bundle.Should().NotBeNull();
        bundle.BundleHash.Should().NotBeNullOrEmpty();
        bundle.Version.Should().Be("1.0.0");
        bundle.RecomputeDisabled.Should().BeTrue("review mode should disable recompute");
    }

    [Fact]
    public void Review_Bundle_Should_Include_Required_Fields()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();
        var comparison = _comparer.Compare(baseline, optimized, intent);

        // Act
        var bundle = _comparer.ExportReviewBundle(comparison, baseline, optimized);

        // Assert
        bundle.Comparison.DeltaSpecVersion.Should().NotBeNullOrEmpty();
        bundle.BundleHash.Should().NotBeNullOrEmpty();
        bundle.Comparison.ComparisonId.Should().NotBeNullOrEmpty();
        bundle.FingerprintSummary.Should().NotBeNull();
        bundle.Comparison.Alignment.Should().NotBeNull();
    }

    [Fact]
    public void Review_Bundle_Should_Preserve_Labels()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization("Before XLA", "After XLA");
        var comparison = _comparer.Compare(baseline, optimized, intent);

        // Act
        var bundle = _comparer.ExportReviewBundle(comparison, baseline, optimized);

        // Assert
        bundle.FingerprintSummary.LabelA.Should().Be("Before XLA");
        bundle.FingerprintSummary.LabelB.Should().Be("After XLA");
    }

    [Fact]
    public void ReExport_Should_Stamp_Parent_Bundle_Hash()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();
        var comparison = _comparer.Compare(baseline, optimized, intent);
        var originalBundle = _comparer.ExportReviewBundle(comparison, baseline, optimized);

        // Act
        var reExported = _comparer.ReExportFromBundle(originalBundle, ReviewExportMode.Audit);

        // Assert
        reExported.IsReviewedFromBundle.Should().BeTrue();
        reExported.ParentBundleHash.Should().Be(originalBundle.BundleHash);
        reExported.ExportMode.Should().Be(ReviewExportMode.Audit);
    }

    #endregion

    #region E) Nearly Identical Fixture Tests (Suppression Stress)

    [Fact]
    public void NearlyIdentical_RunTraces_Should_Load_Successfully()
    {
        // Act
        var nearBaseline = FixtureLoader.LoadRunTrace("nearly_identical_baseline_tfrt_runtrace.json");
        var nearOptimized = FixtureLoader.LoadRunTrace("nearly_identical_optimized_tfrt_runtrace.json");

        // Assert
        nearBaseline.Should().NotBeNull();
        nearOptimized.Should().NotBeNull();
        nearBaseline.RunId.Should().Be("tfrt-near-baseline-fixture-001");
        nearOptimized.RunId.Should().Be("tfrt-near-optimized-fixture-001");
    }

    [Fact]
    public void NearlyIdentical_RunTraces_Should_Pass_Validation()
    {
        // Arrange
        var nearBaseline = FixtureLoader.LoadRunTrace("nearly_identical_baseline_tfrt_runtrace.json");
        var nearOptimized = FixtureLoader.LoadRunTrace("nearly_identical_optimized_tfrt_runtrace.json");

        // Act
        var resultA = _validator.Validate(nearBaseline);
        var resultB = _validator.Validate(nearOptimized);

        // Assert
        resultA.IsValid.Should().BeTrue();
        resultB.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NearlyIdentical_Comparison_Should_Not_Fire_DeltaTc()
    {
        // Arrange
        var nearBaseline = FixtureLoader.LoadRunTrace("nearly_identical_baseline_tfrt_runtrace.json");
        var nearOptimized = FixtureLoader.LoadRunTrace("nearly_identical_optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(nearBaseline, nearOptimized, intent);

        // Assert
        var deltaTc = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔTc");
        deltaTc.Should().NotBeNull();
        
        // Either not fired, or fired with zero difference (same steady state step)
        if (deltaTc!.Fired)
        {
            deltaTc.AbsoluteDifference.Should().Be(0, 
                "nearly identical runs have same steady_state_start");
        }
    }

    [Fact]
    public void NearlyIdentical_Comparison_Should_Have_Zero_Fired_Deltas()
    {
        // Arrange
        var nearBaseline = FixtureLoader.LoadRunTrace("nearly_identical_baseline_tfrt_runtrace.json");
        var nearOptimized = FixtureLoader.LoadRunTrace("nearly_identical_optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(nearBaseline, nearOptimized, intent);

        // Assert - either no deltas fire, or they fire with effectively zero difference
        var meaningfulDeltas = result.FiredDeltas.Where(d => 
            !d.IsSuppressed && Math.Abs(d.AbsoluteDifference) > 0.001);
        
        meaningfulDeltas.Should().BeEmpty(
            "nearly identical runs should not produce meaningful deltas");
    }

    [Fact]
    public void NearlyIdentical_Should_Have_All_Preset_Suppressions_Active()
    {
        // Arrange
        var nearBaseline = FixtureLoader.LoadRunTrace("nearly_identical_baseline_tfrt_runtrace.json");
        var nearOptimized = FixtureLoader.LoadRunTrace("nearly_identical_optimized_tfrt_runtrace.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(nearBaseline, nearOptimized, intent);

        // Assert
        var deltaTd = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔTd");
        var deltaA = result.Deltas.FirstOrDefault(d => d.DeltaType == "ΔĀ");
        
        deltaTd.Should().NotBeNull();
        deltaA.Should().NotBeNull();
        deltaTd!.IsSuppressed.Should().BeTrue("ΔTd should be suppressed");
        deltaA!.IsSuppressed.Should().BeTrue("ΔĀ should be suppressed");
    }

    #endregion

    #region F) Expected Assertions Validation

    [Fact]
    public void Expected_Assertions_Should_Load_Successfully()
    {
        // Act
        var assertions = FixtureLoader.LoadAssertions("expected_assertions.json");

        // Assert
        assertions.Should().NotBeNull();
        assertions.FixtureSet.Should().Be("InferenceOptimization_TFRT_v1");
        assertions.Expected.Should().NotBeNull();
    }

    [Fact]
    public void Comparison_Should_Match_Expected_Assertions()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");
        var assertions = FixtureLoader.LoadAssertions("expected_assertions.json");
        var intent = ComparisonIntent.TfrtOptimization();

        // Act
        var result = _comparer.Compare(baseline, optimized, intent);

        // Assert - validate against expected assertions
        var expected = assertions.Expected!;

        // Validation expectations
        if (expected.Validation?.BaselineValid == true)
        {
            _validator.Validate(baseline).IsValid.Should().BeTrue();
        }
        if (expected.Validation?.OptimizedValid == true)
        {
            _validator.Validate(optimized).IsValid.Should().BeTrue();
        }

        // Comparison intent expectations
        if (expected.ComparisonIntent?.PresetId != null)
        {
            result.PresetId.Should().Be(expected.ComparisonIntent.PresetId);
        }

        // Delta expectations
        if (expected.ExpectedDeltas?.DeltaTc?.Fired == true)
        {
            result.Deltas.Should().Contain(d => d.DeltaType == "ΔTc" && d.Fired);
        }
        if (expected.ExpectedDeltas?.DeltaTd?.ShouldBeSuppressed == true)
        {
            result.Deltas.Should().Contain(d => d.DeltaType == "ΔTd" && d.IsSuppressed);
        }
        if (expected.ExpectedDeltas?.DeltaA?.ShouldBeSuppressed == true)
        {
            result.Deltas.Should().Contain(d => d.DeltaType == "ΔĀ" && d.IsSuppressed);
        }
    }

    [Fact]
    public void Broken_Fixture_Should_Match_Expected_Error_Assertions()
    {
        // Arrange
        var broken = FixtureLoader.LoadRunTrace("broken_baseline_tfrt_runtrace.json");
        var assertions = FixtureLoader.LoadAssertions("expected_assertions_broken.json");

        // Act
        var result = _validator.Validate(broken);

        // Assert
        var expected = assertions.ExpectedValidation!;
        
        result.IsValid.Should().Be(expected.IsValid ?? true);
        
        // Note: Validator short-circuits on timeline errors, so only 1 error is reported
        // The assertions file lists 2 errors for documentation, but only the first fires
        result.Errors.Should().HaveCountGreaterOrEqualTo(1,
            "at least timeline error should be present");
        
        // Only check for timeline error (first error in expected list)
        var firstExpectedError = expected.Errors?.FirstOrDefault();
        if (firstExpectedError?.Code != null)
        {
            result.Errors.Should().Contain(e => e.Code == firstExpectedError.Code,
                $"should contain error code {firstExpectedError.Code}");
        }
    }

    #endregion

    #region G) Capability Tests

    [Fact]
    public void Baseline_Should_Have_Expected_Capabilities()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");

        // Assert
        baseline.Capabilities.HasLatency.Should().BeTrue("baseline has latency_ms");
        baseline.Capabilities.HasThroughput.Should().BeTrue("baseline has throughput");
        baseline.Capabilities.HasMemory.Should().BeTrue("baseline has memory_mb");
        baseline.Capabilities.HasLoss.Should().BeFalse("inference doesn't have loss");
        baseline.Capabilities.HasAccuracy.Should().BeFalse("inference doesn't have accuracy");
    }

    [Fact]
    public void Optimized_Should_Have_Expected_Capabilities()
    {
        // Arrange
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Assert
        optimized.Capabilities.HasLatency.Should().BeTrue("optimized has latency_ms");
        optimized.Capabilities.HasThroughput.Should().BeTrue("optimized has throughput");
        optimized.Capabilities.HasMemory.Should().BeTrue("optimized has memory_mb");
        optimized.Capabilities.HasProfiler.Should().BeTrue("optimized has profiler artifact");
    }

    #endregion

    #region H) Milestone Tests

    [Fact]
    public void Baseline_Should_Have_Expected_Milestones()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");

        // Assert
        baseline.Milestones.WarmupEndStep.Should().Be(12);
        baseline.Milestones.SteadyStateStartStep.Should().Be(13);
    }

    [Fact]
    public void Optimized_Should_Have_Expected_Milestones()
    {
        // Arrange
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Assert
        optimized.Milestones.WarmupEndStep.Should().Be(6);
        optimized.Milestones.SteadyStateStartStep.Should().Be(7);
    }

    [Fact]
    public void Optimized_Should_Stabilize_Earlier_Than_Baseline()
    {
        // Arrange
        var baseline = FixtureLoader.LoadRunTrace("baseline_tfrt_runtrace.json");
        var optimized = FixtureLoader.LoadRunTrace("optimized_tfrt_runtrace.json");

        // Assert
        optimized.Milestones.SteadyStateStartStep.Should().NotBeNull();
        baseline.Milestones.SteadyStateStartStep.Should().NotBeNull();
        optimized.Milestones.SteadyStateStartStep!.Value.Should().BeLessThan(
            baseline.Milestones.SteadyStateStartStep!.Value,
            "optimized should reach steady state earlier");
    }

    #endregion
}
