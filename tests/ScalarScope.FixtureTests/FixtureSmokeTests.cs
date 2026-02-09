// Fixture Smoke Tests - Fast CI Discovery and Validation
// Run with: dotnet test --filter Category=FixtureSmoke

using FluentAssertions;
using ScalarScope.Services.Connectors;
using Xunit;

namespace ScalarScope.FixtureTests;

/// <summary>
/// Smoke tests that discover and validate all fixture sets.
/// These tests ensure fixtures load correctly and basic validation passes.
/// 
/// Usage: dotnet test --filter Category=FixtureSmoke
/// </summary>
[Trait("Category", "FixtureSmoke")]
public class FixtureSmokeTests
{
    private readonly RunTraceValidator _validator = new();
    private readonly RunTraceComparer _comparer = new();

    [Fact]
    public void Should_Discover_At_Least_One_Fixture_Set()
    {
        // Act
        var sets = FixtureLoader.DiscoverFixtureSets();

        // Assert
        sets.Should().NotBeEmpty("at least one fixture set should exist");
    }

    [Fact]
    public void Infrastructure_Optimization_Fixture_Should_Exist()
    {
        // Act
        var sets = FixtureLoader.DiscoverFixtureSets();

        // Assert
        sets.Should().Contain(s => s.Name == "InferenceOptimization",
            "InferenceOptimization fixture set should exist");
    }

    [Fact]
    public void All_Fixture_Sets_Should_Have_RunTrace_Files()
    {
        // Arrange
        var sets = FixtureLoader.DiscoverFixtureSets();

        // Assert
        foreach (var set in sets)
        {
            set.RunTraceFiles.Should().NotBeEmpty(
                $"fixture set '{set.Name}' should have runtrace files");
        }
    }

    [Fact]
    public void All_RunTrace_Files_Should_Load_Successfully()
    {
        // Arrange
        var sets = FixtureLoader.DiscoverFixtureSets();
        var loadErrors = new List<string>();

        // Act
        foreach (var set in sets)
        {
            foreach (var file in set.RunTraceFiles)
            {
                try
                {
                    var trace = FixtureLoader.LoadRunTraceFromSet(set.Path, file);
                    trace.Should().NotBeNull();
                }
                catch (Exception ex)
                {
                    loadErrors.Add($"{set.Name}/{file}: {ex.Message}");
                }
            }
        }

        // Assert
        loadErrors.Should().BeEmpty(
            $"all runtraces should load: {string.Join("; ", loadErrors)}");
    }

    [Fact]
    public void All_Assertion_Files_Should_Load_Successfully()
    {
        // Arrange
        var sets = FixtureLoader.DiscoverFixtureSets();
        var loadErrors = new List<string>();

        // Act
        foreach (var set in sets)
        {
            foreach (var file in set.AssertionFiles)
            {
                try
                {
                    var assertions = FixtureLoader.LoadAssertionsFromSet(set.Path, file);
                    assertions.Should().NotBeNull();
                }
                catch (Exception ex)
                {
                    loadErrors.Add($"{set.Name}/{file}: {ex.Message}");
                }
            }
        }

        // Assert
        loadErrors.Should().BeEmpty(
            $"all assertions should load: {string.Join("; ", loadErrors)}");
    }

    [Fact]
    public void Positive_Fixtures_Should_Pass_Validation()
    {
        // Arrange - load all runtraces that don't have "broken" in the name
        var sets = FixtureLoader.DiscoverFixtureSets();
        var failures = new List<string>();

        // Act
        foreach (var set in sets)
        {
            foreach (var file in set.RunTraceFiles.Where(f => !f.Contains("broken")))
            {
                var trace = FixtureLoader.LoadRunTraceFromSet(set.Path, file);
                var result = _validator.Validate(trace);
                
                if (!result.IsValid)
                {
                    var errorCodes = string.Join(", ", result.Errors.Select(e => e.Code));
                    failures.Add($"{set.Name}/{file}: {errorCodes}");
                }
            }
        }

        // Assert
        failures.Should().BeEmpty(
            $"positive fixtures should pass validation: {string.Join("; ", failures)}");
    }

    [Fact]
    public void Negative_Fixtures_Should_Fail_Validation()
    {
        // Arrange - load all runtraces that have "broken" in the name
        var sets = FixtureLoader.DiscoverFixtureSets();
        var unexpectedPasses = new List<string>();

        // Act
        foreach (var set in sets)
        {
            foreach (var file in set.RunTraceFiles.Where(f => f.Contains("broken")))
            {
                var trace = FixtureLoader.LoadRunTraceFromSet(set.Path, file);
                var result = _validator.Validate(trace);
                
                if (result.IsValid)
                {
                    unexpectedPasses.Add($"{set.Name}/{file}");
                }
            }
        }

        // Assert
        unexpectedPasses.Should().BeEmpty(
            $"broken fixtures should fail validation: {string.Join("; ", unexpectedPasses)}");
    }

    [Fact]
    public void Baseline_Optimized_Pairs_Should_Compare_Successfully()
    {
        // Arrange
        var sets = FixtureLoader.DiscoverFixtureSets();
        var failures = new List<string>();

        // Act - find baseline/optimized pairs (files with matching prefixes)
        foreach (var set in sets)
        {
            var baselineFiles = set.RunTraceFiles.Where(f => 
                f.Contains("baseline") && !f.Contains("broken"))
                .ToList();

            foreach (var baselineFile in baselineFiles)
            {
                // Try to find matching optimized file
                var prefix = baselineFile.Replace("baseline", "");
                var optimizedFile = set.RunTraceFiles.FirstOrDefault(f => 
                    f.Contains("optimized") && !f.Contains("broken") &&
                    f.Replace("optimized", "") == prefix);

                if (optimizedFile != null)
                {
                    try
                    {
                        var baseline = FixtureLoader.LoadRunTraceFromSet(set.Path, baselineFile);
                        var optimized = FixtureLoader.LoadRunTraceFromSet(set.Path, optimizedFile);
                        var intent = ComparisonIntent.TfrtOptimization();
                        
                        var result = _comparer.Compare(baseline, optimized, intent);
                        result.Should().NotBeNull();
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{set.Name}/{baselineFile} vs {optimizedFile}: {ex.Message}");
                    }
                }
            }
        }

        // Assert
        failures.Should().BeEmpty(
            $"baseline/optimized pairs should compare: {string.Join("; ", failures)}");
    }

    [Fact]
    public void Review_Bundle_Export_Should_Succeed_For_Valid_Comparisons()
    {
        // Arrange - use first valid comparison we can find
        var sets = FixtureLoader.DiscoverFixtureSets();
        var exported = false;

        // Act
        foreach (var set in sets)
        {
            var baselineFile = set.RunTraceFiles.FirstOrDefault(f => 
                f.Contains("baseline") && !f.Contains("broken") && !f.Contains("nearly"));
            var optimizedFile = set.RunTraceFiles.FirstOrDefault(f => 
                f.Contains("optimized") && !f.Contains("broken") && !f.Contains("nearly"));

            if (baselineFile != null && optimizedFile != null)
            {
                var baseline = FixtureLoader.LoadRunTraceFromSet(set.Path, baselineFile);
                var optimized = FixtureLoader.LoadRunTraceFromSet(set.Path, optimizedFile);
                var intent = ComparisonIntent.TfrtOptimization();
                var comparison = _comparer.Compare(baseline, optimized, intent);
                
                var bundle = _comparer.ExportReviewBundle(comparison, baseline, optimized);
                bundle.Should().NotBeNull();
                bundle.BundleHash.Should().NotBeNullOrEmpty();
                exported = true;
                break;
            }
        }

        // Assert
        exported.Should().BeTrue("should find at least one valid comparison to export");
    }

    [Fact]
    public void Fixture_Set_Discovery_Should_Return_Correct_File_Counts()
    {
        // Act
        var sets = FixtureLoader.DiscoverFixtureSets();
        var ioSet = sets.FirstOrDefault(s => s.Name == "InferenceOptimization");

        // Assert
        ioSet.Should().NotBeNull();
        ioSet!.RunTraceFiles.Should().HaveCountGreaterOrEqualTo(4,
            "InferenceOptimization should have baseline, optimized, broken, and nearly_identical traces");
        ioSet.AssertionFiles.Should().HaveCountGreaterOrEqualTo(2,
            "InferenceOptimization should have assertion files");
    }
}
