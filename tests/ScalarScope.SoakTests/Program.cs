using ScalarScope.SoakTests;
using Microsoft.Extensions.Logging;

// Parse command line arguments
var durationMinutes = 120; // Default 2 hours
var outputPath = "soak_test_report.json";
var runValidation = false;
string? validationOutputPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--duration":
        case "-d":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out var duration))
            {
                durationMinutes = duration;
                i++;
            }
            break;
        case "--output":
        case "-o":
            if (i + 1 < args.Length)
            {
                outputPath = args[i + 1];
                i++;
            }
            break;
        case "--quick":
        case "-q":
            durationMinutes = 5; // Quick 5-minute test
            break;
        case "--validate":
        case "-v":
            runValidation = true;
            break;
        case "--validate-output":
            if (i + 1 < args.Length)
            {
                validationOutputPath = args[i + 1];
                i++;
            }
            break;
        case "--help":
        case "-h":
            Console.WriteLine("ScalarScope Soak Test Runner");
            Console.WriteLine();
            Console.WriteLine("Usage: ScalarScope.SoakTests [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -d, --duration <minutes>  Test duration in minutes (default: 120)");
            Console.WriteLine("  -o, --output <path>       Output report path (default: soak_test_report.json)");
            Console.WriteLine("  -q, --quick               Quick 5-minute test");
            Console.WriteLine("  -v, --validate            Run Phase 3.2 validation suite");
            Console.WriteLine("      --validate-output     Validation report output path");
            Console.WriteLine("  -h, --help                Show this help");
            return 0;
    }
}

// Run Phase 3.2 validation if requested
if (runValidation)
{
    var validationRunner = new Phase32ValidationRunner();
    var validationReport = await validationRunner.RunValidationAsync();
    
    var validationOutput = validationOutputPath ?? "phase32_validation_report.md";
    
    await File.WriteAllTextAsync(validationOutput, validationReport.ToSummaryTable());
    
    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║         Phase 3.2 Validation Report Generated                 ║");
    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine(validationReport.ToSummaryTable());
    Console.WriteLine();
    Console.WriteLine($"  Report saved to: {validationOutput}");
    
    return validationReport.AllGatesPassed ? 0 : 1;
}

// Set up logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Information)
        .AddConsole(options =>
        {
            options.TimestampFormat = "[HH:mm:ss] ";
        });
});

var logger = loggerFactory.CreateLogger<SoakTestRunner>();

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║           ScalarScope Soak Test Runner                     ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  Duration:    {durationMinutes} minutes");
Console.WriteLine($"  Output:      {outputPath}");
Console.WriteLine();
Console.WriteLine("  Press Ctrl+C to stop early...");
Console.WriteLine();

var config = new SoakTestConfig
{
    DurationMinutes = durationMinutes,
    PauseBetweenIterationsSeconds = 5,
    MaxMemoryGrowthMB = 100
};

var runner = new SoakTestRunner(logger, config);
var report = await runner.RunAsync();

// Output report
report.WriteToConsole();
await report.WriteToFileAsync(outputPath);

Console.WriteLine();
Console.WriteLine($"  Report saved to: {outputPath}");
Console.WriteLine();

return report.OverallPassed ? 0 : 1;
